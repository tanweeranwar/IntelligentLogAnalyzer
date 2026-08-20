namespace LogAnalyzer.Domain.RepositoryIntelligence;

public sealed class RepositoryEvidenceQuery
{
    public string RepositoryId { get; init; } =
        string.Empty;

    public string ExceptionType { get; init; } =
        string.Empty;

    public string StackFrame { get; init; } =
        string.Empty;

    public string DatabaseOperation { get; init; } =
        string.Empty;

    public string ApiPath { get; init; } =
        string.Empty;

    public string ClassName { get; init; } =
        string.Empty;

    public string MethodName { get; init; } =
        string.Empty;
}