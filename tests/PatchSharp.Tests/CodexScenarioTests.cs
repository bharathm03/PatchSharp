namespace PatchSharp.Tests;

/// <summary>
/// Tests ported from codex-cli (openai/codex) apply-patch fixture scenarios.
/// Fixtures copied from codex-rs/apply-patch/tests/fixtures/scenarios/.
/// Only single-file diff logic scenarios are included — file-level operations
/// (move, delete, multi-file) are out of scope for PatchSharp's string API.
/// </summary>
public class CodexScenarioTests
{
    private static readonly string FixturesRoot = FindFixturesRoot();

    private static readonly string[] HeaderPrefixes =
    [
        "*** Begin Patch", "*** End Patch",
        "*** Update File:", "*** Add File:", "*** Delete File:",
    ];

    private static string FindFixturesRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tests", "fixtures", "codex-scenarios");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException(
            "Could not find codex fixture scenarios directory at tests/fixtures/codex-scenarios/");
    }

    private static string ReadFixture(string scenario, string relativePath)
    {
        var path = Path.Combine(FixturesRoot, scenario, relativePath);
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    /// <summary>
    /// Extracts the single-file diff body from a codex-format patch.
    /// Strips *** Begin/End Patch and *** Update/Add/Delete File: headers.
    /// Preserves *** End of File (PatchSharp handles it natively).
    /// </summary>
    private static string ExtractDiffBody(string patch)
    {
        var lines = new List<string>();
        foreach (var raw in patch.Split('\n'))
        {
            var trimmed = raw.TrimStart();
            bool isHeader = false;
            foreach (var prefix in HeaderPrefixes)
            {
                if (trimmed.StartsWith(prefix))
                {
                    isHeader = true;
                    break;
                }
            }
            if (!isHeader)
                lines.Add(raw);
        }

        while (lines.Count > 0 && lines[lines.Count - 1] == "")
            lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }

    // --- Fixture-driven update scenarios (parameterized) ---

    public static IEnumerable<object[]> UpdateScenarios =>
    [
        ["003_multiple_chunks",                    "input/multi.txt",      "expected/multi.txt"],
        ["014_update_file_appends_trailing_newline","input/no_newline.txt", "expected/no_newline.txt"],
        ["016_pure_addition_update_chunk",         "input/input.txt",      "expected/input.txt"],
        ["017_whitespace_padded_hunk_header",      "input/foo.txt",        "expected/foo.txt"],
        ["018_whitespace_padded_patch_markers",    "input/file.txt",       "expected/file.txt"],
        ["019_unicode_simple",                     "input/foo.txt",        "expected/foo.txt"],
        ["020_whitespace_padded_patch_marker_lines","input/file.txt",      "expected/file.txt"],
        ["021_update_file_deletion_only",          "input/lines.txt",      "expected/lines.txt"],
        ["022_update_file_end_of_file_marker",     "input/tail.txt",       "expected/tail.txt"],
    ];

    [Theory]
    [MemberData(nameof(UpdateScenarios))]
    public void CodexUpdate_AppliesPatchCorrectly(string scenario, string inputFile, string expectedFile)
    {
        var input = ReadFixture(scenario, inputFile);
        var patch = ReadFixture(scenario, "patch.txt");
        var expected = ReadFixture(scenario, expectedFile);

        var diff = ExtractDiffBody(patch);
        var result = ApplyPatch.Apply(input, diff);

        Assert.Equal(expected, result);
    }

    // --- Add file scenario ---

    [Fact]
    public void Codex001_AddFile()
    {
        var patch = ReadFixture("001_add_file", "patch.txt");
        var expected = ReadFixture("001_add_file", "expected/bar.md");

        var diff = ExtractDiffBody(patch);
        var result = ApplyPatch.Create(diff);

        // Codex-cli appends trailing newline at the file-write layer.
        // PatchSharp.Create is a string API — it doesn't add one.
        Assert.Equal(expected.TrimEnd('\n'), result);
    }

    // --- Error/rejection scenarios ---

    [Fact]
    public void Codex006_RejectsMissingContext()
    {
        var input = ReadFixture("006_rejects_missing_context", "input/modify.txt");
        var patch = ReadFixture("006_rejects_missing_context", "patch.txt");

        var diff = ExtractDiffBody(patch);
        Assert.Throws<PatchApplyException>(() => ApplyPatch.Apply(input, diff));
    }

    [Fact]
    public void Codex008_EmptyUpdateHunk_NoOp()
    {
        var input = ReadFixture("008_rejects_empty_update_hunk", "input/foo.txt");
        var patch = ReadFixture("008_rejects_empty_update_hunk", "patch.txt");
        var expected = ReadFixture("008_rejects_empty_update_hunk", "expected/foo.txt");

        // Codex-cli rejects at the file-operation level; PatchSharp treats
        // an empty diff as a no-op, matching the expected output (unchanged).
        var diff = ExtractDiffBody(patch);
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal(expected, result);
    }

    // --- Inline equivalents of codex-rs unit tests ---

    [Fact]
    public void SeekSequence_TrimEndMatch_TrailingWhitespace()
    {
        // Port of test_rstrip_match_ignores_trailing_whitespace
        var input = "aaa   \nbbb\nccc";
        var diff = " aaa\n-bbb\n+BBB\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("aaa   \nBBB\nccc", result);
    }

    [Fact]
    public void SeekSequence_TrimMatch_LeadingAndTrailingWhitespace()
    {
        // Port of test_trim_match_ignores_leading_and_trailing_whitespace
        var input = "  aaa  \nbbb\nccc";
        var diff = " aaa\n-bbb\n+BBB\n ccc";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("  aaa  \nBBB\nccc", result);
    }

    [Fact]
    public void Parser_MultiHunkPatch()
    {
        // Port of multi-hunk test with bare @@ anchors
        var input = "line1\nline2\nline3\nline4";
        var diff = "@@\n-line2\n+changed2\n@@\n-line4\n+changed4";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("line1\nchanged2\nline3\nchanged4", result);
    }

    [Fact]
    public void Parser_UpdateWithoutExplicitAnchor()
    {
        // Port: first chunk starts with diff lines, no @@ header
        var input = "aaa\nbbb";
        var diff = "-aaa\n+AAA\n bbb";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("AAA\nbbb", result);
    }

    [Fact]
    public void Parser_EndOfFileMarker()
    {
        // Port of update_file_chunk test: EOF marker
        var input = "first\nsecond";
        var diff = " first\n-second\n+second updated\n*** End of File";
        var result = ApplyPatch.Apply(input, diff);
        Assert.Equal("first\nsecond updated", result);
    }
}
