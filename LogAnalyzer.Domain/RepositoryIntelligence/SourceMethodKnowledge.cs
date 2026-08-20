namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class SourceMethodKnowledge
{
    public string Name { get; init; } =
        string.Empty;

    public string Signature { get; init; } =
        string.Empty;

    public int? LineNumber { get; init; }

    public IReadOnlyCollection<string> CalledMethods
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> DatabaseOperations
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> ConfigurationKeys
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> Routes
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> DbContexts
    {
        get;
        init;
    } = [];
}