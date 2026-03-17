using System.Collections.Generic;

namespace ApplyPatchV4A.Internal;

internal sealed class Chunk
{
    public int OrigIndex { get; set; }
    public List<string> DelLines { get; }
    public List<string> InsLines { get; }

    public Chunk(int origIndex, List<string> delLines, List<string> insLines)
    {
        OrigIndex = origIndex;
        DelLines = delLines;
        InsLines = insLines;
    }
}

internal sealed class ParsedDiff
{
    public List<Chunk> Chunks { get; }
    public int Fuzz { get; }

    public ParsedDiff(List<Chunk> chunks, int fuzz)
    {
        Chunks = chunks;
        Fuzz = fuzz;
    }
}

internal readonly struct ContextMatch
{
    public int NewIndex { get; }
    public int Fuzz { get; }

    public ContextMatch(int newIndex, int fuzz)
    {
        NewIndex = newIndex;
        Fuzz = fuzz;
    }
}
