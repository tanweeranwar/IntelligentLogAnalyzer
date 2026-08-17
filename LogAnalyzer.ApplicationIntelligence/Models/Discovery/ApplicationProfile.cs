namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class ApplicationProfile
{
    public string ApplicationId { get; init; } =
        string.Empty;

    public string ApplicationName { get; init; } =
        string.Empty;

    public int IdentificationConfidenceScore { get; init; }

    public bool IsIdentified =>
        !string.IsNullOrWhiteSpace(ApplicationId);

    public DateTimeOffset GeneratedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public IReadOnlyCollection<ApplicationProfileCandidate>
        Candidates
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<KnowledgeContribution>
        Contributions
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<KnowledgeSourceResult>
        SourceResults
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> Warnings
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<KnowledgeContribution>
        GetContributions(
            KnowledgeContributionType type)
    {
        return Contributions
            .Where(contribution =>
                contribution.Type == type)
            .ToArray();
    }

    public IReadOnlyCollection<string>
        GetValues(
            KnowledgeContributionType type)
    {
        return Contributions
            .Where(contribution =>
                contribution.Type == type)
            .Select(contribution =>
                contribution.Value)
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}