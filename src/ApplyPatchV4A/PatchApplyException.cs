namespace ApplyPatchV4A;

using System;

public class PatchApplyException : Exception
{
    public int? LineNumber { get; }
    public int Fuzz { get; }
    public string? Context { get; }

    public PatchApplyException(string message, int? lineNumber = null, int fuzz = 0, string? context = null)
        : base(message)
    {
        LineNumber = lineNumber;
        Fuzz = fuzz;
        Context = context;
    }

    public PatchApplyException(string message, Exception innerException, int? lineNumber = null, int fuzz = 0, string? context = null)
        : base(message, innerException)
    {
        LineNumber = lineNumber;
        Fuzz = fuzz;
        Context = context;
    }
}
