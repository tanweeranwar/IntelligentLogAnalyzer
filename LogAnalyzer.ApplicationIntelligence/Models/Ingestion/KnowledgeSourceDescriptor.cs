namespace LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

public sealed class KnowledgeSourceDescriptor
{
    public string Id { get; init; } =
        Guid.NewGuid().ToString("N");

    public KnowledgeSourceType Type { get; init; } =
        KnowledgeSourceType.Unknown;

    public string Name { get; init; } =
        string.Empty;

    public string Location { get; init; } =
        string.Empty;

    public bool IsEnabled { get; init; } =
        true;

    public IReadOnlyDictionary<string, string> Metadata
    {
        get;
        init;
    } = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase);
}