namespace LogAnalyzer.Domain.Models;

public sealed class IncidentIntelligence
{
    public string Priority { get; init; } = "Low";

    public int PriorityScore { get; init; }

    public string LikelyCause { get; init; } = string.Empty;

    public string Impact { get; init; } = string.Empty;

    public IReadOnlyCollection<string> RecommendedChecks { get; init; } =
        Array.Empty<string>();
}