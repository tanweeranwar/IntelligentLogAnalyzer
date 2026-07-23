namespace LogAnalyzer.Domain.AI;

public sealed class InvestigationReport
{
    public string IncidentId { get; init; } =
        string.Empty;

    public string ApplicationName { get; init; } =
        string.Empty;

    public string Environment { get; init; } =
        string.Empty;

    public string ExecutiveSummary { get; init; } =
        string.Empty;

    public NextRecommendedAction NextAction { get; init; } =
        new();

    public InvestigationCompleteness Completeness { get; init; } =
        new();

    public IReadOnlyCollection<IncidentTimelineEvent> Timeline
    {
        get;
        init;
    } = [];

    public string AffectedWorkflow { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> AffectedComponents
    { get; init; } = [];

    public IReadOnlyCollection<RootCauseHypothesis> RootCauses
    { get; init; } = [];

    public IReadOnlyCollection<InvestigationStep> InvestigationSteps
    { get; init; } = [];

    public IReadOnlyCollection<SuggestedSqlQuery> SuggestedSqlQueries
    { get; init; } = [];

    public IReadOnlyCollection<SuggestedCodeLocation> SuggestedCodeLocations
    { get; init; } = [];

    public IReadOnlyCollection<DependencyFinding> Dependencies
    { get; init; } = [];

    public BusinessImpactAssessment BusinessImpact { get; init; } =
        new();

    public IReadOnlyCollection<ResolutionRecommendation>
        ResolutionRecommendations
    { get; init; } = [];

    public IReadOnlyCollection<EvidenceReference> EvidenceReferences
    { get; init; } = [];

    public int OverallConfidenceScore { get; init; }

    public IReadOnlyCollection<string> Unknowns
    { get; init; } = [];

    public IReadOnlyCollection<string> Assumptions
    { get; init; } = [];

    public DateTimeOffset GeneratedAt { get; init; } =
        DateTimeOffset.UtcNow;


}

public sealed class RootCauseHypothesis
{
    public string Title { get; init; } =
        string.Empty;

    public string Explanation { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }

    public IReadOnlyCollection<string> SupportingEvidence
    { get; init; } = [];

    public IReadOnlyCollection<string> ContradictingEvidence
    { get; init; } = [];
}

public sealed class InvestigationStep
{
    public int Sequence { get; init; }

    public string Title { get; init; } =
        string.Empty;

    public string Action { get; init; } =
        string.Empty;

    public string Reason { get; init; } =
        string.Empty;

    public string ExpectedOutcome { get; init; } =
        string.Empty;

    public string Priority { get; init; } =
        "Medium";

    public int ConfidenceScore { get; init; }
}

public sealed class SuggestedSqlQuery
{
    public string Title { get; init; } =
        string.Empty;

    public string DatabaseName { get; init; } =
        string.Empty;

    public string Purpose { get; init; } =
        string.Empty;

    public string Query { get; init; } =
        string.Empty;

    public string ExpectedOutcome { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

public sealed class SuggestedCodeLocation
{
    public string Project { get; init; } =
        string.Empty;

    public string FilePath { get; init; } =
        string.Empty;

    public string ClassName { get; init; } =
        string.Empty;

    public string MethodName { get; init; } =
        string.Empty;

    public string Reason { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

public sealed class DependencyFinding
{
    public string Name { get; init; } =
        string.Empty;

    public string Type { get; init; } =
        string.Empty;

    public string Role { get; init; } =
        string.Empty;

    public string Risk { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

public sealed class BusinessImpactAssessment
{
    public string Severity { get; init; } =
        "Unknown";

    public string CustomerImpact { get; init; } =
        string.Empty;

    public string OperationalImpact { get; init; } =
        string.Empty;

    public string FinancialImpact { get; init; } =
        string.Empty;

    public string Scope { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

public sealed class ResolutionRecommendation
{
    public string Title { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public string RecommendationType { get; init; } =
        string.Empty;

    public string Risk { get; init; } =
        string.Empty;

    public int ConfidenceScore { get; init; }
}

public sealed class EvidenceReference
{
    public string EvidenceType { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public string Source { get; init; } =
        string.Empty;

    public int? LineNumber { get; init; }

    public string CorrelationId { get; init; } =
        string.Empty;
}