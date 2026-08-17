namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class ApplicationProfileCandidate
{
    public string ApplicationId { get; init; } =
        string.Empty;

    public string ApplicationName { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }

    public IReadOnlyCollection<KnowledgeContribution>
        IdentityEvidence
    {
        get;
        init;
    } = [];
}