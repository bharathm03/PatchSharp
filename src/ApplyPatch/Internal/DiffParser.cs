using System.Collections.Generic;

namespace ApplyPatch.Internal;

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
                throw new PatchApplyException($"Invalid Line: {line}", lineNumber: index);

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
            sectionChunks.Add(new Chunk(
                context.Count - delLines.Count,
                new List<string>(delLines),
                new List<string>(insLines)));
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

            cursor = findResult.NewIndex + section.NextContext.Count;
            fuzz += findResult.Fuzz;
            index = section.EndIndex;

            foreach (var ch in section.SectionChunks)
            {
                ch.OrigIndex += findResult.NewIndex;
                chunks.Add(ch);
            }
        }

        return new ParsedDiff(chunks, fuzz);
    }
}
