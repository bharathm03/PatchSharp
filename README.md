# PatchSharp

A C# library for applying text diffs and replacements with built-in fuzzy matching. Supports OpenAI's V4A patch format and Anthropic-style `str_replace` operations.

## Installation

```
dotnet add package PatchSharp
```

Targets `netstandard2.1`, `net8.0`, and `net10.0`.

## Usage

All methods are on the static class `PatchSharp.ApplyPatch`.

### Apply a V4A diff

```csharp
using PatchSharp;

var original = "line one\nline two\nline three\n";
var diff = @"*** original.txt
 line one
-line two
+line 2
 line three
";

var result = ApplyPatch.Apply(original, diff);
// "line one\nline 2\nline three\n"
```

### Create a new file from a diff

```csharp
var diff = @"*** new-file.txt
+hello
+world
";

var result = ApplyPatch.Create(diff);
// "hello\nworld\n"
```

### Find and replace with fuzzy matching

```csharp
var input = "function hello() {\n  return 'world';\n}\n";

var result = ApplyPatch.StrReplace(
    input,
    oldStr: "  return 'world';",
    newStr: "  return 'hello world';");
```

Replace all occurrences:

```csharp
var result = ApplyPatch.StrReplace(input, "old", "new", allowMulti: true);
```

Use a regex pattern:

```csharp
var result = ApplyPatch.StrReplace(input, @"v\d+\.\d+", "v2.0", useRegex: true);
```

### Error handling

All methods throw `PatchApplyException` on failure:

```csharp
try
{
    ApplyPatch.Apply(input, diff);
}
catch (PatchApplyException ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.LineNumber); // line in the diff where the error occurred
    Console.WriteLine(ex.Fuzz);      // fuzzy match level used (0 = exact)
    Console.WriteLine(ex.Context);   // surrounding context for diagnostics
}
```

## Fuzzy matching

When an exact match isn't found, PatchSharp tries progressively looser matching:

| Fuzz level | Strategy |
|------------|----------|
| 0 | Exact match |
| 1 | Ignore trailing whitespace |
| 100 | Ignore leading and trailing whitespace |
| 1000 | Unicode normalization (smart quotes → ASCII quotes, em-dashes → hyphens, etc.) |

The lowest fuzz level that produces a match wins.

## V4A diff format

```
*** path/to/file.txt           ← file header
@@ context line                ← anchor (jump to this line)
 unchanged line                ← context (space prefix)
-removed line                  ← deletion
+added line                    ← insertion
*** End of File                ← anchor to end of file
```

Wrapped in optional `*** Begin Patch` / `*** End Patch` markers.

## License

MIT
