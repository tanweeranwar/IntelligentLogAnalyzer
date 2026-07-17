using System.Globalization;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Services;

public sealed partial class LogAnalysisPipeline
    : ILogAnalysisPipeline
{
    private readonly IIncidentIntelligenceService
        _incidentIntelligenceService;

    private readonly ILogIncidentBuilder
        _incidentBuilder;

    private readonly ILogCorrelationService
        _logCorrelationService;

    private readonly IApplicationHealthService
        _applicationHealthService;

    public LogAnalysisPipeline(
        IIncidentIntelligenceService incidentIntelligenceService,
        ILogIncidentBuilder incidentBuilder,
        ILogCorrelationService logCorrelationService,
        IApplicationHealthService applicationHealthService)
    {
        _incidentIntelligenceService =
            incidentIntelligenceService;

        _incidentBuilder =
            incidentBuilder;

        _logCorrelationService =
            logCorrelationService;

        _applicationHealthService =
            applicationHealthService;
    }

    public LogAnalysisResult Build(
        int totalLines,
        IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var correlatedEntries =
            _logCorrelationService
                .Correlate(entries)
                .ToArray();

        var summaries = correlatedEntries
            .GroupBy(CreateSignature)
            .Select(group =>
            {
                var first =
                    group.First();

                var occurrenceCount =
                    group.Count();

                return new ErrorSummary
                {
                    Signature =
                        group.Key,

                    Message =
                        first.Message,

                    ExceptionType =
                        first.ExceptionType,

                    HttpStatusCode =
                        first.HttpStatusCode,

                    OccurrenceCount =
                        occurrenceCount,

                    Intelligence =
                        _incidentIntelligenceService.Analyze(
                            first.Message,
                            first.ExceptionType,
                            first.HttpStatusCode,
                            occurrenceCount)
                };
            })
            .OrderByDescending(summary =>
                summary.Intelligence.PriorityScore)
            .ThenByDescending(summary =>
                summary.OccurrenceCount)
            .ToArray();

        var timestamps = correlatedEntries
            .Where(entry =>
                entry.Timestamp.HasValue)
            .Select(entry =>
                entry.Timestamp!.Value)
            .OrderBy(timestamp =>
                timestamp)
            .ToArray();

        var incidents =
            _incidentBuilder.Build(
                correlatedEntries);

        var health =
            _applicationHealthService.Calculate(
                incidents);

        return new LogAnalysisResult
        {
            TotalLines =
                totalLines,

            ErrorCount =
                correlatedEntries.Count(entry =>
                    entry.Severity is
                        "Error" or "Critical"),

            WarningCount =
                correlatedEntries.Count(entry =>
                    entry.Severity ==
                    "Warning"),

            FirstTimestamp =
                timestamps.Length > 0
                    ? timestamps[0]
                    : null,

            LastTimestamp =
                timestamps.Length > 0
                    ? timestamps[^1]
                    : null,

            Entries =
                correlatedEntries,

            ErrorSummaries =
                summaries,

            Incidents =
                incidents,

            Health =
                health
        };
    }

    private static string CreateSignature(
        NormalizedLogEntry entry)
    {
        var sourceText =
            string.IsNullOrWhiteSpace(entry.RawContent)
                ? entry.Message ?? string.Empty
                : entry.RawContent;

        var normalizedMessage =
            TimestampRegex().Replace(
                sourceText,
                "{TIMESTAMP}");

        normalizedMessage =
            GuidRegex().Replace(
                normalizedMessage,
                "{GUID}");

        normalizedMessage =
            ThreadOrIdentifierRegex().Replace(
                normalizedMessage,
                "{ID}");

        normalizedMessage =
            FilePathLineNumberRegex().Replace(
                normalizedMessage,
                ":line {LINE}");

        normalizedMessage =
            LongNumberRegex().Replace(
                normalizedMessage,
                "{NUMBER}");

        normalizedMessage =
            WhitespaceRegex().Replace(
                    normalizedMessage,
                    " ")
                .ToLowerInvariant()
                .Trim();

        return string.Join(
            "|",
            entry.ExceptionType ?? string.Empty,
            entry.HttpStatusCode?.ToString(
                CultureInfo.InvariantCulture) ??
            string.Empty,
            entry.ApiPath ?? string.Empty,
            normalizedMessage);
    }

    [GeneratedRegex(
        @"\b\d{1,4}[./-]\d{1,2}[./-]\d{1,4}(?:[ T]\d{1,2}[:.]\d{2}[:.]\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        @"\[(?:\d+)\]")]
    private static partial Regex ThreadOrIdentifierRegex();

    [GeneratedRegex(
        @":line\s+\d+",
        RegexOptions.IgnoreCase)]
    private static partial Regex FilePathLineNumberRegex();

    [GeneratedRegex(
        @"\b\d{5,}\b")]
    private static partial Regex LongNumberRegex();

    [GeneratedRegex(
        @"\s+")]
    private static partial Regex WhitespaceRegex();
}