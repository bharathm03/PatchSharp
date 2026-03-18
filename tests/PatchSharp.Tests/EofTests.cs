using Xunit;

namespace PatchSharp.Tests;

public class EofTests
{
    [Fact]
    public void Apply_EndOfFile_MatchesAtEnd()
    {
        var input = "first\nsecond\nthird\nfourth";
        var diff = " third\n-fourth\n+FOURTH\n*** End of File";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("first\nsecond\nthird\nFOURTH", result);
    }

    [Fact]
    public void Apply_EndOfFile_AppendLines()
    {
        var input = "first\nsecond\nthird";
        var diff = " third\n+fourth\n*** End of File";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("first\nsecond\nthird\nfourth", result);
    }

    [Fact]
    public void Apply_EndOfFile_WithDuplicateContext()
    {
        var input = "third\nsecond\nthird\nfourth";
        var diff = " third\n-fourth\n+FOURTH\n*** End of File";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("third\nsecond\nthird\nFOURTH", result);
    }
}
