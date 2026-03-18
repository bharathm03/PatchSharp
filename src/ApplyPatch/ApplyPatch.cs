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
        {
            // Line-level matching failed — try exact character-level substring matching.
            // Fuzzy tiers are already handled by ContextMatcher at line level above.
            var charMatches = StrReplaceParser.FindSubstringMatches(normalizedInput, normalizedOldStr);
            return ApplyCharOffsetReplacements(normalizedInput, charMatches, normalizedNewStr, allowMulti, newline);
        }

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
            chunks.Add(new Chunk(match.NewIndex, delLines, insLines));
        }

        return ChunkApplier.Apply(normalizedInput, chunks, newline);
    }

    private static string ApplyRegexReplace(
        string normalizedInput, string pattern, string normalizedNewStr,
        bool allowMulti, string newline)
    {
        var matches = StrReplaceParser.FindRegexMatches(normalizedInput, pattern);
        return ApplyCharOffsetReplacements(normalizedInput, matches, normalizedNewStr, allowMulti, newline);
    }

    /// <summary>
    /// Validates match count, then applies character-offset replacements in reverse order.
    /// Shared by the regex and substring fallback paths.
    /// </summary>
    private static string ApplyCharOffsetReplacements(
        string normalizedInput, List<(int Start, int Length)> matches,
        string normalizedNewStr, bool allowMulti, string newline)
    {
        if (matches.Count == 0)
            throw new PatchApplyException("old_str not found in input");

        if (matches.Count > 1 && !allowMulti)
            throw new PatchApplyException(
                $"old_str found {matches.Count} times; set allowMulti = true to replace all");

        var sb = new StringBuilder(normalizedInput);
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var (start, length) = matches[i];
            sb.Remove(start, length);
            sb.Insert(start, normalizedNewStr);
        }

        var result = sb.ToString();
        return newline == "\r\n" ? result.Replace("\n", "\r\n") : result;
    }
}
