namespace LogAnalyzer.Infrastructure.AI;

internal sealed class ModelInvestigationResult
{
    public string Summary { get; init; } =
        string.Empty;

    public IReadOnlyCollection<ModelRootCause> Hypotheses
    {
        get;
        init;
    } = [];

    public string NextAction { get; init; } =
        string.Empty;
}

internal sealed class ModelRootCause
{
    public string Cause { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> Evidence
    {
        get;
        init;
    } = [];

    public int Confidence { get; init; }
}