namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class KnownIssueKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = "Known";

    public string Severity { get; init; } = "Medium";

    public string RootCause { get; init; } = string.Empty;

    public string TemporaryMitigation { get; init; } = string.Empty;

    public string PermanentResolution { get; init; } = string.Empty;

    public string OwnerTeam { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Symptoms { get; init; } = [];

    public IReadOnlyCollection<string> ExceptionTypes { get; init; } = [];

    public IReadOnlyCollection<string> MessagePatterns { get; init; } = [];

    public IReadOnlyCollection<string> RelatedWorkflowIds { get; init; } = [];

    public IReadOnlyCollection<string> RelatedComponentIds { get; init; } = [];

    public IReadOnlyCollection<string> RelatedDependencyIds { get; init; } = [];

    public IReadOnlyCollection<string> RelatedRunbookIds { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}