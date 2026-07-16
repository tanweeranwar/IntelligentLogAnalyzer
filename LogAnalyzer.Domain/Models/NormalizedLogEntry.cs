namespace LogAnalyzer.Domain.Models;

public sealed class NormalizedLogEntry
{
    public int LineNumber { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string Severity { get; init; } = "Unknown";

    public string ExceptionType { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public int? HttpStatusCode { get; init; }

    public string RequestUrl { get; init; } = string.Empty;

    public string ApiPath { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string ServerName { get; init; } = string.Empty;

    public string MachineName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string StackTrace { get; init; } = string.Empty;

    public string RawContent { get; init; } = string.Empty;
}