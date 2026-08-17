namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApplicationKnowledge
{
    public required ApplicationMetadata Metadata { get; init; }

    public IReadOnlyCollection<ApplicationRepositoryKnowledge> Repositories
    { get; init; } = [];

    public IReadOnlyCollection<ArchitectureComponentKnowledge> Components
    { get; init; } = [];

    public IReadOnlyCollection<WorkflowKnowledge> Workflows
    { get; init; } = [];

    public IReadOnlyCollection<DependencyKnowledge> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<ApiEndpointKnowledge> ApiEndpoints
    { get; init; } = [];

    public IReadOnlyCollection<DatabaseObjectKnowledge> DatabaseObjects
    { get; init; } = [];

    public IReadOnlyCollection<RunbookKnowledge> Runbooks
    { get; init; } = [];

    public IReadOnlyCollection<KnownIssueKnowledge> KnownIssues
    { get; init; } = [];

    public IReadOnlyCollection<ConfigurationKnowledge> Configurations
    { get; init; } = [];

    public IReadOnlyCollection<ApplicationFingerprint> Fingerprints
    { get; init; } = [];
}