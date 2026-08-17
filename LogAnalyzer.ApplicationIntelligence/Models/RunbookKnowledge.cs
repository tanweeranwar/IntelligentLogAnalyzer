namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class RunbookKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string OwnerTeam { get; init; } = string.Empty;

    public string SeverityScope { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Preconditions { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationSteps { get; init; } = [];

    public IReadOnlyCollection<string> MitigationSteps { get; init; } = [];

    public IReadOnlyCollection<string> ValidationSteps { get; init; } = [];

    public IReadOnlyCollection<string> EscalationCriteria { get; init; } = [];

    public IReadOnlyCollection<string> RelatedWorkflowIds { get; init; } = [];

    public IReadOnlyCollection<string> RelatedKnownIssueIds { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}