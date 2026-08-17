namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class LogDiscoveryFact
{
    public KnowledgeContributionType Type { get; init; } =
        KnowledgeContributionType.Unknown;

    public string Key { get; init; } =
        string.Empty;

    public string Value { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }

    public string Evidence { get; init; } =
        string.Empty;

    public bool IsApplicationIdentityHint { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } =
        [];
}