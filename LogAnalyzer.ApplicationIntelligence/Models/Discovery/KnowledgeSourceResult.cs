namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class KnowledgeSourceResult
{
    public string SourceName { get; init; } =
        string.Empty;

    public KnowledgeSourceKind SourceKind { get; init; } =
        KnowledgeSourceKind.Unknown;

    public IReadOnlyCollection<KnowledgeContribution> Contributions
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> Warnings
    {
        get;
        init;
    } = [];

    public TimeSpan Duration { get; init; }

    public bool HasContributions =>
        Contributions.Count > 0;
}