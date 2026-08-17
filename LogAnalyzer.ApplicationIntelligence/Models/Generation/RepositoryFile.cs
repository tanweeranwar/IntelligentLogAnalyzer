namespace LogAnalyzer.ApplicationIntelligence.Models.Generation;

public sealed class RepositoryFile
{
    public string RelativePath { get; init; } =
        string.Empty;

    public string FileName { get; init; } =
        string.Empty;

    public string Extension { get; init; } =
        string.Empty;

    public string Category { get; init; } =
        string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset LastModifiedUtc { get; init; }

    public string? ProjectName { get; init; }
}