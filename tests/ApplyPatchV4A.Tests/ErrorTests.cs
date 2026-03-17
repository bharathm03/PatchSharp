using Xunit;

namespace ApplyPatchV4A.Tests;

public class ErrorTests
{
    [Fact]
    public void Apply_InvalidPrefix_Throws()
    {
        var input = "aaa\nbbb";
        var diff = " aaa\nXinvalid line\n bbb";
        Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
    }

    [Fact]
    public void Apply_ContextNotFound_Throws()
    {
        var input = "aaa\nbbb\nccc";
        var diff = " xxx\n-yyy\n+zzz";
        var ex = Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
        Assert.Contains("Context", ex.Message);
    }

    [Fact]
    public void Apply_InvalidStarLine_Throws()
    {
        var input = "aaa";
        var diff = " aaa\n*** Something Invalid";
        Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
    }

    [Fact]
    public void Create_MissingPlusPrefix_Throws()
    {
        var diff = "+valid\nmissing plus";
        var ex = Assert.Throws<PatchApplyException>(() => ApplyPatch.Create(diff));
        Assert.Contains("Invalid Add File Line", ex.Message);
    }

    [Fact]
    public void Apply_ContextNotFound_ExceptionHasProperties()
    {
        var input = "aaa\nbbb\nccc";
        var diff = " xxx\n-yyy\n+zzz";
        var ex = Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
        Assert.Contains("Context", ex.Message);
        Assert.NotNull(ex.Context);
    }

    [Fact]
    public void Apply_AnchorAlreadySeenBeforeCursor_SkipsSearch()
    {
        var input = "marker\naaa\nbbb\nccc\nddd";
        var diff = " marker\n-aaa\n+AAA\n bbb\n@@ marker\n ccc\n-ddd\n+DDD";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("marker\nAAA\nbbb\nccc\nDDD", result);
    }

    [Fact]
    public void Apply_EmptySectionBetweenAnchors_Throws()
    {
        var input = "aaa\nbbb\nccc";
        var diff = "@@ aaa\n@@ bbb\n bbb\n-ccc\n+CCC";
        Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
    }
}
