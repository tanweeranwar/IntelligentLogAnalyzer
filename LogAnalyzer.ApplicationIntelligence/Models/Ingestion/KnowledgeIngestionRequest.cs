namespace LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

public sealed class KnowledgeIngestionRequest
{
    public string ApplicationHint { get; init; } =
        string.Empty;

    public string EnvironmentHint { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> Evidence { get; init; } =
        [];

    public IReadOnlyCollection<KnowledgeSourceDescriptor> Sources
    {
        get;
        init;
    } = [];

    public IReadOnlyDictionary<string, string> Metadata
    {
        get;
        init;
    } = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase);
}