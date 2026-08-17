namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class KnowledgeContribution
{
    public string ApplicationId { get; init; } =
        string.Empty;

    public string ApplicationName { get; init; } =
        string.Empty;

    public KnowledgeContributionType Type { get; init; } =
        KnowledgeContributionType.Unknown;

    public string Key { get; init; } =
        string.Empty;

    public string Value { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }

    public KnowledgeSourceKind SourceKind { get; init; } =
        KnowledgeSourceKind.Unknown;

    public string SourceName { get; init; } =
        string.Empty;

    public string Evidence { get; init; } =
        string.Empty;

    public bool IsIdentityEvidence { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } =
        [];
}