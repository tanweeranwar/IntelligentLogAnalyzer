namespace LogAnalyzer.Domain.AI;

public sealed class ReasoningPackage
{
    public string SystemInstructions { get; init; } =
        string.Empty;

    public string InvestigationContext { get; init; } =
        string.Empty;

    public string OutputInstructions { get; init; } =
        string.Empty;

    public InvestigationMode Mode { get; init; } =
        InvestigationMode.Deep;

    public string Version { get; init; } =
        "1.0";

    public int EstimatedTokenCount { get; init; }

    public IReadOnlyDictionary<string, string> Metadata
    {
        get;
        init;
    } = new Dictionary<string, string>();
}