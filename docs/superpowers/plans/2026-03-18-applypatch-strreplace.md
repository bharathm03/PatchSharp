# ApplyPatch StrReplace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the library from `ApplyPatchV4A` to `ApplyPatch` and add a `StrReplace` public method supporting fuzzy matching, `allowMulti`, and `useRegex`.

**Architecture:** A new `StrReplaceParser` internal class handles match discovery — fuzzy path reuses existing `ContextMatcher`, regex path does char-offset matching via `System.Text.RegularExpressions`. The public `ApplyPatch.StrReplace` method orchestrates detection, matching, validation, and delegates to `ChunkApplier` (fuzzy) or inline `StringBuilder` (regex).

**Tech Stack:** C# / .NET (netstandard2.1, net8.0, net10.0), xUnit, `System.Text.RegularExpressions`

**Spec:** `docs/superpowers/specs/2026-03-18-applypatch-strreplace-design.md`

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Rename folder | `src/ApplyPatchV4A/` → `src/ApplyPatch/` | Library source root |
| Rename | `src/ApplyPatch/ApplyPatchV4A.csproj` → `src/ApplyPatch/ApplyPatch.csproj` | Project file |
| Modify | `src/ApplyPatch/ApplyPatch.csproj` | PackageId, tags, InternalsVisibleTo |
| Modify namespace | `src/ApplyPatch/ApplyPatch.cs` | Add `StrReplace` method |
| Modify namespace | `src/ApplyPatch/PatchApplyException.cs` | Namespace only |
| Modify namespace | `src/ApplyPatch/Internal/Chunk.cs` | Namespace only |
| Modify namespace | `src/ApplyPatch/Internal/ChunkApplier.cs` | Namespace only |
| Modify namespace | `src/ApplyPatch/Internal/ContextMatcher.cs` | Namespace only |
| Modify namespace | `src/ApplyPatch/Internal/DiffParser.cs` | Namespace only |
| Modify namespace | `src/ApplyPatch/Internal/NewlineHelper.cs` | Namespace only |
| **Create** | `src/ApplyPatch/Internal/StrReplaceParser.cs` | Find all fuzzy/regex matches |
| Rename folder | `tests/ApplyPatchV4A.Tests/` → `tests/ApplyPatch.Tests/` | Test project root |
| Rename | `tests/ApplyPatch.Tests/ApplyPatchV4A.Tests.csproj` → `tests/ApplyPatch.Tests/ApplyPatch.Tests.csproj` | Test project file |
| Modify | `tests/ApplyPatch.Tests/ApplyPatch.Tests.csproj` | Update ProjectReference path |
| Modify namespace | `tests/ApplyPatch.Tests/ApplyBasicTests.cs` | Namespace only |
| Modify namespace | `tests/ApplyPatch.Tests/CreateTests.cs` | Namespace only |
| Modify namespace | `tests/ApplyPatch.Tests/FuzzyMatchingTests.cs` | Namespace + using |
| Modify namespace | `tests/ApplyPatch.Tests/AnchorTests.cs` | Namespace only |
| Modify namespace | `tests/ApplyPatch.Tests/EofTests.cs` | Namespace only |
| Modify namespace | `tests/ApplyPatch.Tests/ErrorTests.cs` | Namespace only |
| Modify namespace | `tests/ApplyPatch.Tests/EdgeCaseTests.cs` | Namespace only |
| **Create** | `tests/ApplyPatch.Tests/StrReplaceTests.cs` | All StrReplace tests |

---

## Task 1: Rename Project Folders and Files

**Files:** All source and test project files

- [ ] **Step 1: Move source folder**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
git mv src/ApplyPatchV4A src/ApplyPatch
```

- [ ] **Step 2: Rename the library .csproj**

```bash
git mv src/ApplyPatch/ApplyPatchV4A.csproj src/ApplyPatch/ApplyPatch.csproj
```

- [ ] **Step 3: Move test folder**

```bash
git mv tests/ApplyPatchV4A.Tests tests/ApplyPatch.Tests
```

- [ ] **Step 4: Rename the test .csproj**

```bash
git mv "tests/ApplyPatch.Tests/ApplyPatchV4A.Tests.csproj" "tests/ApplyPatch.Tests/ApplyPatch.Tests.csproj"
```

- [ ] **Step 5: Update library .csproj content**

Edit `src/ApplyPatch/ApplyPatch.csproj` — replace the full content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.1;net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <PackageId>ApplyPatch</PackageId>
    <Description>Apply patch format diffs to text content (V4A and StrReplace formats)</Description>
    <PackageTags>openai;v4a;str-replace;patch;diff</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ApplyPatch.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Update test .csproj content**

Edit `tests/ApplyPatch.Tests/ApplyPatch.Tests.csproj` — replace the full content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ApplyPatch\ApplyPatch.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: Update namespaces in all source .cs files**

Use PowerShell (more reliable than `sed -i` on Windows) to update namespace declarations:

```powershell
cd C:\Users\bhara\OneDrive\Documents\GitHub\ApplyPatchV4A

# Internal namespace files
$internalFiles = @(
    'src\ApplyPatch\Internal\Chunk.cs',
    'src\ApplyPatch\Internal\ChunkApplier.cs',
    'src\ApplyPatch\Internal\ContextMatcher.cs',
    'src\ApplyPatch\Internal\DiffParser.cs',
    'src\ApplyPatch\Internal\NewlineHelper.cs'
)
foreach ($f in $internalFiles) {
    (Get-Content $f -Raw).Replace('namespace ApplyPatchV4A.Internal;', 'namespace ApplyPatch.Internal;') | Set-Content $f -NoNewline
}

# Root namespace files
$rootFiles = @('src\ApplyPatch\ApplyPatch.cs', 'src\ApplyPatch\PatchApplyException.cs')
foreach ($f in $rootFiles) {
    (Get-Content $f -Raw).Replace('namespace ApplyPatchV4A;', 'namespace ApplyPatch;') | Set-Content $f -NoNewline
}

# using statement in ApplyPatch.cs
$ap = 'src\ApplyPatch\ApplyPatch.cs'
(Get-Content $ap -Raw).Replace('using ApplyPatchV4A.Internal;', 'using ApplyPatch.Internal;') | Set-Content $ap -NoNewline
```

- [ ] **Step 8: Update namespaces in all test .cs files**

```powershell
cd C:\Users\bhara\OneDrive\Documents\GitHub\ApplyPatchV4A

$testFiles = @(
    'tests\ApplyPatch.Tests\ApplyBasicTests.cs',
    'tests\ApplyPatch.Tests\CreateTests.cs',
    'tests\ApplyPatch.Tests\FuzzyMatchingTests.cs',
    'tests\ApplyPatch.Tests\AnchorTests.cs',
    'tests\ApplyPatch.Tests\EofTests.cs',
    'tests\ApplyPatch.Tests\ErrorTests.cs',
    'tests\ApplyPatch.Tests\EdgeCaseTests.cs'
)
foreach ($f in $testFiles) {
    (Get-Content $f -Raw).Replace('namespace ApplyPatchV4A.Tests;', 'namespace ApplyPatch.Tests;') | Set-Content $f -NoNewline
}

# FuzzyMatchingTests also has a using statement
$fuzzy = 'tests\ApplyPatch.Tests\FuzzyMatchingTests.cs'
(Get-Content $fuzzy -Raw).Replace('using ApplyPatchV4A.Internal;', 'using ApplyPatch.Internal;') | Set-Content $fuzzy -NoNewline
```

- [ ] **Step 9: Build to verify rename is complete**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 10: Run existing tests to verify nothing broke**

```bash
dotnet test
```

Expected: All existing tests pass (should be the same count as before rename).

- [ ] **Step 11: Commit the rename**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
git add -A
git commit -m "refactor: rename library from ApplyPatchV4A to ApplyPatch"
```

---

## Task 2: Write Failing Tests for StrReplace (Fuzzy Path)

**Files:**
- Create: `tests/ApplyPatch.Tests/StrReplaceTests.cs`

- [ ] **Step 1: Create the test file with fuzzy-path tests**

```csharp
// tests/ApplyPatch.Tests/StrReplaceTests.cs
using System;
using System.Text.RegularExpressions;

namespace ApplyPatch.Tests;

public class StrReplaceTests
{
    // ── Basic replacement ──────────────────────────────────────────────────

    [Fact]
    public void StrReplace_SingleLine_ReplacesCorrectly()
    {
        var input = "hello world";
        var result = ApplyPatch.StrReplace(input, "world", "earth");
        Assert.Equal("hello earth", result);
    }

    [Fact]
    public void StrReplace_MultiLine_OldStr_ReplacesBlock()
    {
        var input = "line one\nline two\nline three";
        var result = ApplyPatch.StrReplace(input, "line one\nline two", "LINE ONE\nLINE TWO");
        Assert.Equal("LINE ONE\nLINE TWO\nline three", result);
    }

    [Fact]
    public void StrReplace_EmptyNewStr_DeletesMatch()
    {
        var input = "aaa\nbbb\nccc";
        var result = ApplyPatch.StrReplace(input, "bbb\n", "");
        Assert.Equal("aaa\nccc", result);
    }

    [Fact]
    public void StrReplace_WholeFile_ReplacesEntireContent()
    {
        var input = "only line";
        var result = ApplyPatch.StrReplace(input, "only line", "replaced");
        Assert.Equal("replaced", result);
    }

    [Fact]
    public void StrReplace_MatchAtStart_ReplacesCorrectly()
    {
        var input = "first\nsecond\nthird";
        var result = ApplyPatch.StrReplace(input, "first", "FIRST");
        Assert.Equal("FIRST\nsecond\nthird", result);
    }

    [Fact]
    public void StrReplace_MatchAtEnd_ReplacesCorrectly()
    {
        var input = "first\nsecond\nthird";
        var result = ApplyPatch.StrReplace(input, "third", "THIRD");
        Assert.Equal("first\nsecond\nTHIRD", result);
    }

    // ── Fuzzy matching ─────────────────────────────────────────────────────

    [Fact]
    public void StrReplace_Fuzzy_TrailingWhitespace_Matches()
    {
        // Input has trailing spaces, oldStr does not
        var input = "aaa\nbbb   \nccc";
        var result = ApplyPatch.StrReplace(input, "bbb", "BBB");
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void StrReplace_Fuzzy_FullTrim_Matches()
    {
        // Input has leading + trailing whitespace, oldStr does not
        var input = "aaa\n   bbb   \nccc";
        var result = ApplyPatch.StrReplace(input, "bbb", "BBB");
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void StrReplace_Fuzzy_SmartQuotes_Matches()
    {
        // Input has ASCII quotes, oldStr has smart quotes
        var input = "he said \"hello\"";
        var result = ApplyPatch.StrReplace(input, "he said \u201Chello\u201D", "he said \"goodbye\"");
        Assert.Equal("he said \"goodbye\"", result);
    }

    // ── allowMulti = false (default) ───────────────────────────────────────

    [Fact]
    public void StrReplace_AllowMultiFalse_SingleMatch_Succeeds()
    {
        var input = "aaa\nbbb\nccc";
        var result = ApplyPatch.StrReplace(input, "bbb", "BBB", allowMulti: false);
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void StrReplace_AllowMultiFalse_MultipleMatches_Throws()
    {
        var input = "aaa\nbbb\naaa\nbbb";
        var ex = Assert.Throws<PatchApplyException>(
            () => ApplyPatch.StrReplace(input, "bbb", "BBB", allowMulti: false));
        Assert.Contains("2", ex.Message);
    }

    // ── allowMulti = true ──────────────────────────────────────────────────

    [Fact]
    public void StrReplace_AllowMultiTrue_ReplacesAllOccurrences()
    {
        var input = "aaa\nbbb\naaa\nbbb";
        var result = ApplyPatch.StrReplace(input, "bbb", "BBB", allowMulti: true);
        Assert.Equal("aaa\nBBB\naaa\nBBB", result);
    }

    [Fact]
    public void StrReplace_AllowMultiTrue_ThreeOccurrences_ReplacesAll()
    {
        var input = "x\ny\nx\ny\nx\ny";
        var result = ApplyPatch.StrReplace(input, "x", "X", allowMulti: true);
        Assert.Equal("X\ny\nX\ny\nX\ny", result);
    }

    // ── Not found ─────────────────────────────────────────────────────────

    [Fact]
    public void StrReplace_NotFound_ThrowsPatchApplyException()
    {
        var input = "hello world";
        var ex = Assert.Throws<PatchApplyException>(
            () => ApplyPatch.StrReplace(input, "missing text", "anything"));
        Assert.Contains("not found", ex.Message);
    }

    // ── Newline preservation ───────────────────────────────────────────────

    [Fact]
    public void StrReplace_CrlfInput_PreservesCrlf()
    {
        var input = "line one\r\nline two\r\nline three";
        var result = ApplyPatch.StrReplace(input, "line two", "LINE TWO");
        Assert.Equal("line one\r\nLINE TWO\r\nline three", result);
    }

    // ── useRegex = true ───────────────────────────────────────────────────

    [Fact]
    public void StrReplace_Regex_SimplePattern_Matches()
    {
        var input = "foo 123 bar";
        var result = ApplyPatch.StrReplace(input, @"\d+", "NUM", useRegex: true);
        Assert.Equal("foo NUM bar", result);
    }

    [Fact]
    public void StrReplace_Regex_NewStrIsLiteral_NoSubstitution()
    {
        // $1 in newStr must be treated as literal text, not capture group
        var input = "hello world";
        var result = ApplyPatch.StrReplace(input, @"(hello)", "$1", useRegex: true);
        Assert.Equal("$1 world", result);
    }

    [Fact]
    public void StrReplace_Regex_MultipleMatches_AllowMultiFalse_Throws()
    {
        var input = "cat and cat";
        var ex = Assert.Throws<PatchApplyException>(
            () => ApplyPatch.StrReplace(input, "cat", "dog", useRegex: true, allowMulti: false));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void StrReplace_Regex_MultipleMatches_AllowMultiTrue_ReplacesAll()
    {
        var input = "cat and cat";
        var result = ApplyPatch.StrReplace(input, "cat", "dog", useRegex: true, allowMulti: true);
        Assert.Equal("dog and dog", result);
    }

    [Fact]
    public void StrReplace_Regex_InvalidPattern_ThrowsPatchApplyException()
    {
        var ex = Assert.Throws<PatchApplyException>(
            () => ApplyPatch.StrReplace("input", "[invalid", "x", useRegex: true));
        Assert.Contains("Invalid regex pattern", ex.Message);
    }

    [Fact]
    public void StrReplace_Regex_CrlfInput_PreservesCrlf()
    {
        var input = "line one\r\nline two\r\nline three";
        var result = ApplyPatch.StrReplace(input, @"line \w+", "LINE", useRegex: true, allowMulti: true);
        Assert.Equal("LINE\r\nLINE\r\nLINE", result);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail (method doesn't exist yet)**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
dotnet test --filter "StrReplaceTests"
```

Expected: Build error — `ApplyPatch` does not contain a definition for `StrReplace`.

---

## Task 3: Implement StrReplaceParser (Fuzzy Path) + Public StrReplace Method

**Files:**
- Create: `src/ApplyPatch/Internal/StrReplaceParser.cs`
- Modify: `src/ApplyPatch/ApplyPatch.cs`

- [ ] **Step 1: Create StrReplaceParser**

```csharp
// src/ApplyPatch/Internal/StrReplaceParser.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ApplyPatch.Internal;

internal static class StrReplaceParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Find all fuzzy matches of <paramref name="oldLines"/> in <paramref name="inputLines"/>.
    /// Calls ContextMatcher.FindContext in a loop, advancing past each match.
    /// </summary>
    public static List<ContextMatch> FindFuzzyMatches(List<string> inputLines, List<string> oldLines)
    {
        var results = new List<ContextMatch>();
        int start = 0;

        while (true)
        {
            var match = ContextMatcher.FindContext(inputLines, oldLines, start, eof: false);
            if (match.NewIndex == -1) break;
            results.Add(match);
            start = match.NewIndex + oldLines.Count;
            // String.Split always returns >= 1 element, so oldLines.Count >= 1 and
            // start always advances — no infinite-loop risk even for empty oldStr.
        }

        return results;
    }

    /// <summary>
    /// Find all regex matches of <paramref name="pattern"/> in <paramref name="normalizedInput"/>.
    /// Returns (StartChar, Length) for each match.
    /// Throws <see cref="PatchApplyException"/> on invalid pattern or timeout.
    /// </summary>
    public static List<(int Start, int Length)> FindRegexMatches(string normalizedInput, string pattern)
    {
        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.None, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new PatchApplyException($"Invalid regex pattern: {ex.Message}", ex);
        }

        MatchCollection matches;
        try
        {
            matches = regex.Matches(normalizedInput);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new PatchApplyException("Regex match timed out after 5 seconds", ex);
        }

        var result = new List<(int, int)>(matches.Count);
        foreach (Match m in matches)
            result.Add((m.Index, m.Length));
        return result;
    }
}
```

- [ ] **Step 2: Add StrReplace to ApplyPatch.cs**

Replace the content of `src/ApplyPatch/ApplyPatch.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ApplyPatch.Internal;

namespace ApplyPatch;

public static class ApplyPatch
{
    /// <summary>
    /// Apply a V4A diff to create new file content.
    /// All diff lines must be prefixed with "+".
    /// </summary>
    public static string Create(string diff)
    {
        var newline = NewlineHelper.DetectNewline("", diff, isCreateMode: true);
        var diffLines = DiffParser.NormalizeDiffLines(diff);
        return DiffParser.ParseCreateDiff(diffLines, newline);
    }

    /// <summary>
    /// Apply a V4A diff to update existing text content.
    /// </summary>
    public static string Apply(string input, string diff)
    {
        var newline = NewlineHelper.DetectNewline(input, diff, isCreateMode: false);
        var diffLines = DiffParser.NormalizeDiffLines(diff);
        var normalizedInput = NewlineHelper.NormalizeToLf(input);
        var parsed = DiffParser.ParseUpdateDiff(diffLines, normalizedInput);
        return ChunkApplier.Apply(normalizedInput, parsed.Chunks, newline);
    }

    /// <summary>
    /// Apply an Anthropic-style str_replace operation to existing text content.
    /// </summary>
    /// <param name="input">The original text content.</param>
    /// <param name="oldStr">The text to find. Matched using 4-tier fuzzy matching when <paramref name="useRegex"/> is false.</param>
    /// <param name="newStr">The replacement text. Always treated as a literal string (no capture group substitution).</param>
    /// <param name="allowMulti">When false (default), throws if <paramref name="oldStr"/> matches more than once. When true, replaces all occurrences.</param>
    /// <param name="useRegex">When true, <paramref name="oldStr"/> is a .NET regex pattern. Fuzzy matching is skipped.</param>
    /// <exception cref="PatchApplyException">Thrown when oldStr is not found, matches multiple times without allowMulti, or the regex is invalid/times out.</exception>
    public static string StrReplace(
        string input,
        string oldStr,
        string newStr,
        bool allowMulti = false,
        bool useRegex = false)
    {
        var newline = NewlineHelper.DetectNewlineFromText(input);
        var normalizedInput = NewlineHelper.NormalizeToLf(input);
        var normalizedNewStr = NewlineHelper.NormalizeToLf(newStr);

        return useRegex
            ? ApplyRegexReplace(normalizedInput, oldStr, normalizedNewStr, allowMulti, newline)
            : ApplyFuzzyReplace(normalizedInput, oldStr, normalizedNewStr, allowMulti, newline);
    }

    private static string ApplyFuzzyReplace(
        string normalizedInput, string oldStr, string normalizedNewStr,
        bool allowMulti, string newline)
    {
        var normalizedOldStr = NewlineHelper.NormalizeToLf(oldStr);
        var inputLines = new List<string>(normalizedInput.Split('\n'));
        var oldLines = new List<string>(normalizedOldStr.Split('\n'));

        var matches = StrReplaceParser.FindFuzzyMatches(inputLines, oldLines);

        if (matches.Count == 0)
            throw new PatchApplyException("old_str not found in input");

        if (matches.Count > 1 && !allowMulti)
            throw new PatchApplyException(
                $"old_str found {matches.Count} times; set allowMulti = true to replace all",
                fuzz: matches.Min(m => m.Fuzz));

        var insLines = string.IsNullOrEmpty(normalizedNewStr)
            ? new List<string>()
            : new List<string>(normalizedNewStr.Split('\n'));

        // Build chunks — ChunkApplier applies them in reverse order
        var chunks = new List<Chunk>();
        foreach (var match in matches)
        {
            var delLines = inputLines.GetRange(match.NewIndex, oldLines.Count);
            chunks.Add(new Chunk(match.NewIndex, delLines, new List<string>(insLines)));
        }

        return ChunkApplier.Apply(normalizedInput, chunks, newline);
    }

    private static string ApplyRegexReplace(
        string normalizedInput, string pattern, string normalizedNewStr,
        bool allowMulti, string newline)
    {
        var matches = StrReplaceParser.FindRegexMatches(normalizedInput, pattern);

        if (matches.Count == 0)
            throw new PatchApplyException("old_str not found in input");

        if (matches.Count > 1 && !allowMulti)
            throw new PatchApplyException(
                $"old_str found {matches.Count} times; set allowMulti = true to replace all");

        // Apply replacements in reverse char-offset order to preserve positions
        var sb = new StringBuilder(normalizedInput);
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var (start, length) = matches[i];
            sb.Remove(start, length);
            sb.Insert(start, normalizedNewStr);
        }

        var result = sb.ToString();
        // Restore original newline style (result is LF-normalized at this point)
        return newline == "\r\n" ? result.Replace("\n", "\r\n") : result;
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Run StrReplace tests**

```bash
dotnet test --filter "StrReplaceTests"
```

Expected: All tests pass. If any fail, debug before proceeding.

- [ ] **Step 5: Run all tests to confirm no regressions**

```bash
dotnet test
```

Expected: All tests pass (including original V4A tests).

- [ ] **Step 6: Commit**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
git add src/ApplyPatch/Internal/StrReplaceParser.cs src/ApplyPatch/ApplyPatch.cs tests/ApplyPatch.Tests/StrReplaceTests.cs
git commit -m "feat: add StrReplace with fuzzy matching, allowMulti, and useRegex support"
```

---

## Task 4: Edge Case — Empty newStr Deletion

The `StrReplace_EmptyNewStr_DeletesMatch` test in Task 2 tests `"bbb\n"` as `oldStr`. This multi-line deletion (matching a line plus its trailing newline as part of the next line) needs verification. The fuzzy matcher works on line arrays, so `"bbb\n".Split('\n')` = `["bbb", ""]` — two lines. Verify this matches correctly.

- [ ] **Step 1: Run just the deletion test in isolation**

```bash
dotnet test --filter "StrReplace_EmptyNewStr_DeletesMatch"
```

If this fails, the test expectation may need adjustment. The input is `"aaa\nbbb\nccc"` (lines: `["aaa", "bbb", "ccc"]`). oldStr `"bbb\n"` splits to `["bbb", ""]`. That matches lines 1–2 `["bbb", "ccc"]`? No — `"ccc" != ""`. This means the test as written may not work as expected.

**Correct approach for deletion:** use `"bbb"` as oldStr (just the line, no trailing `\n`) and `""` as newStr. The fuzzy matcher finds line `"bbb"` at index 1, deletes it (1 line), inserts nothing. ChunkApplier will then produce `["aaa", "ccc"]` joined as `"aaa\nccc"`.

- [ ] **Step 2: Fix the deletion test if needed**

If Step 1 fails, update the test in `tests/ApplyPatch.Tests/StrReplaceTests.cs`:

```csharp
[Fact]
public void StrReplace_EmptyNewStr_DeletesMatch()
{
    var input = "aaa\nbbb\nccc";
    var result = ApplyPatch.StrReplace(input, "bbb", "");
    Assert.Equal("aaa\nccc", result);
}
```

- [ ] **Step 3: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 4: Commit if test was fixed**

```bash
git add tests/ApplyPatch.Tests/StrReplaceTests.cs
git commit -m "fix: correct deletion test to use line-only oldStr without trailing newline"
```

---

## Final Verification

- [ ] **Step 1: Full clean build and test**

```bash
cd /c/Users/bhara/OneDrive/Documents/GitHub/ApplyPatchV4A
dotnet build && dotnet test
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 2: Verify public API is complete**

Check that `src/ApplyPatch/ApplyPatch.cs` exposes:
- `Create(string diff)` — existing
- `Apply(string input, string diff)` — existing
- `StrReplace(string input, string oldStr, string newStr, bool allowMulti = false, bool useRegex = false)` — new

- [ ] **Step 3: Final commit if anything was left uncommitted**

```bash
git status
# Commit any remaining changes
```
