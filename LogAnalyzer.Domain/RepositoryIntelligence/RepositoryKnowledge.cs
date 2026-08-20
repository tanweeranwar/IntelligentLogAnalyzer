namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryKnowledge
{
    public required RepositoryDescriptor Repository
    {
        get;
        init;
    }

    public IReadOnlyCollection<RepositoryProject> Projects
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<SourceFileKnowledge> Files
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<RepositoryApiEndpoint> ApiEndpoints
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<RepositoryDatabaseReference> DatabaseReferences
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<RepositoryConfigurationReference>
        ConfigurationReferences
    {
        get;
        init;
    } = [];
}