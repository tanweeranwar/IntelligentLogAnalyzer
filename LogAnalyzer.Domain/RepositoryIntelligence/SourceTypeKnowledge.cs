namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class SourceTypeKnowledge
{
    public string Name { get; init; } =
        string.Empty;

    public string FullName { get; init; } =
        string.Empty;

    public string Kind { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> BaseTypes
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<SourceMethodKnowledge> Methods
    {
        get;
        init;
    } = [];
}