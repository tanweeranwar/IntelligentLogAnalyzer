namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ConfigurationKnowledge
{
    public string Id { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string ValueType { get; init; } = string.Empty;

    public bool IsSensitive { get; init; }

    public bool RequiresRestart { get; init; }

    public IReadOnlyCollection<string> Environments { get; init; } = [];

    public IReadOnlyCollection<string> RelatedComponentIds { get; init; } = [];

    public IReadOnlyCollection<string> FailureSymptoms { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}