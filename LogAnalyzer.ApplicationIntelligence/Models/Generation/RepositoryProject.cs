namespace LogAnalyzer.ApplicationIntelligence.Models.Generation;

public sealed class RepositoryProject
{
    public string Name { get; init; } =
        string.Empty;

    public string RelativePath { get; init; } =
        string.Empty;

    public string ProjectType { get; init; } =
        string.Empty;

    public string TargetFramework { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> ProjectReferences
    { get; init; } = [];

    public IReadOnlyCollection<string> PackageReferences
    { get; init; } = [];

    public IReadOnlyCollection<string> Technologies
    { get; init; } = [];
}