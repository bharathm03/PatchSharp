using Xunit;

namespace PatchSharp.Tests;

public class ApplyBasicTests
{
    [Fact]
    public void Apply_SimpleReplacement()
    {
        var input = "line one\nline two\nline three";
        var diff = " line one\n-line two\n+line TWO\n line three";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line one\nline TWO\nline three", result);
    }

    [Fact]
    public void Apply_InsertLine()
    {
        var input = "aaa\nbbb";
        var diff = " aaa\n+inserted\n bbb";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\ninserted\nbbb", result);
    }

    [Fact]
    public void Apply_DeleteLine()
    {
        var input = "aaa\nbbb\nccc";
        var diff = " aaa\n-bbb\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nccc", result);
    }

    [Fact]
    public void Apply_MultipleChunksInSection()
    {
        var input = "aaa\nbbb\nccc\nddd\neee";
        var diff = " aaa\n-bbb\n+BBB\n ccc\n-ddd\n+DDD\n eee";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\nBBB\nccc\nDDD\neee", result);
    }

    [Fact]
    public void Apply_EmptyDiff_ReturnsInput()
    {
        var input = "hello\nworld";
        var diff = "*** End Patch";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void Apply_EmptyInput_EmptyDiff()
    {
        var input = "";
        var diff = "*** End Patch";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("", result);
    }

    [Fact]
    public void Apply_PreservesCrlf()
    {
        var input = "line one\r\nline two\r\nline three";
        var diff = " line one\n-line two\n+line TWO\n line three";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line one\r\nline TWO\r\nline three", result);
    }

    [Fact]
    public void Apply_BeginPatchHeader_IsStripped()
    {
        var input = "line one\nline two\nline three";
        var diff = "*** Begin Patch\n line one\n-line two\n+line TWO\n line three\n*** End Patch";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line one\nline TWO\nline three", result);
    }

    [Fact]
    public void Create_BeginPatchHeader_IsStripped()
    {
        var diff = "*** Begin Patch\n+hello\n+world\n*** End Patch";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void Apply_SkipsBlankLines_SimpleReplacement()
    {
        // File has blank lines between blocks, diff context omits them
        var input =
            "    [StringLength(100)]\n" +
            "    public string Company { get; set; } = string.Empty;\n" +
            "\n" +
            "    [Required]\n" +
            "    [StringLength(1000, MinimumLength = 20)]\n" +
            "    public string Message { get; set; } = string.Empty;";
        // Diff context omits the blank line between Company and [Required]
        var diff =
            "     [StringLength(100)]\n" +
            "     public string Company { get; set; } = string.Empty;\n" +
            "-    [Required]\n" +
            "+    [Required, MaxLength(50)]\n" +
            "     [StringLength(1000, MinimumLength = 20)]\n" +
            "     public string Message { get; set; } = string.Empty;";
        var result = ApplyPatch.Apply(input, diff);
        var expected =
            "    [StringLength(100)]\n" +
            "    public string Company { get; set; } = string.Empty;\n" +
            "\n" +
            "    [Required, MaxLength(50)]\n" +
            "    [StringLength(1000, MinimumLength = 20)]\n" +
            "    public string Message { get; set; } = string.Empty;";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Apply_SkipsBlankLines_InsertLine()
    {
        // File has blank line, diff context omits it
        var input = "aaa\n\nbbb\nccc";
        var diff = " aaa\n bbb\n+inserted\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\n\nbbb\ninserted\nccc", result);
    }

    [Fact]
    public void Apply_SkipsBlankLines_DeleteLine()
    {
        var input = "aaa\n\nbbb\nccc";
        var diff = " aaa\n-bbb\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa\n\nccc", result);
    }
}
