namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryDatabaseReference
{
    public string Operation { get; init; } =
        string.Empty;

    public string DatabaseType { get; init; } =
        string.Empty;

    public string DbContext { get; init; } =
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