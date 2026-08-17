namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApplicationRepositoryKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string RepositoryType { get; init; } = string.Empty;

    public string DefaultBranch { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string OwnershipTeam { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Technologies { get; init; } = [];

    public IReadOnlyCollection<string> Components { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}