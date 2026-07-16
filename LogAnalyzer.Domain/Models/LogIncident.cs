namespace LogAnalyzer.Domain.Models;

public sealed class LogIncident
{
    public string IncidentId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public TimeSpan? Duration =>
        StartedAt.HasValue && EndedAt.HasValue
            ? EndedAt.Value - StartedAt.Value
            : null;

    public string Severity { get; init; } = "Unknown";

    public int EntryCount { get; init; }

    public string ExceptionType { get; init; } = string.Empty;

    public int? HttpStatusCode { get; init; }

    public string ApiPath { get; init; } = string.Empty;

    public string ServerName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public IReadOnlyCollection<NormalizedLogEntry> Entries { get; init; } =
        Array.Empty<NormalizedLogEntry>();

    public IncidentIntelligence Intelligence { get; init; } = new();
}