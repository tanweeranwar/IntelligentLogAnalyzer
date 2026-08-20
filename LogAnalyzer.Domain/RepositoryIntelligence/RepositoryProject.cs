namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryProject
{
    public string Name { get; init; } =
        string.Empty;

    public string FilePath { get; init; } =
        string.Empty;

    public string ProjectType { get; init; } =
        string.Empty;

    public string TargetFramework { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> ProjectReferences
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> PackageReferences
    {
        get;
        init;
    } = [];
}