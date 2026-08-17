namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class WorkflowKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string BusinessPurpose { get; init; } = string.Empty;

    public string OwnerTeam { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Steps { get; init; } = [];

    public IReadOnlyCollection<string> EntryPoints { get; init; } = [];

    public IReadOnlyCollection<string> ComponentIds { get; init; } = [];

    public IReadOnlyCollection<string> DependencyIds { get; init; } = [];

    public IReadOnlyCollection<string> DatabaseObjectIds { get; init; } = [];

    public IReadOnlyCollection<string> RunbookIds { get; init; } = [];

    public IReadOnlyCollection<string> KnownIssueIds { get; init; } = [];

    public IReadOnlyCollection<string> FingerprintIds { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}