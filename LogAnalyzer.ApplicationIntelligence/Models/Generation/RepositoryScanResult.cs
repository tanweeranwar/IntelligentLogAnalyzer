namespace LogAnalyzer.ApplicationIntelligence.Models.Generation;

public sealed class RepositoryScanResult
{
    public string RootPath { get; init; } =
        string.Empty;

    public DateTimeOffset ScannedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public int TotalFiles { get; init; }

    public long TotalSizeBytes { get; init; }

    public IReadOnlyCollection<RepositoryProject> Projects
    { get; init; } = [];

    public IReadOnlyCollection<RepositoryFile> Files
    { get; init; } = [];

    public IReadOnlyCollection<string> SolutionFiles
    { get; init; } = [];

    public IReadOnlyCollection<string> Technologies
    { get; init; } = [];

    public IReadOnlyDictionary<string, int> FilesByCategory
    { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyCollection<string> Warnings
    { get; init; } = [];
}