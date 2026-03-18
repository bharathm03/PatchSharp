using System.Collections.Generic;
using ApplyPatch.Internal;
using Xunit;

namespace ApplyPatch.Tests;

public class FuzzyMatchingTests
{
    [Fact]
    public void FindContext_ExactMatch_ReturnsFuzz0()
    {
        var lines = new List<string> { "aaa", "bbb", "ccc", "ddd" };
        var context = new List<string> { "bbb", "ccc" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(1, result.NewIndex);
        Assert.Equal(0, result.Fuzz);
    }

    [Fact]
    public void FindContext_TrailingWhitespace_ReturnsFuzz1()
    {
        var lines = new List<string> { "aaa", "bbb  ", "ccc\t", "ddd" };
        var context = new List<string> { "bbb", "ccc" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(1, result.NewIndex);
        Assert.Equal(1, result.Fuzz);
    }

    [Fact]
    public void FindContext_FullWhitespace_ReturnsFuzz100()
    {
        var lines = new List<string> { "aaa", "  bbb  ", "\tccc\t", "ddd" };
        var context = new List<string> { "bbb", "ccc" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(1, result.NewIndex);
        Assert.Equal(100, result.Fuzz);
    }

    [Fact]
    public void FindContext_NoMatch_ReturnsNeg1()
    {
        var lines = new List<string> { "aaa", "bbb", "ccc" };
        var context = new List<string> { "xxx", "yyy" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(-1, result.NewIndex);
    }

    [Fact]
    public void FindContext_EmptyContext_ReturnsStart()
    {
        var lines = new List<string> { "aaa", "bbb" };
        var context = new List<string>();
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(0, result.NewIndex);
        Assert.Equal(0, result.Fuzz);
    }

    [Fact]
    public void FindContext_Eof_SearchesFromEnd()
    {
        var lines = new List<string> { "aaa", "bbb", "ccc", "bbb", "ccc" };
        var context = new List<string> { "bbb", "ccc" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: true);
        Assert.Equal(3, result.NewIndex);
        Assert.Equal(0, result.Fuzz);
    }

    [Fact]
    public void FindContext_Eof_FallbackAdds10000()
    {
        var lines = new List<string> { "bbb", "ccc", "ddd", "eee", "fff" };
        var context = new List<string> { "bbb", "ccc" };
        var result = ContextMatcher.FindContext(lines, context, 0, eof: true);
        Assert.Equal(0, result.NewIndex);
        Assert.True(result.Fuzz >= 10000);
    }

    [Fact]
    public void FindContext_RespectsStartOffset()
    {
        var lines = new List<string> { "aaa", "bbb", "aaa", "bbb" };
        var context = new List<string> { "aaa", "bbb" };
        var result = ContextMatcher.FindContext(lines, context, 2, eof: false);
        Assert.Equal(2, result.NewIndex);
    }

    [Fact]
    public void FindContext_UnicodeSmartQuotes_ReturnsFuzz1000()
    {
        // File has ASCII quotes, context has smart quotes (or vice versa)
        var lines = new List<string> { "aaa", "it's a \"test\"", "ccc" };
        var context = new List<string> { "it\u2019s a \u201Ctest\u201D" }; // smart quotes
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(1, result.NewIndex);
        Assert.Equal(1000, result.Fuzz);
    }

    [Fact]
    public void FindContext_UnicodeEmDash_ReturnsFuzz1000()
    {
        var lines = new List<string> { "a - b", "x -- y" };
        var context = new List<string> { "a \u2013 b" }; // en-dash
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(0, result.NewIndex);
        Assert.Equal(1000, result.Fuzz);
    }

    [Fact]
    public void FindContext_NonBreakingSpace_ReturnsFuzz1000()
    {
        var lines = new List<string> { "hello world" };
        var context = new List<string> { "hello\u00A0world" }; // non-breaking space
        var result = ContextMatcher.FindContext(lines, context, 0, eof: false);
        Assert.Equal(0, result.NewIndex);
        Assert.Equal(1000, result.Fuzz);
    }
}
