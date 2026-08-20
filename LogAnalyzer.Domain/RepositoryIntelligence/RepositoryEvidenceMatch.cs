namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryEvidenceMatch
{
    public string MatchType { get; init; } =
        string.Empty;

    public string MatchedValue { get; init; } =
        string.Empty;

    public string Project { get; init; } =
        string.Empty;

    public string FilePath { get; init; } =
        string.Empty;

    public string ClassName { get; init; } =
        string.Empty;

    public string MethodName { get; init; } =
        string.Empty;

    public int? LineNumber { get; init; }

    public int ConfidenceScore { get; init; }

    public string Reason { get; init; } =
        string.Empty;
}