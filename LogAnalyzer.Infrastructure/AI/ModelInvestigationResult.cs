namespace LogAnalyzer.Infrastructure.AI;

internal sealed class ModelInvestigationResult
{
    public string ExecutiveSummary { get; init; } =
        string.Empty;

    public ModelNextAction NextAction { get; init; } =
        new();

    public IReadOnlyCollection<ModelRootCause> RootCauses
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<ModelInvestigationStep>
        InvestigationSteps
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<ModelResolutionRecommendation>
        ResolutionRecommendations
    {
        get;
        init;
    } = [];

    public int OverallConfidenceScore { get; init; }

    public IReadOnlyCollection<string> Unknowns
    {
        get;
        init;
    } = [];
}

internal sealed class ModelNextAction
{
    public string Action { get; init; } =
        string.Empty;

    public string Why { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

internal sealed class ModelRootCause
{
    public string Cause { get; init; } =
        string.Empty;

    public string Evidence { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

internal sealed class ModelInvestigationStep
{
    public int Sequence { get; init; }

    public string Action { get; init; } =
        string.Empty;

    public string Expected { get; init; } =
        string.Empty;
}

internal sealed class ModelResolutionRecommendation
{
    public string Recommendation { get; init; } =
        string.Empty;

    public string Condition { get; init; } =
        string.Empty;
}