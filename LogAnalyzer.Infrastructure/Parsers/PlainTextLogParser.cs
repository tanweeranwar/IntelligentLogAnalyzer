using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed partial class PlainTextLogParser : ILogParser
{
    private readonly IIncidentIntelligenceService
    _incidentIntelligenceService;

    public PlainTextLogParser(
    IIncidentIntelligenceService incidentIntelligenceService)
    {
        _incidentIntelligenceService =
            incidentIntelligenceService;
    }

    public async Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var entries = new List<NormalizedLogEntry>();

        using var reader = new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var lineNumber = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineNumber++;

            var exceptionType = ExtractExceptionType(line);
            var statusCode = ExtractHttpStatusCode(line);
            var severity = DetectSeverity(
                line,
                statusCode,
                exceptionType);
            var requestUrl = ExtractUrl(line);
            var apiPath = ExtractApiPath(requestUrl);
            var correlationId = ExtractCorrelationId(line);
            var serverName = ExtractServerName(line);
            var machineName = ExtractMachineName(line);
            var environment = ExtractEnvironment(line);
            var userName = ExtractUserName(line);
            var timestamp = ExtractTimestamp(line);

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
                LineNumber = lineNumber,
                Timestamp = timestamp,
                Severity = severity,
                ExceptionType = exceptionType,
                Message = line.Trim(),
                HttpStatusCode = statusCode,
                RequestUrl = requestUrl,
                ApiPath = apiPath,
                CorrelationId = correlationId,
                ServerName = serverName,
                MachineName = machineName,
                Environment = environment,
                UserName = userName,
                RawContent = line
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
                    Signature = group.Key,
                    Message = first.Message,
                    ExceptionType = first.ExceptionType,
                    HttpStatusCode = first.HttpStatusCode,
                    OccurrenceCount = occurrenceCount,
                    Intelligence =
                        _incidentIntelligenceService.Analyze(
                            first.Message,
                            first.ExceptionType,
                            first.HttpStatusCode,
                            occurrenceCount)
                };
            })
            .OrderByDescending(
                item => item.Intelligence.PriorityScore)
            .ThenByDescending(
                item => item.OccurrenceCount)
            .ToArray();

        var timestamps = entries
            .Where(item => item.Timestamp.HasValue)
            .Select(item => item.Timestamp!.Value)
            .OrderBy(item => item)
            .ToArray();

        return new LogAnalysisResult
        {
            TotalLines = lineNumber,

            ErrorCount = entries.Count(
                item => item.Severity is "Error" or "Critical"),

            WarningCount = entries.Count(
                item => item.Severity == "Warning"),

            FirstTimestamp = timestamps.Length > 0
                ? timestamps[0]
                : null,

            LastTimestamp = timestamps.Length > 0
                ? timestamps[^1]
                : null,

            Entries = entries,
            ErrorSummaries = summaries
        };
    }

    private static string DetectSeverity(
    string line,
    int? statusCode,
    string exceptionType)
    {
        if (CriticalLevelRegex().IsMatch(line))
        {
            return "Critical";
        }

        if (ErrorLevelRegex().IsMatch(line) ||
            statusCode >= 400 ||
            !string.IsNullOrWhiteSpace(exceptionType))
        {
            return "Error";
        }

        if (WarningLevelRegex().IsMatch(line))
        {
            return "Warning";
        }

        return "Information";
    }

    private static string ExtractExceptionType(string line)
    {
        var match = ExceptionRegex().Match(line);

        return match.Success
            ? match.Value
            : string.Empty;
    }

    private static int? ExtractHttpStatusCode(string line)
    {
        var patterns = new[]
        {
        HttpStatusAfterHttpRegex(),
        NamedHttpStatusRegex(),
        HttpStatusDescriptionRegex()
    };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(line);

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

    private static string ExtractUrl(string line)
    {
        var match = UrlRegex().Match(line);

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
            ']');
    }

    private static string ExtractApiPath(string requestUrl)
    {
        if (string.IsNullOrWhiteSpace(requestUrl))
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

    private static string ExtractCorrelationId(string line)
    {
        var match = CorrelationIdRegex().Match(line);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractServerName(string line)
    {
        var match = ServerNameRegex().Match(line);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractMachineName(string line)
    {
        var match = MachineNameRegex().Match(line);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static string ExtractEnvironment(string line)
    {
        var match = EnvironmentRegex().Match(line);

        return match.Success
            ? match.Groups[1].Value.ToUpperInvariant()
            : string.Empty;
    }

    private static string ExtractUserName(string line)
    {
        var match = UserNameRegex().Match(line);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }

    private static DateTimeOffset? ExtractTimestamp(string line)
    {
        var match = TimestampRegex().Match(line);

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
        var normalizedMessage = TimestampRegex()
            .Replace(entry.Message, "{TIMESTAMP}");

        normalizedMessage = GuidRegex()
            .Replace(normalizedMessage, "{GUID}");

        normalizedMessage = NumberRegex()
            .Replace(normalizedMessage, "{NUMBER}");

        normalizedMessage = WhitespaceRegex()
            .Replace(normalizedMessage, " ")
            .ToLowerInvariant()
            .Trim();

        return string.Join(
            "|",
            entry.ExceptionType,
            entry.HttpStatusCode,
            normalizedMessage);
    }

    [GeneratedRegex(
        @"\b(?:[A-Za-z_][A-Za-z0-9_.]*Exception)\b")]
    private static partial Regex ExceptionRegex();

    //[GeneratedRegex(@"\b([45]\d{2})\b")]
    //private static partial Regex HttpStatusRegex();

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

    [GeneratedRegex(@"https?://[^\s""']+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b\d{5,}\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
    @"\b\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\s+")]
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