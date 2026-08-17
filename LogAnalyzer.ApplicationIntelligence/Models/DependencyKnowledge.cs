namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class DependencyKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DependencyType { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Direction { get; init; } = "Outbound";

    public string OwnershipTeam { get; init; } = string.Empty;

    public string Criticality { get; init; } = "Medium";

    public IReadOnlyCollection<string> Endpoints { get; init; } = [];

    public IReadOnlyCollection<string> FailureSymptoms { get; init; } = [];

    public IReadOnlyCollection<string> HealthChecks { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}