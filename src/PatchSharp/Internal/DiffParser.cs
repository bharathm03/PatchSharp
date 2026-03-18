using System.Collections.Generic;

namespace PatchSharp.Internal;

internal static class DiffParser
{
    internal const string BeginPatch = "*** Begin Patch";
    internal const string EndPatch = "*** End Patch";
    internal const string EndFile = "*** End of File";
    internal const string UpdateFile = "*** Update File:";
    internal const string DeleteFile = "*** Delete File:";
    internal const string AddFile = "*** Add File:";

    internal static readonly string[] SectionTerminators = new[]
    {
        EndPatch, UpdateFile, DeleteFile, AddFile,
    };

    internal static readonly string[] EndSectionMarkers = new[]
    {
        EndPatch, UpdateFile, DeleteFile, AddFile, EndFile,
    };

    private static readonly string[] SectionBreakPrefixes = new[]
    {
        "@@", EndPatch, UpdateFile, DeleteFile, AddFile, EndFile,
    };

    public static List<string> NormalizeDiffLines(string diff)
    {
        var lines = new List<string>();
        foreach (var line in diff.Split('\n'))
        {
            lines.Add(line.TrimEnd('\r'));
        }
        if (lines.Count > 0 && lines[lines.Count - 1] == "")
            lines.RemoveAt(lines.Count - 1);

        // Strip optional *** Begin Patch header
        if (lines.Count > 0 && lines[0] == BeginPatch)
            lines.RemoveAt(0);

        return lines;
    }

    public static string ParseCreateDiff(List<string> lines, string newline)
    {
        var allLines = new List<string>(lines) { EndPatch };
        var output = new List<string>();
        int index = 0;

        while (index < allLines.Count && !IsDone(allLines, index, SectionTerminators))
        {
            var line = allLines[index];
            index++;
            if (!line.StartsWith("+"))
                throw new PatchApplyException($"Invalid Add File Line: {line}", lineNumber: index);
            output.Add(line.Substring(1));
        }

        return string.Join(newline, output);
    }

    internal static bool IsDone(List<string> lines, int index, string[] prefixes)
    {
        if (index >= lines.Count) return true;
        var current = lines[index];
        foreach (var prefix in prefixes)
        {
            if (current.StartsWith(prefix)) return true;
        }
        return false;
    }

    internal sealed class ReadSectionResult
    {
        public List<string> NextContext { get; }
        public List<Chunk> SectionChunks { get; }
        public int EndIndex { get; }
        public bool Eof { get; }

        public ReadSectionResult(List<string> nextContext, List<Chunk> sectionChunks, int endIndex, bool eof)
        {
            NextContext = nextContext;
            SectionChunks = sectionChunks;
            EndIndex = endIndex;
            Eof = eof;
        }
    }

    public static ReadSectionResult ReadSection(List<string> lines, int startIndex)
    {
        var context = new List<string>();
        var delLines = new List<string>();
        var insLines = new List<string>();
        var sectionChunks = new List<Chunk>();
        char mode = ' '; // ' ' = keep, '+' = add, '-' = delete
        int index = startIndex;

        while (index < lines.Count)
        {
            var raw = lines[index];

            if (IsDone(lines, index, SectionBreakPrefixes))
                break;

            if (raw == "***") break;
            if (raw.StartsWith("***"))
                throw new PatchApplyException($"Invalid Line: {raw}", lineNumber: index);

            index++;
            char lastMode = mode;
            string line = raw.Length == 0 ? " " : raw;
            char prefix = line[0];

            if (prefix != '+' && prefix != '-' && prefix != ' ')
            {
                // Per codex-cli: if we've already parsed lines, break out
                // (assume start of next hunk). If first line, error.
                if (index - 1 == startIndex)
                    throw new PatchApplyException($"Invalid Line: {line}", lineNumber: index);
                index--; // Back up so the outer loop can re-process this line
                break;
            }

            mode = prefix;
            string lineContent = line.Substring(1);
            bool switchingToContext = mode == ' ' && lastMode != mode;

            if (switchingToContext && (delLines.Count > 0 || insLines.Count > 0))
            {
                FlushChunk(sectionChunks, context, delLines, insLines);
            }

            if (mode == '-')
            {
                delLines.Add(lineContent);
                context.Add(lineContent);
            }
            else if (mode == '+')
            {
                insLines.Add(lineContent);
            }
            else
            {
                context.Add(lineContent);
            }
        }

        if (delLines.Count > 0 || insLines.Count > 0)
        {
            FlushChunk(sectionChunks, context, delLines, insLines);
        }

        // Strip trailing empty context lines — they are inter-chunk blank
        // separators in the diff text, not real context (matches codex-cli).
        // Guard: only strip while context.Count exceeds the last chunk's
        // OrigIndex, since OrigIndex is a 0-based offset into context and
        // removing lines at or below it would invalidate chunk positions.
        while (context.Count > 0 && context[context.Count - 1] == ""
               && (sectionChunks.Count == 0 || context.Count > sectionChunks[sectionChunks.Count - 1].OrigIndex))
        {
            context.RemoveAt(context.Count - 1);
        }

        if (index < lines.Count && lines[index] == EndFile)
            return new ReadSectionResult(context, sectionChunks, index + 1, true);

        if (index == startIndex)
        {
            string nextLine = index < lines.Count ? lines[index] : "";
            throw new PatchApplyException($"Nothing in this section - index={index} {nextLine}");
        }

        return new ReadSectionResult(context, sectionChunks, index, false);
    }

    private static void FlushChunk(List<Chunk> sectionChunks, List<string> context, List<string> delLines, List<string> insLines)
    {
        sectionChunks.Add(new Chunk(
            context.Count - delLines.Count,
            new List<string>(delLines),
            new List<string>(insLines)));
        delLines.Clear();
        insLines.Clear();
    }

    public static ParsedDiff ParseUpdateDiff(List<string> lines, string input)
    {
        var allLines = new List<string>(lines) { EndPatch };
        var inputLines = new List<string>(input.Split('\n'));
        var chunks = new List<Chunk>();
        int cursor = 0;
        int index = 0;
        int fuzz = 0;

        while (!IsDone(allLines, index, EndSectionMarkers))
        {
            // Skip non-parseable lines between sections: blank lines are formatting
            // artifacts, unprefixed non-empty lines are garbage from broken hunks.
            // Per codex-cli, both should be silently skipped between sections.
            // Note: ReadSection also breaks out on unprefixed lines (backing up
            // the index), so this loop handles the line that ReadSection yielded.
            while (index < allLines.Count
                   && !IsDone(allLines, index, EndSectionMarkers)
                   && !allLines[index].StartsWith("@@"))
            {
                var skipped = allLines[index];
                bool isValidDiffLine = skipped.Length > 0
                    && (skipped[0] == '+' || skipped[0] == '-' || skipped[0] == ' ');
                if (isValidDiffLine)
                    break; // This is a real diff line (first chunk without @@)
                index++;
            }
            if (IsDone(allLines, index, EndSectionMarkers))
                break;

            // Read anchor
            string anchor = "";
            if (index < allLines.Count && allLines[index].StartsWith("@@ "))
            {
                anchor = allLines[index].Substring(3);
                index++;
            }
            else
            {
                bool hasBareAnchor = index < allLines.Count && allLines[index] == "@@";
                if (hasBareAnchor)
                {
                    index++;
                }
                else if (cursor != 0)
                {
                    string currentLine = index < allLines.Count ? allLines[index] : "";
                    throw new PatchApplyException($"Invalid Line:\n{currentLine}", lineNumber: index);
                }
            }

            if (!string.IsNullOrWhiteSpace(anchor))
            {
                cursor = ContextMatcher.AdvanceCursorToAnchor(anchor, inputLines, cursor, ref fuzz);
            }

            var section = ReadSection(allLines, index);
            var findResult = ContextMatcher.FindContext(inputLines, section.NextContext, cursor, section.Eof);

            if (findResult.NewIndex == -1)
            {
                string ctxText = string.Join("\n", section.NextContext);
                string label = section.Eof ? "Invalid EOF Context" : "Invalid Context";
                throw new PatchApplyException($"{label} {cursor}:\n{ctxText}",
                    fuzz: fuzz, context: ctxText);
            }

            // Per codex-cli: if the last chunk's del lines end with an empty
            // string (trailing-newline sentinel from Split) and that would
            // overflow the input, trim the sentinel from both del and ins lines.
            if (section.SectionChunks.Count > 0)
            {
                var lastChunk = section.SectionChunks[section.SectionChunks.Count - 1];
                if (lastChunk.DelLines.Count > 0
                    && lastChunk.DelLines[lastChunk.DelLines.Count - 1] == "")
                {
                    int chunkStart = findResult.NewIndex + lastChunk.OrigIndex;
                    if (chunkStart + lastChunk.DelLines.Count > inputLines.Count)
                    {
                        lastChunk.DelLines.RemoveAt(lastChunk.DelLines.Count - 1);
                        if (lastChunk.InsLines.Count > 0
                            && lastChunk.InsLines[lastChunk.InsLines.Count - 1] == "")
                        {
                            lastChunk.InsLines.RemoveAt(lastChunk.InsLines.Count - 1);
                        }
                    }
                }
            }

            // Pure addition: all chunks are insert-only with no deletions and
            // no context lines to anchor against.  Per codex-cli, insert at EOF.
            bool isPureAddition = section.NextContext.Count == 0
                && section.SectionChunks.Count > 0
                && section.SectionChunks.TrueForAll(c => c.DelLines.Count == 0);

            // Don't advance cursor for pure additions — they append to EOF
            // without consuming any file content, so subsequent hunks must
            // still match against the original content from the current cursor.
            if (!isPureAddition)
                cursor = findResult.NewIndex + section.NextContext.Count;
            fuzz += findResult.Fuzz;
            index = section.EndIndex;

            foreach (var ch in section.SectionChunks)
            {
                if (isPureAddition)
                {
                    ch.InsertAtEnd = true;
                }
                else
                {
                    ch.OrigIndex += findResult.NewIndex;
                }
                chunks.Add(ch);
            }
        }

        return new ParsedDiff(chunks, fuzz);
    }
}
