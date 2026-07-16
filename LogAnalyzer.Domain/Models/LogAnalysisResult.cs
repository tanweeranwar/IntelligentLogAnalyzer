namespace LogAnalyzer.Domain.Models;

public sealed class LogAnalysisResult
{
    public int TotalLines { get; init; }

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    public DateTimeOffset? FirstTimestamp { get; init; }

    public DateTimeOffset? LastTimestamp { get; init; }

    public IncidentIntelligence Intelligence { get; init; } = new();

    public IReadOnlyCollection<NormalizedLogEntry> Entries { get; init; } =
        Array.Empty<NormalizedLogEntry>();

    public IReadOnlyCollection<ErrorSummary> ErrorSummaries { get; init; } =
        Array.Empty<ErrorSummary>();

    public IReadOnlyCollection<LogIncident> Incidents { get; init; } =
    Array.Empty<LogIncident>();
}

public sealed class ErrorSummary
{
    public string Signature { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string ExceptionType { get; init; } = string.Empty;

    public int OccurrenceCount { get; init; }

    public int? HttpStatusCode { get; init; }
    public IncidentIntelligence Intelligence { get; init; } = new();
}