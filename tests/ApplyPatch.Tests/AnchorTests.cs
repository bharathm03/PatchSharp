using Xunit;

namespace ApplyPatch.Tests;

public class AnchorTests
{
    [Fact]
    public void Apply_WithAnchor_JumpsToCorrectPosition()
    {
        var input = "header\nfunction foo() {\n  old\n}\nfunction bar() {\n  code\n}";
        var diff = "@@ function bar() {\n function bar() {\n-  code\n+  new code\n }";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("header\nfunction foo() {\n  old\n}\nfunction bar() {\n  new code\n}", result);
    }

    [Fact]
    public void Apply_BareAnchor_WorksWithoutText()
    {
        var input = "aaa\nbbb\nccc";
        var diff = "@@\n aaa\n-bbb\n+BBB\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc", result);
    }

    [Fact]
    public void Apply_MultipleAnchors()
    {
        var input = "a\nb\nc\nd\ne\nf";
        var diff = "@@ b\n b\n-c\n+C\n d\n@@ e\n e\n-f\n+F";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("a\nb\nC\nd\ne\nF", result);
    }

    [Fact]
    public void Apply_AnchorWithWhitespace_FuzzyMatch()
    {
        var input = "header\n  function foo()  \n  body\nfooter";
        var diff = "@@ function foo()\n   function foo()  \n-  body\n+  new body\n footer";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("header\n  function foo()  \n  new body\nfooter", result);
    }
}
