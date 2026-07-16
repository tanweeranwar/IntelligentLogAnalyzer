using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed partial class PlainTextLogParser : ILogParser
{
    private readonly IIncidentIntelligenceService
        _incidentIntelligenceService;

    private readonly ILogIncidentBuilder
        _incidentBuilder;

    private readonly IRawLogEventBuilder
        _rawLogEventBuilder;

    public PlainTextLogParser(
        IIncidentIntelligenceService incidentIntelligenceService,
        ILogIncidentBuilder incidentBuilder,
        IRawLogEventBuilder rawLogEventBuilder)
    {
        _incidentIntelligenceService =
            incidentIntelligenceService;

        _incidentBuilder =
            incidentBuilder;

        _rawLogEventBuilder =
            rawLogEventBuilder;
    }

    public async Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var rawEvents =
            await _rawLogEventBuilder.BuildAsync(
                stream,
                cancellationToken);

        var entries = new List<NormalizedLogEntry>();

        DateTimeOffset? currentTimestamp = null;

        foreach (var rawEvent in rawEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = rawEvent.RawContent;
            var primaryLine = rawEvent.PrimaryLine;

            var exceptionType =
                ExtractExceptionType(content);

            var statusCode =
                ExtractHttpStatusCode(content);

            var severity = DetectSeverity(
                primaryLine,
                content,
                statusCode,
                exceptionType);

            var requestUrl =
                ExtractUrl(content);

            var apiPath =
                ExtractApiPath(requestUrl);

            var correlationId =
                ExtractCorrelationId(content);

            var serverName =
                ExtractServerName(content);

            var machineName =
                ExtractMachineName(content);

            var environment =
                ExtractEnvironment(content);

            var userName =
                ExtractUserName(content);

            var detectedTimestamp =
                ExtractTimestamp(primaryLine);

            if (detectedTimestamp.HasValue)
            {
                currentTimestamp =
                    detectedTimestamp;
            }

            var timestamp =
                detectedTimestamp ??
                currentTimestamp;

            var isRelevant =
                severity is "Error" or "Critical" or "Warning" ||
                !string.IsNullOrWhiteSpace(exceptionType) ||
                statusCode >= 400;

            if (!isRelevant)
            {
                continue;
            }

            entries.Add(new NormalizedLogEntry
            {
                LineNumber =
                    rawEvent.StartLineNumber,

                Timestamp =
                    timestamp,

                Severity =
                    severity,

                ExceptionType =
                    exceptionType,

                Message =
                    GetDisplayMessage(rawEvent),

                HttpStatusCode =
                    statusCode,

                RequestUrl =
                    requestUrl,

                ApiPath =
                    apiPath,

                CorrelationId =
                    correlationId,

                ServerName =
                    serverName,

                MachineName =
                    machineName,

                Environment =
                    environment,

                UserName =
                    userName,

                StackTrace =
                    ExtractStackTrace(rawEvent),

                RawContent =
                    content
            });
        }

        var summaries = entries
            .GroupBy(CreateSignature)
            .Select(group =>
            {
                var first = group.First();
                var occurrenceCount = group.Count();

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
            .OrderByDescending(
                item =>
                    item.Intelligence.PriorityScore)
            .ThenByDescending(
                item =>
                    item.OccurrenceCount)
            .ToArray();

        var timestamps = entries
            .Where(item =>
                item.Timestamp.HasValue)
            .Select(item =>
                item.Timestamp!.Value)
            .OrderBy(item => item)
            .ToArray();

        var incidents =
            _incidentBuilder.Build(entries);

        var totalLines = rawEvents.Count == 0
            ? 0
            : rawEvents.Max(
                item => item.EndLineNumber);

        return new LogAnalysisResult
        {
            TotalLines =
                totalLines,

            ErrorCount = entries.Count(
                item =>
                    item.Severity is
                        "Error" or "Critical"),

            WarningCount = entries.Count(
                item =>
                    item.Severity == "Warning"),

            FirstTimestamp = timestamps.Length > 0
                ? timestamps[0]
                : null,

            LastTimestamp = timestamps.Length > 0
                ? timestamps[^1]
                : null,

            Entries =
                entries,

            ErrorSummaries =
                summaries,

            Incidents =
                incidents
        };
    }

    private static string DetectSeverity(
        string primaryLine,
        string fullContent,
        int? statusCode,
        string exceptionType)
    {
        if (CriticalLevelRegex().IsMatch(primaryLine))
        {
            return "Critical";
        }

        if (ErrorLevelRegex().IsMatch(primaryLine) ||
            statusCode >= 400 ||
            !string.IsNullOrWhiteSpace(exceptionType))
        {
            return "Error";
        }

        if (WarningLevelRegex().IsMatch(primaryLine))
        {
            return "Warning";
        }

        if (CriticalLevelRegex().IsMatch(fullContent))
        {
            return "Critical";
        }

        if (WarningLevelRegex().IsMatch(fullContent))
        {
            return "Warning";
        }

        return "Information";
    }

    private static string GetDisplayMessage(
        RawLogEvent rawEvent)
    {
        if (!string.IsNullOrWhiteSpace(
                rawEvent.PrimaryLine))
        {
            return rawEvent.PrimaryLine.Trim();
        }

        return rawEvent.RawContent
            .Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;
    }

    private static string ExtractStackTrace(
        RawLogEvent rawEvent)
    {
        var lines = rawEvent.RawContent
            .Split(
                Environment.NewLine,
                StringSplitOptions.None);

        if (lines.Length <= 1)
        {
            return string.Empty;
        }

        var continuationLines = lines
            .Skip(1)
            .Where(line =>
                !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (continuationLines.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            continuationLines);
    }

    private static string ExtractExceptionType(
        string content)
    {
        var match =
            ExceptionRegex().Match(content);

        return match.Success
            ? match.Value
            : string.Empty;
    }

    private static int? ExtractHttpStatusCode(
        string content)
    {
        var patterns = new[]
        {
            HttpStatusAfterHttpRegex(),
            NamedHttpStatusRegex(),
            HttpStatusDescriptionRegex()
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(content);

            if (match.Success &&
                int.TryParse(
                    match.Groups[1].Value,
                    out var statusCode))
            {
                return statusCode;
            }
        }

        return null;
    }

    private static string ExtractUrl(
        string content)
    {
        var match =
            UrlRegex().Match(content);

        if (!match.Success)
        {
            return string.Empty;
        }

        return match.Value.TrimEnd(
            '.',
            ',',
            ':',
            ';',
            ')',
            ']',
            '\'');
    }

    private static string ExtractApiPath(
        string requestUrl)
    {
        if (string.IsNullOrWhiteSpace(
                requestUrl))
        {
            return string.Empty;
        }

        return Uri.TryCreate(
            requestUrl,
            UriKind.Absolute,
            out var uri)
            ? uri.AbsolutePath
            : string.Empty;
    }

    private static string ExtractCorrelationId(
        string content)
    {
        var match =
            CorrelationIdRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractServerName(
        string content)
    {
        var match =
            ServerNameRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractMachineName(
        string content)
    {
        var match =
            MachineNameRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractEnvironment(
        string content)
    {
        var match =
            EnvironmentRegex().Match(content);

        return match.Success
            ? match.Groups[1]
                .Value
                .ToUpperInvariant()
            : string.Empty;
    }

    private static string ExtractUserName(
        string content)
    {
        var match =
            UserNameRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static DateTimeOffset? ExtractTimestamp(
        string content)
    {
        var match =
            TimestampRegex().Match(content);

        if (!match.Success)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            match.Value,
            out var timestamp)
            ? timestamp
            : null;
    }

    private static string CreateSignature(
        NormalizedLogEntry entry)
    {
        var sourceText =
            string.IsNullOrWhiteSpace(entry.RawContent)
                ? entry.Message
                : entry.RawContent;

        var normalizedMessage = TimestampRegex()
            .Replace(
                sourceText,
                "{TIMESTAMP}");

        normalizedMessage = GuidRegex()
            .Replace(
                normalizedMessage,
                "{GUID}");

        normalizedMessage = ThreadOrIdentifierRegex()
            .Replace(
                normalizedMessage,
                "{ID}");

        normalizedMessage = LongNumberRegex()
            .Replace(
                normalizedMessage,
                "{NUMBER}");

        normalizedMessage = WhitespaceRegex()
            .Replace(
                normalizedMessage,
                " ")
            .ToLowerInvariant()
            .Trim();

        return string.Join(
            "|",
            entry.ExceptionType,
            entry.HttpStatusCode,
            entry.ApiPath,
            normalizedMessage);
    }

    [GeneratedRegex(
        @"\b(?:[A-Za-z_][A-Za-z0-9_.]*Exception)\b")]
    private static partial Regex ExceptionRegex();

    [GeneratedRegex(
        @"(?i)\bHTTP(?:/\d(?:\.\d)?)?\s+(?:status\s*)?([45]\d{2})\b")]
    private static partial Regex HttpStatusAfterHttpRegex();

    [GeneratedRegex(
        @"(?i)\b(?:status|statusCode|httpStatusCode)\s*[:=]\s*([45]\d{2})\b")]
    private static partial Regex NamedHttpStatusRegex();

    [GeneratedRegex(
        @"(?i)\b([45]\d{2})\s+(?:Bad Request|Unauthorized|Forbidden|Not Found|Request Timeout|Conflict|Internal Server Error|Bad Gateway|Service Unavailable|Gateway Timeout)\b")]
    private static partial Regex HttpStatusDescriptionRegex();

    [GeneratedRegex(
        @"(?i)\b(?:CRITICAL|FATAL)\b")]
    private static partial Regex CriticalLevelRegex();

    [GeneratedRegex(
        @"(?i)\b(?:ERROR|ERR)\b")]
    private static partial Regex ErrorLevelRegex();

    [GeneratedRegex(
        @"(?i)\b(?:WARNING|WARN)\b")]
    private static partial Regex WarningLevelRegex();

    [GeneratedRegex(
        @"https?://[^\s""']+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(
        @"\b\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        @"\[(?:\d+)\]")]
    private static partial Regex ThreadOrIdentifierRegex();

    [GeneratedRegex(
        @"\b\d{5,}\b")]
    private static partial Regex LongNumberRegex();

    [GeneratedRegex(
        @"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"(?i)\b(?:correlation[-_ ]?id|request[-_ ]?id|trace[-_ ]?id)\s*[:=]\s*([A-Za-z0-9\-]+)")]
    private static partial Regex CorrelationIdRegex();

    [GeneratedRegex(
        @"(?i)\b(?:server|serverName)\s*[:=]\s*([A-Za-z0-9._\-]+)")]
    private static partial Regex ServerNameRegex();

    [GeneratedRegex(
        @"(?i)\b(?:machine|machineName|host|hostName)\s*[:=]\s*([A-Za-z0-9._\-]+)")]
    private static partial Regex MachineNameRegex();

    [GeneratedRegex(
        @"(?i)\b(?:environment|env)\s*[:=]\s*(DEV|RQA\d*|QA|UAT|PREPROD|PROD|PRODUCTION)\b")]
    private static partial Regex EnvironmentRegex();

    [GeneratedRegex(
        @"(?i)\b(?:user|userName|loginName)\s*[:=]\s*([A-Za-z0-9._\\\-]+)")]
    private static partial Regex UserNameRegex();
}