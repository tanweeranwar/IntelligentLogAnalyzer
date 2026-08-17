namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ArchitectureComponentKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ComponentType { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string RepositoryId { get; init; } = string.Empty;

    public string OwnershipTeam { get; init; } = string.Empty;

    public IReadOnlyCollection<string> DependsOn { get; init; } = [];

    public IReadOnlyCollection<string> UsedBy { get; init; } = [];

    public IReadOnlyCollection<string> Environments { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}