namespace PatchSharp.Tests;

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

    // ── CRLF multiline matching (ported from FileEditorToolTests) ─────────

    [Fact]
    public void StrReplace_CrlfCode_LfOldStr_ReplacesCleanly()
    {
        var input = "<div>\r\n  content\r\n</div>\r\n";
        var result = ApplyPatch.StrReplace(input, "<div>\n  content\n</div>\n", "<span>replaced</span>\n");
        Assert.Equal("<span>replaced</span>\r\n", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_LfOldStr_NoDoubleClosingTags()
    {
        var input = "prefix\r\n<div class=\"outer\">\r\n  <p>content</p>\r\n</div>\r\nsuffix";
        var result = ApplyPatch.StrReplace(input,
            "<div class=\"outer\">\n  <p>content</p>\n</div>\n",
            "<div class=\"new\">\n  <p>new content</p>\n</div>\n");
        Assert.Equal("prefix\r\n<div class=\"new\">\r\n  <p>new content</p>\r\n</div>\r\nsuffix", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_PreservesTrailingContent()
    {
        var input = "<div>\r\n  content\r\n</div>\r\n<footer>end</footer>";
        var result = ApplyPatch.StrReplace(input,
            "<div>\n  content\n</div>\n",
            "<section>new</section>\n");
        Assert.Equal("<section>new</section>\r\n<footer>end</footer>", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_NoTrailingNewline_ReplacesExactly()
    {
        var input = "<div>\r\n  content\r\n</div>";
        var result = ApplyPatch.StrReplace(input, "<div>\n  content\n</div>", "<span>replaced</span>");
        Assert.Equal("<span>replaced</span>", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_NestedElements_NoOrphanedTags()
    {
        var input = "<div>\r\n  <ul>\r\n    <li>item1</li>\r\n    <li>item2</li>\r\n  </ul>\r\n</div>\r\nmore content";
        var result = ApplyPatch.StrReplace(input,
            "<div>\n  <ul>\n    <li>item1</li>\n    <li>item2</li>\n  </ul>\n</div>\n",
            "<section>\n  <p>simplified</p>\n</section>\n");
        Assert.Equal("<section>\r\n  <p>simplified</p>\r\n</section>\r\nmore content", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_CSharpBraces_NoOrphanedBraces()
    {
        var input = "public void Method()\r\n{\r\n    Console.WriteLine();\r\n}\r\n\r\npublic void Other() { }";
        var result = ApplyPatch.StrReplace(input,
            "public void Method()\n{\n    Console.WriteLine();\n}\n",
            "public void Method()\n{\n    Debug.Log();\n}\n");
        Assert.Equal("public void Method()\r\n{\r\n    Debug.Log();\r\n}\r\n\r\npublic void Other() { }", result);
    }

    [Fact]
    public void StrReplace_MixedLineEndings_HandlesCorrectly()
    {
        var input = "<div>\r\n  line1\n  line2\r\n</div>\n";
        var result = ApplyPatch.StrReplace(input,
            "<div>\n  line1\n  line2\n</div>\n",
            "<span>replaced</span>\n");
        Assert.Equal("<span>replaced</span>\r\n", result);
    }

    [Fact]
    public void StrReplace_CrlfCode_AllowMulti_ReplacesAllOccurrences()
    {
        var input = "header\r\n<p>old</p>\r\nmiddle\r\n<p>old</p>\r\nfooter";
        var result = ApplyPatch.StrReplace(input, "<p>old</p>", "<p>new</p>", allowMulti: true);
        Assert.Equal("header\r\n<p>new</p>\r\nmiddle\r\n<p>new</p>\r\nfooter", result);
    }

    // ── Fuzzy indentation matching (ported from FileEditorToolTests) ──────

    [Fact]
    public void StrReplace_Fuzzy_TabsVsSpaces_Matches()
    {
        var input = "\tLine1\n\tLine2\n";
        var result = ApplyPatch.StrReplace(input, "    Line1\n    Line2", "A\nB");
        Assert.Equal("A\nB\n", result);
    }

    [Fact]
    public void StrReplace_Fuzzy_MultilineLeadingSpaces_Matches()
    {
        var input = "    Line1\n    Line2\n";
        var result = ApplyPatch.StrReplace(input, "Line1\nLine2", "A\nB");
        Assert.Equal("A\nB\n", result);
    }

    [Fact]
    public void StrReplace_Fuzzy_MultilineTrailingSpaces_Matches()
    {
        var input = "Line1\nLine2\nLine3";
        var result = ApplyPatch.StrReplace(input, "Line1  \nLine2  \nLine3  ", "A\nB\nC");
        Assert.Equal("A\nB\nC", result);
    }
}
