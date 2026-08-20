namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryApiEndpoint
{
    public string Route { get; init; } =
        string.Empty;

    public string HttpMethod { get; init; } =
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
}