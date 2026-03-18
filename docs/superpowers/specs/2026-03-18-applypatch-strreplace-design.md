# Design: ApplyPatch — StrReplace Support + Library Rename

**Date:** 2026-03-18
**Status:** Approved

---

## Summary

Expand the `ApplyPatchV4A` library to support a second patch format — the Anthropic StrReplace format — and rename the library to `ApplyPatch` to reflect its broader scope.

---

## Scope

### In Scope

- Rename the library from `ApplyPatchV4A` to `ApplyPatch` (folders, projects, namespaces, NuGet package ID)
- Add `ApplyPatch.StrReplace(input, oldStr, newStr, allowMulti, useRegex)` static method
- Support `allow_multi`: replace all occurrences when true; throw when false and multiple matches found
- Support `use_regex`: treat `oldStr` as a .NET regex pattern with a 5-second timeout; `newStr` is a literal string (no capture group substitution)
- Apply existing 4-tier fuzzy matching (exact → trimEnd → trim → unicode) when `useRegex = false`
- Preserve input newline style in output

### Out of Scope

- JSON deserialization (callers pass pre-parsed `oldStr`/`newStr` strings)
- Anthropic `view`, `create`, `insert`, `undo_edit` commands
- Aider SEARCH/REPLACE block format
- Regex capture group substitution in `newStr`
- Regex features beyond `System.Text.RegularExpressions` with timeout

---

## Public API

The public class is `ApplyPatch` in the `ApplyPatch` namespace. The class name matches the namespace — this is intentional and valid C#. Internal classes live in `ApplyPatch.Internal` (a child namespace), so they always reference the public class unambiguously via unqualified name `ApplyPatch` or not at all (internal classes are called by `ApplyPatch`, not vice versa).

```csharp
namespace ApplyPatch
{
    public static class ApplyPatch
    {
        // Existing — unchanged signatures
        public static string Create(string diff);
        public static string Apply(string input, string diff);

        // New
        public static string StrReplace(
            string input,
            string oldStr,
            string newStr,
            bool allowMulti = false,
            bool useRegex = false);
    }
}
```

### `StrReplace` Behavior

| Scenario | Result |
|---|---|
| Single match found | Replace matched region with `newStr`; return modified input |
| No match found | Throw `PatchApplyException` |
| Multiple matches, `allowMulti = false` | Throw `PatchApplyException` with match count in message |
| Multiple matches, `allowMulti = true` | Replace all occurrences (reverse order); return modified input |
| `useRegex = true`, valid pattern | Use `Regex.Matches` with 5s timeout; `newStr` treated as literal string; skip fuzzy matching |
| `useRegex = true`, invalid pattern | Throw `PatchApplyException` wrapping `RegexParseException` |
| `useRegex = true`, timeout exceeded | Throw `PatchApplyException` wrapping `RegexMatchTimeoutException` |
| `newStr = ""` | Deletion — matched region removed; `InsLines` is an empty list (not a list containing one empty string) |

---

## Architecture

### Rename

Full rename — folders, `.csproj` names, assembly names, root namespace, NuGet package ID:

| Before | After |
|---|---|
| `src/ApplyPatchV4A/` | `src/ApplyPatch/` |
| `tests/ApplyPatchV4A.Tests/` | `tests/ApplyPatch.Tests/` |
| `ApplyPatchV4A.csproj` | `ApplyPatch.csproj` |
| `namespace ApplyPatchV4A` | `namespace ApplyPatch` |
| `namespace ApplyPatchV4A.Internal` | `namespace ApplyPatch.Internal` |
| `[InternalsVisibleTo("ApplyPatchV4A.Tests")]` | `[InternalsVisibleTo("ApplyPatch.Tests")]` |

### Pipeline

```
StrReplace(input, oldStr, newStr, allowMulti, useRegex)
    → NewlineHelper.DetectNewline(input)
    → newlineNormalized = NewlineHelper.NormalizeToLf(input)
    → lines = newlineNormalized.Split('\n')
    → StrReplaceParser.FindMatches(newlineNormalized, lines, oldStr, useRegex)   ← NEW
        [useRegex=false] → split oldStr into oldLines
                         → loop: ContextMatcher.FindContext(lines, oldLines, start, eof:false)
                           advance start = match.NewIndex + oldLines.Count after each hit
                           until NewIndex == -1
        [useRegex=true]  → Regex(oldStr, timeout=5s).Matches(newlineNormalized)
                         → convert char-offset matches to line indices (count '\n' chars in
                           newlineNormalized up to each match.Index)
    → validate match count vs allowMulti
    → build Chunk[] per match:
        OrigIndex = match.NewIndex (fuzzy) or computed line index (regex)
        DelLines  = oldLines (fuzzy) or lines spanned by regex match
        InsLines  = newStr.Split('\n') if newStr != "" else empty list
    → ChunkApplier.Apply(lines, chunks, newline)              ← reused unchanged
    → return result
```

### New Internal Class: `StrReplaceParser`

**Location:** `src/ApplyPatch/Internal/StrReplaceParser.cs`

**Responsibilities:**

- **Non-regex path:**
  - Normalize `oldStr` to LF, split into `oldLines`
  - Call `ContextMatcher.FindContext(lines, oldLines, start, eof: false)` in a loop
  - After each found match, advance `start = match.NewIndex + oldLines.Count`
  - Repeat until `NewIndex == -1`
  - Return all found `ContextMatch` values
- **Regex path:**
  - Instantiate `Regex(oldStr, options, MatchTimeout = TimeSpan.FromSeconds(5))`
  - Call `Regex.Matches(newlineNormalized)` — runs against the LF-normalized joined string
  - For each `Match`: count `\n` chars in `newlineNormalized[0..match.Index]` to get `startLine`; count `\n` chars in the matched text to get `lineCount`
  - Wrap `RegexParseException` → `PatchApplyException`; wrap `RegexMatchTimeoutException` → `PatchApplyException`
  - Return matches as `(startLine, lineCount)` pairs

**Reused unchanged:**
- `NewlineHelper` — detection and normalization
- `ContextMatcher` — 4-tier fuzzy matching; already accepts `start` parameter for cursor advancement
- `ChunkApplier` — reverse-order chunk application
- `Chunk`, `ParsedDiff`, `ContextMatch` data structures
- `PatchApplyException` — error surface

---

## Error Handling

All errors surface as `PatchApplyException` with meaningful messages:

| Error | `PatchApplyException.Message` | Other Properties |
|---|---|---|
| `old_str` not found | `"old_str not found in input"` | `LineNumber = null`, `Fuzz = 0` |
| Multiple matches, `allowMulti = false` | `"old_str found {N} times; set allowMulti = true to replace all"` | `Fuzz = minimum fuzz across all matches` |
| Invalid regex | `"Invalid regex pattern: {regexMessage}"` | wraps `RegexParseException` |
| Regex timeout | `"Regex match timed out after 5 seconds"` | wraps `RegexMatchTimeoutException` |

---

## Fuzzy Matching Semantics for `allowMulti = true`

When fuzzy matching collects multiple occurrences, each match may be found at a different fuzz level. All matches are accepted regardless of individual fuzz levels. The `Fuzz` property on `PatchApplyException` (when `allowMulti = false` throws) is the **minimum** fuzz across all matches found — this represents the best-quality match to help the caller understand the ambiguity.

---

## Testing

New test class: `tests/ApplyPatch.Tests/StrReplaceTests.cs`

| Test Group | Scenarios |
|---|---|
| Basic | Single-line replacement, multi-line old/new, empty newStr (deletion) |
| Fuzzy matching | Trailing whitespace (fuzz=1), full trim (fuzz=100), unicode normalization (fuzz=1000) |
| `allowMulti = false` | Single match succeeds, multiple matches throws with count |
| `allowMulti = true` | Replaces all occurrences; reverse-order correctness |
| `useRegex = true` | Simple pattern, multi-line content, `newStr` as literal (no capture group expansion) |
| Regex errors | Invalid pattern throws, timeout throws |
| Newline preservation | CRLF input → CRLF output, LF input → LF output |
| Edge cases | Replacement at start of file, end of file, `old_str` = entire input, `new_str` = `""` |

Existing test classes renamed (namespace updated, no logic changes):
- `ApplyBasicTests`, `CreateTests`, `FuzzyMatchingTests`, `AnchorTests`, `EofTests`, `ErrorTests`, `EdgeCaseTests`

---

## Open Questions / Decisions Made

| Question | Decision |
|---|---|
| JSON deserialization? | No — callers pass pre-parsed strings |
| Which commands? | `str_replace` only |
| Fuzzy matching? | Yes — 4-tier, line-by-line, same as V4A |
| `allow_multi`? | Yes — throw on ambiguity by default |
| `use_regex`? | Yes — .NET `System.Text.RegularExpressions` with 5s timeout |
| `newStr` in regex mode? | Literal string — no capture group substitution |
| Multiple matches + `allow_multi = true` application order? | Reverse order (consistent with ChunkApplier) |
| Fuzz for multiple-match exception? | Minimum fuzz across all matches |
| `newStr = ""` InsLines representation? | Empty `List<string>` (not a list containing one empty string) |
| Namespace / assembly? | Full rename to `ApplyPatch`; class name also `ApplyPatch` (valid C#, no internal ambiguity) |
| Regex runs against? | LF-normalized string (`newlineNormalized`) |
| Char offset → line index conversion? | Count `\n` in `newlineNormalized[0..match.Index]` |
