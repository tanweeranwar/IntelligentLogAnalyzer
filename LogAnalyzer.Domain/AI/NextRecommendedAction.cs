namespace LogAnalyzer.Domain.AI;

public sealed class NextRecommendedAction
{
    public string Title { get; init; } =
        string.Empty;

    public string Action { get; init; } =
        string.Empty;

    public string Reason { get; init; } =
        string.Empty;

    public string ExpectedOutcome { get; init; } =
        string.Empty;

    public string SuggestedOwner { get; init; } =
        "Production Support";

    public string EstimatedEffort { get; init; } =
        "Unknown";

    public int ConfidenceScore { get; init; }
}