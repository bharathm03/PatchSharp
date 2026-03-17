using System.Collections.Generic;

namespace ApplyPatchV4A.Internal;

internal static class ChunkApplier
{
    public static string Apply(string input, List<Chunk> chunks, string newline)
    {
        var lines = new List<string>(input.Split('\n'));

        // Apply chunks in reverse order to avoid index shifting
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            var chunk = chunks[i];

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

        return string.Join(newline, lines);
    }
}
