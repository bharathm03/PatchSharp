using ApplyPatchV4A.Internal;

namespace ApplyPatchV4A;

public static class ApplyPatch
{
    /// <summary>
    /// Apply a V4A diff to create new file content.
    /// All diff lines must be prefixed with "+".
    /// </summary>
    public static string Create(string diff)
    {
        var newline = NewlineHelper.DetectNewline("", diff, isCreateMode: true);
        var diffLines = DiffParser.NormalizeDiffLines(diff);
        return DiffParser.ParseCreateDiff(diffLines, newline);
    }

    /// <summary>
    /// Apply a V4A diff to update existing text content.
    /// </summary>
    public static string Apply(string input, string diff)
    {
        var newline = NewlineHelper.DetectNewline(input, diff, isCreateMode: false);
        var diffLines = DiffParser.NormalizeDiffLines(diff);
        var normalizedInput = NewlineHelper.NormalizeToLf(input);
        var parsed = DiffParser.ParseUpdateDiff(diffLines, normalizedInput);
        return ChunkApplier.Apply(normalizedInput, parsed.Chunks, newline);
    }
}
