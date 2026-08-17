namespace LogAnalyzer.Infrastructure.AI;

internal sealed class DistilledEvidence
{
    public IReadOnlyCollection<EvidenceFact> Facts
    {
        get;
        init;
    } = [];

    public string ToPromptText()
    {
        if (Facts.Count == 0)
        {
            return "No high-value evidence was extracted.";
        }

        return string.Join(
            Environment.NewLine,
            Facts.Select(
                fact =>
                    $"- [{fact.Authority}] [{fact.Category}] " +
                    $"{fact.Label}: {fact.Value} " +
                    $"(source: {fact.Source})"));
    }
}

internal sealed class EvidenceFact
{
    public string Category { get; init; } =
        string.Empty;

    public string Label { get; init; } =
        string.Empty;

    public string Value { get; init; } =
        string.Empty;

    public int Priority { get; init; }

    public EvidenceAuthority Authority { get; init; } =
        EvidenceAuthority.Unknown;

    public string Source { get; init; } =
        string.Empty;
}