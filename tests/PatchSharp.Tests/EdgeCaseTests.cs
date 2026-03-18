using Xunit;

namespace PatchSharp.Tests;

public class EdgeCaseTests
{
    [Fact]
    public void Apply_EmptyLineInDiff_TreatedAsContext()
    {
        // Empty line in diff normalizes to space (context)
        var input = "aaa\n\nbbb";
        var diff = " aaa\n\n-bbb\n+BBB";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\n\nBBB", result);
    }

    [Fact]
    public void Apply_CrlfInput_LfDiff_PreservesCrlf()
    {
        var input = "aaa\r\nbbb\r\nccc";
        var diff = " aaa\n-bbb\n+BBB\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\r\nBBB\r\nccc", result);
    }

    [Fact]
    public void Apply_LargeFile_MultipleAnchors()
    {
        // Build a 100-line file, modify lines 25 and 75
        var lines = new string[100];
        for (int i = 0; i < 100; i++) lines[i] = $"line {i}";
        var input = string.Join("\n", lines);

        var diff = "@@ line 24\n line 24\n-line 25\n+LINE 25\n line 26\n@@ line 74\n line 74\n-line 75\n+LINE 75\n line 76";
        var result = ApplyPatch.Apply(input, diff);

        Assert.Contains("LINE 25", result);
        Assert.Contains("LINE 75", result);
        Assert.DoesNotContain("line 25\n", result);
        Assert.DoesNotContain("line 75\n", result);
    }

    [Fact]
    public void Apply_ConsecutiveAdditions()
    {
        var input = "aaa\nbbb";
        var diff = " aaa\n+one\n+two\n+three\n bbb";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\none\ntwo\nthree\nbbb", result);
    }

    [Fact]
    public void Apply_ConsecutiveDeletions()
    {
        var input = "aaa\nb1\nb2\nb3\nccc";
        var diff = " aaa\n-b1\n-b2\n-b3\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nccc", result);
    }

    [Fact]
    public void Apply_ReplaceMultipleWithMultiple()
    {
        var input = "aaa\nold1\nold2\nbbb";
        var diff = " aaa\n-old1\n-old2\n+new1\n+new2\n+new3\n bbb";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nnew1\nnew2\nnew3\nbbb", result);
    }

    [Fact]
    public void Apply_UnequalChunkSizes_IndexesCorrect()
    {
        // First chunk inserts more lines than it deletes, second chunk must still work
        var input = "a\nb\nc\nd\ne";
        var diff = " a\n-b\n+B1\n+B2\n+B3\n c\n d\n-e\n+E";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("a\nB1\nB2\nB3\nc\nd\nE", result);
    }

    // --- Bug fixes: codex-cli compliance ---

    [Fact]
    public void Apply_PureAddition_InsertsAtEOF()
    {
        // Pure addition hunk (only + lines, no context) must insert at end of file
        var input = "line1\nline2";
        var diff = "@@\n+line3\n+line4";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line1\nline2\nline3\nline4", result);
    }

    [Fact]
    public void Apply_PureAddition_FollowedByRemoval_BothApply()
    {
        // Pure addition at EOF must not advance cursor past original content
        var input = "line1\nline2\nline3";
        var diff = "@@\n+after-context\n+second-line\n@@\n line1\n-line2\n-line3\n+line2-replacement";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line1\nline2-replacement\nafter-context\nsecond-line", result);
    }

    [Fact]
    public void Apply_BlankLinesBetweenHunks_Skipped()
    {
        // Blank lines between @@ sections are formatting artifacts, not context
        var input = "aaa\nbbb\nccc\nddd";
        var diff = "@@\n-aaa\n+AAA\n\n@@\n-ccc\n+CCC";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("AAA\nbbb\nCCC\nddd", result);
    }

    [Fact]
    public void Apply_UnprefixedNonEmptyLine_BreaksOutOfHunk()
    {
        // Unprefixed non-empty line after parsed lines should break hunk, not throw
        var input = "aaa\nbbb\nccc";
        var diff = "@@\n aaa\n-bbb\n+BBB\nGARBAGE\n@@\n-ccc\n+CCC";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nCCC", result);
    }

    [Theory]
    [InlineData("\u00A0", " ")]   // non-breaking space
    [InlineData("\u2002", " ")]   // en space
    [InlineData("\u2003", " ")]   // em space
    [InlineData("\u3000", " ")]   // ideographic space
    public void Apply_UnicodeNormalization_MatchesDuringContextFind(string unicodeChar, string asciiChar)
    {
        // Unicode chars in file must fuzzy-match ASCII chars in diff context
        var input = $"x{unicodeChar}y\nline2";
        var diff = $"@@\n-x{asciiChar}y\n+replaced\n line2";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("replaced", result);
    }

    [Fact]
    public void Apply_AnchorWithUnicodeNormalization_AdvancesCursor()
    {
        // Anchor text with unicode spaces in file must match ASCII anchor in diff
        var input = "first\nsecond\u2003line\nthird";
        var diff = "@@ second line\n second line\n-third\n+THIRD";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("first\nsecond\u2003line\nTHIRD", result);
    }

    [Fact]
    public void Apply_TrailingEmptyContextLines_Stripped()
    {
        // Trailing empty lines in context should be stripped as inter-chunk separators
        var input = "aaa\nbbb\nccc";
        var diff = "@@\n aaa\n-bbb\n+BBB\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void Apply_MultipleEndChunks_InsertInOrder()
    {
        // Multiple pure-addition hunks should all insert at EOF in order
        var input = "line1\nline2\n";
        var diff = "@@\n+added1\n@@\n+added2";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line1\nline2\nadded1\nadded2\n", result);
    }

    // --- Codex-cli compatibility verification tests ---

    [Fact]
    public void CodexCompat_TrailingNewline_InputWithTrailingNewline_PreservesIt()
    {
        // Codex-cli: always ensures output ends with \n
        // File with trailing newline should keep it after edits
        var input = "aaa\nbbb\nccc\n";
        var diff = "@@\n-bbb\n+BBB";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc\n", result);
    }

    [Fact]
    public void CodexCompat_TrailingNewline_InputWithoutTrailingNewline_PreservesAbsence()
    {
        // Codex-cli enforces trailing \n at the file-write layer, not in the diff
        // algorithm. PatchSharp is a string library, so it preserves the original
        // input's trailing newline state: no trailing \n in → no trailing \n out.
        var input = "aaa\nbbb\nccc";
        var diff = "@@\n-bbb\n+BBB";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void CodexCompat_EofSentinelRetry_PatternEndsWithEmptyLine()
    {
        // Codex-cli: if seek_sequence fails and pattern ends with empty string
        // (trailing newline sentinel), retry without it.
        // This simulates a file ending with newline where the diff context
        // includes the trailing empty line from split.
        var input = "line1\nline2\nline3\n";
        // Diff context includes trailing empty line that came from splitting
        // a file that ends with \n — the empty line is a sentinel, not real content
        var diff = "@@ line2\n line2\n-line3\n+LINE3\n ";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("LINE3", result);
    }

    [Fact]
    public void CodexCompat_EofSentinelRetry_ContextEndsWithEmptyLine()
    {
        // Context matching with anchor and deletion at trailing newline position.
        // Input "first\nsecond\n" splits to ["first", "second", ""].
        // The diff deletes the empty trailing line (sentinel) and inserts "last".
        var input = "first\nsecond\n";
        var diff = "@@ second\n second\n-\n+last";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("last", result);
    }

    [Fact]
    public void CodexCompat_EofSentinelRetry_OldLinesEndWithEmpty()
    {
        // Codex-cli: when old_lines pattern ends with empty string (trailing
        // newline sentinel) and seek fails, retry without that trailing empty.
        // Input has no trailing newline, but diff's del lines include one.
        var input = "first\nsecond";
        // The diff deletes "second" and an empty line (sentinel from a file
        // that originally had trailing \n). Should still match "second" at EOF
        // even though the empty sentinel line doesn't exist in input.
        var diff = "@@ second\n-second\n-\n+replacement";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("replacement", result);
    }

    [Fact]
    public void CodexCompat_NonContiguousChunks_SameSection()
    {
        // Codex-cli: each chunk independently seeks its old_lines.
        // PatchSharp: chunks in a section share contiguous context.
        // This tests if PatchSharp handles non-adjacent changes in separate sections.
        var input = "aaa\nbbb\nccc\nddd\neee\nfff";
        var diff = "@@\n aaa\n-bbb\n+BBB\n ccc\n@@\n eee\n-fff\n+FFF";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc\nddd\neee\nFFF", result);
    }

    [Fact]
    public void CodexCompat_ChunksSeparatedByUnrelatedLines_SingleSection()
    {
        // In codex-cli, chunks seek independently so they can skip unrelated lines.
        // In PatchSharp, a single section's context must be contiguous.
        // This tests two changes in one section with lines between them.
        var input = "aaa\nbbb\nccc\nddd\neee";
        var diff = "@@\n aaa\n-bbb\n+BBB\n ccc\n ddd\n-eee\n+EEE";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc\nddd\nEEE", result);
    }

    [Fact]
    public void CodexCompat_UnicodeNormalization_DashVariants()
    {
        // Codex-cli normalizes: \u2010-\u2015, \u2212 → '-'
        // PatchSharp normalizes: \u2013, \u2014, \u2212 → '-'
        // Test the dash variants codex-cli supports that PatchSharp may not
        var input = "a\u2010b\nline2";  // HYPHEN (\u2010)
        var diff = "@@\n-a-b\n+replaced\n line2";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("replaced", result);
    }

    [Fact]
    public void CodexCompat_UnicodeNormalization_QuoteVariant_201B()
    {
        // Codex-cli normalizes \u201B (SINGLE HIGH-REVERSED-9 QUOTATION MARK) → '
        // PatchSharp may not include this variant
        var input = "it\u201Bs\nline2";
        var diff = "@@\n-it's\n+replaced\n line2";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Contains("replaced", result);
    }
}
