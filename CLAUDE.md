# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
dotnet build                                          # Build all projects
dotnet test                                           # Run all tests
dotnet test --filter "ClassName.MethodName"            # Run a single test
dotnet test --filter "FuzzyMatchingTests"              # Run all tests in a class
```

## Architecture

C# library that applies OpenAI's V4A patch format diffs to text. Multi-targets `netstandard2.1`, `net8.0`, `net10.0`.

### Public API

Two static methods on `ApplyPatchV4A.ApplyPatch`:
- `Create(string diff)` — builds new file content from a diff where every line is `+`-prefixed
- `Apply(string input, string diff)` — applies a V4A diff to existing text

Throws `PatchApplyException` (with `LineNumber`, `Fuzz`, `Context` properties) on failure.

### Internal Pipeline

```
Input → NewlineHelper (detect/normalize to LF)
      → DiffParser (parse diff into sections, handle anchors and markers)
        → ContextMatcher (locate where each section applies via fuzzy matching)
      → ChunkApplier (apply chunks in reverse order)
      → NewlineHelper (restore original line endings)
      → Output
```

**Key design decisions:**
- **Reverse-order application**: Chunks apply bottom-to-top so earlier indices stay valid.
- **4-tier fuzzy matching**: exact (fuzz=0) → trimEnd (fuzz=1) → trim (fuzz=100) → Unicode normalization (fuzz=1000). Lowest fuzz wins.
- **Unicode normalization** converts smart quotes, em-dashes, and non-breaking spaces to ASCII equivalents for matching.
- **Anchors** (`@@ text`): advance the search cursor to a specific line in the input.
- **`*** End of File`**: searches backward from the end of the file.

### Project Layout

- `src/ApplyPatchV4A/` — library (public: `ApplyPatch`, `PatchApplyException`; internal: `Internal/` folder)
- `tests/ApplyPatchV4A.Tests/` — xUnit tests, one class per feature area
- Tests use `[InternalsVisibleTo]` to access internal types directly

### V4A Diff Format

Lines prefixed with `+` (insert), `-` (delete), or ` ` (context). Special markers:
- `*** Begin Patch` / `*** End Patch` — optional header/footer
- `*** file.txt` — file path header for each section
- `@@ context line` — anchor to jump to a position
- `*** End of File` — anchor to the end of file
