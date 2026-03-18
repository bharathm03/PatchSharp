using System.Collections.Generic;

namespace PatchSharp.Internal;

internal static class ChunkApplier
{
    public static string Apply(string input, List<Chunk> chunks, string newline)
    {
        var lines = new List<string>(input.Split('\n'));

        // Separate pure-addition (InsertAtEnd) chunks from positional chunks.
        // Positional chunks are applied in reverse order to avoid index shifting.
        // InsertAtEnd chunks are applied afterwards at the current end of file.
        var positionalChunks = new List<Chunk>();
        var endChunks = new List<Chunk>();
        foreach (var chunk in chunks)
        {
            if (chunk.InsertAtEnd)
                endChunks.Add(chunk);
            else
                positionalChunks.Add(chunk);
        }

        // Apply positional chunks in reverse order
        for (int i = positionalChunks.Count - 1; i >= 0; i--)
        {
            var chunk = positionalChunks[i];

            if (chunk.OrigIndex > lines.Count)
                throw new PatchApplyException(
                    $"Chunk origIndex {chunk.OrigIndex} exceeds input length {lines.Count}");

            // Remove deleted lines
            for (int j = 0; j < chunk.DelLines.Count; j++)
                lines.RemoveAt(chunk.OrigIndex);

            // Insert new lines
            for (int j = 0; j < chunk.InsLines.Count; j++)
                lines.Insert(chunk.OrigIndex + j, chunk.InsLines[j]);
        }

        // Apply InsertAtEnd chunks in order at current EOF.
        // Compute insertAt once so all end-chunks insert at a consistent
        // position (before a trailing empty line from Split, if present).
        if (endChunks.Count > 0)
        {
            int insertAt = lines.Count > 0 && lines[lines.Count - 1] == ""
                ? lines.Count - 1
                : lines.Count;
            foreach (var chunk in endChunks)
            {
                for (int j = 0; j < chunk.InsLines.Count; j++)
                    lines.Insert(insertAt + j, chunk.InsLines[j]);
                insertAt += chunk.InsLines.Count;
            }
        }

        return string.Join(newline, lines);
    }
}
