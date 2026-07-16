namespace LogAnalyzer.Domain.Models;

public sealed class RawLogEvent
{
    public int StartLineNumber { get; init; }

    public int EndLineNumber { get; init; }

    public string PrimaryLine { get; init; } = string.Empty;

    public string RawContent { get; init; } = string.Empty;
}