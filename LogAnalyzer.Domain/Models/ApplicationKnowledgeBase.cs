namespace LogAnalyzer.Domain.Models;

public sealed class ApplicationKnowledgeBase
{
    public string ApplicationName { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public IReadOnlyCollection<ApplicationComponent> Components
    { get; init; } = [];

    public IReadOnlyCollection<ApplicationWorkflow> Workflows
    { get; init; } = [];

    public IReadOnlyCollection<KnownApplicationIssue> KnownIssues
    { get; init; } = [];
}

public sealed class ApplicationComponent
{
    public string Name { get; init; } =
        string.Empty;

    public string Type { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> ApiPaths
    { get; init; } = [];

    public IReadOnlyCollection<string> ExceptionTypes
    { get; init; } = [];

    public IReadOnlyCollection<string> Servers
    { get; init; } = [];

    public IReadOnlyCollection<string> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<string> DatabaseObjects
    { get; init; } = [];

    public IReadOnlyCollection<string> Keywords
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationHints
    { get; init; } = [];
}

public sealed class ApplicationWorkflow
{
    public string Name { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> ApiPaths
    { get; init; } = [];

    public IReadOnlyCollection<string> Components
    { get; init; } = [];

    public IReadOnlyCollection<string> Steps
    { get; init; } = [];

    public IReadOnlyCollection<string> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<string> Keywords
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationHints
    { get; init; } = [];
}

public sealed class KnownApplicationIssue
{
    public string Title { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> ApiPaths
    { get; init; } = [];

    public IReadOnlyCollection<string> ExceptionTypes
    { get; init; } = [];

    public IReadOnlyCollection<int> HttpStatusCodes
    { get; init; } = [];

    public IReadOnlyCollection<string> MessagePatterns
    { get; init; } = [];

    public IReadOnlyCollection<string> LikelyCauses
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationSteps
    { get; init; } = [];

    public IReadOnlyCollection<string> ResolutionSteps
    { get; init; } = [];

    public IReadOnlyCollection<string> SuggestedQueries
    { get; init; } = [];
}