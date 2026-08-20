namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryScanRequest
{
    public required string Location
    {
        get;
        init;
    }

    public string Provider
    {
        get;
        init;
    } = "Local";

    public string RepositoryName
    {
        get;
        init;
    } = string.Empty;

    public string Branch
    {
        get;
        init;
    } = string.Empty;

    public bool IncludeTests
    {
        get;
        init;
    }

    public bool IncludeGeneratedFiles
    {
        get;
        init;
    }

    public int MaximumFiles
    {
        get;
        init;
    } = 5000;
}