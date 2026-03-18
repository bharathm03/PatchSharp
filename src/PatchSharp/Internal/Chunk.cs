using System.Collections.Generic;

namespace PatchSharp.Internal;

internal sealed class Chunk
{
    public int OrigIndex { get; set; }
    public List<string> DelLines { get; }
    public List<string> InsLines { get; }
    /// <summary>
    /// When true, this chunk's insertion point is resolved at apply time
    /// to the current end of the file (after preceding chunks have been applied).
    /// Used for pure-addition hunks per codex-cli V4A spec.
    /// </summary>
    public bool InsertAtEnd { get; set; }

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
