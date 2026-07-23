namespace LogAnalyzer.Domain.AI;

public sealed class IncidentTimelineEvent
{
    public int Sequence { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string EventType { get; init; } =
        string.Empty;

    public string Title { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public string Severity { get; init; } =
        string.Empty;

    public string ExceptionType { get; init; } =
        string.Empty;

    public string ApiPath { get; init; } =
        string.Empty;

    public string ServerName { get; init; } =
        string.Empty;

    public string CorrelationId { get; init; } =
        string.Empty;

    public int? LineNumber { get; init; }

    public int OccurrenceCount { get; init; } = 1;

    public int ConfidenceScore { get; init; }
}