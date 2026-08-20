namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryDescriptor
{
    public string Id { get; init; } =
        string.Empty;

    public string Name { get; init; } =
        string.Empty;

    public string Provider { get; init; } =
        string.Empty;

    public string Location { get; init; } =
        string.Empty;

    public string DefaultBranch { get; init; } =
        string.Empty;

    public string CommitId { get; init; } =
        string.Empty;

    public DateTimeOffset ScannedAt { get; init; } =
        DateTimeOffset.UtcNow;
}