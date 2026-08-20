namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class SourceFileKnowledge
{
    public string FilePath { get; init; } =
        string.Empty;

    public string ProjectName { get; init; } =
        string.Empty;

    public string Language { get; init; } =
        string.Empty;

    public string Namespace { get; init; } =
        string.Empty;

    public IReadOnlyCollection<SourceTypeKnowledge> Types
    {
        get;
        init;
    } = [];
}