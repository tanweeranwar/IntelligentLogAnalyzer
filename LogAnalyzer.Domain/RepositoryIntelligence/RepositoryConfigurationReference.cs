namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryConfigurationReference
{
    public string Key { get; init; } =
        string.Empty;

    public string Project { get; init; } =
        string.Empty;

    public string FilePath { get; init; } =
        string.Empty;

    public string ClassName { get; init; } =
        string.Empty;

    public string MethodName { get; init; } =
        string.Empty;
}