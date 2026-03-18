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
