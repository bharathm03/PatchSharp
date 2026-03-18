using System.Collections.Generic;

namespace PatchSharp.Internal;

internal static class ChunkApplier
{
    public static string Apply(string input, List<Chunk> chunks, string newline)
    {
        var lines = new List<string>(input.Split('\n'));

        // Check if any chunks need InsertAtEnd handling (uncommon).
        List<Chunk>? endChunks = null;
        foreach (var chunk in chunks)
        {
            if (chunk.InsertAtEnd)
            {
                endChunks ??= [];
                endChunks.Add(chunk);
            }
        }

        // Apply positional chunks in reverse order to avoid index shifting.
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            var chunk = chunks[i];
            if (chunk.InsertAtEnd) continue;

            if (chunk.OrigIndex > lines.Count)
                throw new PatchApplyException(
                    $"Chunk origIndex {chunk.OrigIndex} exceeds input length {lines.Count}");

            if (chunk.DelLines.Count > 0)
                lines.RemoveRange(chunk.OrigIndex, chunk.DelLines.Count);

            if (chunk.InsLines.Count > 0)
                lines.InsertRange(chunk.OrigIndex, chunk.InsLines);
        }

        // Apply InsertAtEnd chunks in order at current EOF.
        if (endChunks != null)
        {
            int insertAt = lines.Count > 0 && lines[lines.Count - 1] == ""
                ? lines.Count - 1
                : lines.Count;
            foreach (var chunk in endChunks)
            {
                lines.InsertRange(insertAt, chunk.InsLines);
                insertAt += chunk.InsLines.Count;
            }
        }

        return string.Join(newline, lines);
    }
}
