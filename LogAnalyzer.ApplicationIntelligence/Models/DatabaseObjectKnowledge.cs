namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class DatabaseObjectKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string OwnershipTeam { get; init; } = string.Empty;

    public bool IsWriteSensitive { get; init; }

    public IReadOnlyCollection<string> RelatedObjects { get; init; } = [];

    public IReadOnlyCollection<string> UsedByComponentIds { get; init; } = [];

    public IReadOnlyCollection<string> UsedByWorkflowIds { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}