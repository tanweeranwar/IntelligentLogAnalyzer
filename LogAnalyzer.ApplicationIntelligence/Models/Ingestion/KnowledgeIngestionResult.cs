using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

public sealed class KnowledgeIngestionResult
{
    public required ApplicationProfile Profile { get; init; }

    public IReadOnlyCollection<KnowledgeSourceDescriptor>
        ProcessedSources
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> Warnings
    {
        get;
        init;
    } = [];

    public DateTimeOffset CompletedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;
}