namespace LogAnalyzer.Domain.Models;

public sealed class ApplicationContextResult
{
    public string ApplicationName { get; init; } =
        string.Empty;

    public string IncidentId { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }

    public IReadOnlyCollection<MatchedApplicationComponent>
        Components
    { get; init; } = [];

    public IReadOnlyCollection<MatchedApplicationWorkflow>
        Workflows
    { get; init; } = [];

    public IReadOnlyCollection<MatchedKnownIssue>
        KnownIssues
    { get; init; } = [];

    public IReadOnlyCollection<string> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<string> DatabaseObjects
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationHints
    { get; init; } = [];

    public bool HasContext =>
        Components.Count > 0 ||
        Workflows.Count > 0 ||
        KnownIssues.Count > 0;
}

public sealed class MatchedApplicationComponent
{
    public string Name { get; init; } =
        string.Empty;

    public string Type { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public int MatchScore { get; init; }

    public IReadOnlyCollection<string> MatchReasons
    { get; init; } = [];

    public IReadOnlyCollection<string> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<string> DatabaseObjects
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationHints
    { get; init; } = [];
}

public sealed class MatchedApplicationWorkflow
{
    public string Name { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public int MatchScore { get; init; }

    public IReadOnlyCollection<string> MatchReasons
    { get; init; } = [];

    public IReadOnlyCollection<string> Steps
    { get; init; } = [];

    public IReadOnlyCollection<string> Dependencies
    { get; init; } = [];

    public IReadOnlyCollection<string> InvestigationHints
    { get; init; } = [];
}

public sealed class MatchedKnownIssue
{
    public string Title { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public int MatchScore { get; init; }

    public IReadOnlyCollection<string> MatchReasons
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