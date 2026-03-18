using System;
using System.Collections.Generic;
using System.Text;

namespace ApplyPatch.Internal;

internal static class ContextMatcher
{
    public static ContextMatch FindContext(List<string> lines, List<string> context, int start, bool eof)
    {
        if (eof)
        {
            int endStart = Math.Max(0, lines.Count - context.Count);
            var endMatch = FindContextCore(lines, context, endStart);
            if (endMatch.NewIndex != -1)
                return endMatch;
            var fallback = FindContextCore(lines, context, start);
            return new ContextMatch(fallback.NewIndex, fallback.Fuzz + 10000);
        }
        return FindContextCore(lines, context, start);
    }

    public static int AdvanceCursorToAnchor(string anchor, List<string> inputLines, int cursor, ref int fuzz)
    {
        // Try exact match first, then trimmed match
        if (TryFindAnchor(inputLines, cursor, anchor, s => s, out int exactPos))
        {
            return exactPos;
        }

        string trimmedAnchor = anchor.Trim();
        if (TryFindAnchor(inputLines, cursor, trimmedAnchor, s => s.Trim(), out int trimmedPos))
        {
            fuzz += 1;
            return trimmedPos;
        }

        return cursor;
    }

    private static bool TryFindAnchor(List<string> lines, int cursor, string target, Func<string, string> normalize, out int position)
    {
        position = cursor;

        // Check if anchor was already seen before cursor
        for (int i = 0; i < cursor; i++)
        {
            if (normalize(lines[i]) == target) return false;
        }

        // Search forward from cursor
        for (int i = cursor; i < lines.Count; i++)
        {
            if (normalize(lines[i]) == target)
            {
                position = i;
                return true;
            }
        }

        return false;
    }

    private static ContextMatch FindContextCore(List<string> lines, List<string> context, int start)
    {
        if (context.Count == 0)
            return new ContextMatch(start, 0);

        // Tier 1: exact
        for (int i = start; i < lines.Count; i++)
        {
            if (EqualsSlice(lines, context, i, s => s))
                return new ContextMatch(i, 0);
        }

        // Tier 2: trimEnd
        for (int i = start; i < lines.Count; i++)
        {
            if (EqualsSlice(lines, context, i, s => s.TrimEnd()))
                return new ContextMatch(i, 1);
        }

        // Tier 3: trim
        for (int i = start; i < lines.Count; i++)
        {
            if (EqualsSlice(lines, context, i, s => s.Trim()))
                return new ContextMatch(i, 100);
        }

        // Tier 4: Unicode normalization (smart quotes, dashes, non-breaking spaces)
        for (int i = start; i < lines.Count; i++)
        {
            if (EqualsSlice(lines, context, i, NormalizeUnicode))
                return new ContextMatch(i, 1000);
        }

        return new ContextMatch(-1, 0);
    }

    private static bool EqualsSlice(List<string> source, List<string> target, int start, Func<string, string> mapFn)
    {
        if (start + target.Count > source.Count) return false;
        for (int i = 0; i < target.Count; i++)
        {
            if (mapFn(source[start + i]) != mapFn(target[i]))
                return false;
        }
        return true;
    }

    internal static string NormalizeUnicode(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                // Smart single quotes / apostrophes → '
                case '\u2018': // LEFT SINGLE QUOTATION MARK
                case '\u2019': // RIGHT SINGLE QUOTATION MARK
                case '\u201A': // SINGLE LOW-9 QUOTATION MARK
                case '\u2039': // SINGLE LEFT-POINTING ANGLE QUOTATION MARK
                case '\u203A': // SINGLE RIGHT-POINTING ANGLE QUOTATION MARK
                case '\u0060': // GRAVE ACCENT (backtick often used as quote)
                    sb.Append('\'');
                    break;
                // Smart double quotes → "
                case '\u201C': // LEFT DOUBLE QUOTATION MARK
                case '\u201D': // RIGHT DOUBLE QUOTATION MARK
                case '\u201E': // DOUBLE LOW-9 QUOTATION MARK
                case '\u00AB': // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
                case '\u00BB': // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
                    sb.Append('"');
                    break;
                // Dashes → -
                case '\u2013': // EN DASH
                case '\u2014': // EM DASH
                case '\u2212': // MINUS SIGN
                    sb.Append('-');
                    break;
                // Non-breaking space → space
                case '\u00A0': // NO-BREAK SPACE
                    sb.Append(' ');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
