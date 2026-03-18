using Xunit;

namespace PatchSharp.Tests;

public class CreateTests
{
    [Fact]
    public void Create_BasicLines_ReturnsContent()
    {
        var diff = "+line one\n+line two\n+line three";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("line one\nline two\nline three", result);
    }

    [Fact]
    public void Create_WithEndPatch_IgnoresTerminator()
    {
        var diff = "+hello\n+world\n*** End Patch";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void Create_EmptyContent_ReturnsEmpty()
    {
        var diff = "*** End Patch";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("", result);
    }

    [Fact]
    public void Create_InvalidLine_Throws()
    {
        var diff = "+valid\ninvalid line";
        Assert.Throws<PatchApplyException>(() => ApplyPatch.Create(diff));
    }

    [Fact]
    public void Create_CrlfDiff_PreservesCrlf()
    {
        var diff = "+line one\r\n+line two\r\n*** End Patch";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("line one\r\nline two", result);
    }

    [Fact]
    public void Create_SingleLine_Works()
    {
        var diff = "+only line";
        var result = ApplyPatch.Create(diff);
        Assert.Equal("only line", result);
    }
}
