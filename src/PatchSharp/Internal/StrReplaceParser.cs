// src/PatchSharp/Internal/StrReplaceParser.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PatchSharp.Internal;

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
            if (match.IndexMap != null)
                start = match.IndexMap[oldLines.Count - 1] + 1;
            else
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

        var result = new List<(int, int)>();
        try
        {
            var matches = regex.Matches(normalizedInput);
            foreach (Match m in matches)
                result.Add((m.Index, m.Length));
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new PatchApplyException($"Regex match timed out after {(int)RegexTimeout.TotalSeconds} seconds", ex);
        }
        return result;
    }

    /// <summary>
    /// Find all exact substring matches of <paramref name="pattern"/> in <paramref name="input"/>.
    /// Returns (StartChar, Length) for each non-overlapping match.
    /// </summary>
    public static List<(int Start, int Length)> FindSubstringMatches(string input, string pattern)
    {
        var results = new List<(int, int)>();
        if (pattern.Length == 0) return results;
        int start = 0;
        while (true)
        {
            int idx = input.IndexOf(pattern, start, StringComparison.Ordinal);
            if (idx < 0) break;
            results.Add((idx, pattern.Length));
            start = idx + pattern.Length;
        }
        return results;
    }
}
