namespace LogAnalyzer.Domain.AI;

public sealed class InvestigationCompleteness
{
    public int OverallScore { get; init; }

    public InvestigationCompletenessItem Evidence { get; init; } =
        new();

    public InvestigationCompletenessItem ApplicationContext { get; init; } =
        new();

    public InvestigationCompletenessItem Workflow { get; init; } =
        new();

    public InvestigationCompletenessItem Dependencies { get; init; } =
        new();

    public InvestigationCompletenessItem RootCause { get; init; } =
        new();

    public InvestigationCompletenessItem BusinessImpact { get; init; } =
        new();

    public InvestigationCompletenessItem ChangeHistory { get; init; } =
        new();

    public IReadOnlyCollection<string> MissingInformation
    { get; init; } = [];
}

public sealed class InvestigationCompletenessItem
{
    public string Name { get; init; } =
        string.Empty;

    public int Score { get; init; }

    public string Status { get; init; } =
        "Unknown";

    public string Explanation { get; init; } =
        string.Empty;
}