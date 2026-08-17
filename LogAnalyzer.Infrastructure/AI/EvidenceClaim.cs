namespace LogAnalyzer.Infrastructure.AI;

internal sealed class EvidenceClaim
{
    public string Category { get; init; } =
        string.Empty;

    public string Label { get; init; } =
        string.Empty;

    public string Value { get; init; } =
        string.Empty;

    public EvidenceAuthority Authority { get; init; }

    public int ConfidenceScore { get; init; }

    public string Source { get; init; } =
        string.Empty;

    public bool IsConfirmed =>
        Authority is EvidenceAuthority.RawLog
            or EvidenceAuthority.ExplicitIdentifier
            or EvidenceAuthority.RepositoryMatch
            or EvidenceAuthority.OpenApiMatch ||
        Authority == EvidenceAuthority.ExactKnowledgeMatch &&
        ConfidenceScore >= 80;

    public bool IsWeak =>
        Authority is EvidenceAuthority.Unknown
            or EvidenceAuthority.FuzzyContext;
}