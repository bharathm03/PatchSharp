namespace ApplyPatchV4A.Internal;

internal static class NewlineHelper
{
    public static string DetectNewline(string input, string diff, bool isCreateMode)
    {
        if (!isCreateMode && input.Contains("\n"))
            return DetectNewlineFromText(input);
        return DetectNewlineFromText(diff);
    }

    public static string DetectNewlineFromText(string text)
    {
        return text.Contains("\r\n") ? "\r\n" : "\n";
    }

    public static string NormalizeToLf(string text)
    {
        return text.Replace("\r\n", "\n");
    }
}
