namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApiEndpointKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Route { get; init; } = string.Empty;

    public string HttpMethod { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Controller { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string RepositoryId { get; init; } = string.Empty;

    public string WorkflowId { get; init; } = string.Empty;

    public IReadOnlyCollection<string> ComponentIds { get; init; } = [];

    public IReadOnlyCollection<string> DependencyIds { get; init; } = [];

    public IReadOnlyCollection<string> DatabaseObjectIds { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}