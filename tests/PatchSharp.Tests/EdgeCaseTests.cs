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
}
