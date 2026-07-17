using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed partial class PlainTextLogParser : ILogParser
{
    private readonly IRawLogEventBuilder
        _rawLogEventBuilder;

    private readonly ILogAnalysisPipeline
        _analysisPipeline;

    public PlainTextLogParser(
        IRawLogEventBuilder rawLogEventBuilder,
        ILogAnalysisPipeline analysisPipeline)
    {
        _rawLogEventBuilder =
            rawLogEventBuilder;

        _analysisPipeline =
            analysisPipeline;
    }

    public string Name =>
        "Plain Text Log Parser";

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
    [
        ".txt",
        ".log"
    ];

    public bool CanParse(
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension =
            Path.GetExtension(fileName);

        return SupportedExtensions.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<bool> CanParseAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return Task.FromResult(
            CanParse(fileName));
    }

    public async Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var rawEvents =
            await _rawLogEventBuilder.BuildAsync(
                stream,
                cancellationToken);

        var entries =
            new List<NormalizedLogEntry>();

        DateTimeOffset? currentTimestamp =
            null;

        foreach (var rawEvent in rawEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content =
                rawEvent.RawContent;

            var primaryLine =
                rawEvent.PrimaryLine;

            var exceptionType =
                ExtractExceptionType(content);

            var statusCode =
                ExtractHttpStatusCode(content);

            var severity =
                DetectSeverity(
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
                severity is
                    "Error" or
                    "Critical" or
                    "Warning" ||
                !string.IsNullOrWhiteSpace(
                    exceptionType) ||
                statusCode is >= 400;

            if (!isRelevant)
            {
                continue;
            }

            entries.Add(
                new NormalizedLogEntry
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

        var totalLines =
            rawEvents.Count == 0
                ? 0
                : rawEvents.Max(
                    rawEvent =>
                        rawEvent.EndLineNumber);

        return _analysisPipeline.Build(
            totalLines,
            entries);
    }

    private static string DetectSeverity(
        string primaryLine,
        string fullContent,
        int? statusCode,
        string exceptionType)
    {
        if (CriticalLevelRegex().IsMatch(
                primaryLine))
        {
            return "Critical";
        }

        if (ErrorLevelRegex().IsMatch(
                primaryLine) ||
            statusCode is >= 400 ||
            !string.IsNullOrWhiteSpace(
                exceptionType))
        {
            return "Error";
        }

        if (WarningLevelRegex().IsMatch(
                primaryLine))
        {
            return "Warning";
        }

        if (CriticalLevelRegex().IsMatch(
                fullContent))
        {
            return "Critical";
        }

        if (WarningLevelRegex().IsMatch(
                fullContent))
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
            .Trim() ??
            string.Empty;
    }

    private static string ExtractStackTrace(
        RawLogEvent rawEvent)
    {
        if (string.IsNullOrWhiteSpace(
                rawEvent.RawContent))
        {
            return string.Empty;
        }

        var lines =
            rawEvent.RawContent.Split(
                Environment.NewLine,
                StringSplitOptions.None);

        if (lines.Length <= 1)
        {
            return string.Empty;
        }

        var continuationLines =
            lines
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
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            ExceptionRegex().Match(content);

        return match.Success
            ? match.Value
            : string.Empty;
    }

    private static int? ExtractHttpStatusCode(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var patterns = new[]
        {
            HttpStatusAfterHttpRegex(),
            NamedHttpStatusRegex(),
            HttpStatusDescriptionRegex()
        };

        foreach (var pattern in patterns)
        {
            var match =
                pattern.Match(content);

            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(
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
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

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
            '}',
            '"',
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
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            CorrelationIdRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static string ExtractServerName(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            ServerNameRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static string ExtractMachineName(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            MachineNameRegex().Match(content);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return ExtractServerName(content);
    }

    private static string ExtractEnvironment(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            EnvironmentRegex().Match(content);

        if (match.Success)
        {
            var environment =
                match.Groups[1]
                    .Value
                    .ToUpperInvariant();

            return environment ==
                   "PRODUCTION"
                ? "PROD"
                : environment;
        }

        var serverName =
            ExtractServerName(content);

        if (string.IsNullOrWhiteSpace(
                serverName))
        {
            serverName =
                ExtractMachineName(content);
        }

        if (string.IsNullOrWhiteSpace(
                serverName))
        {
            return string.Empty;
        }

        if (serverName.Contains(
                "PREPROD",
                StringComparison.OrdinalIgnoreCase))
        {
            return "PREPROD";
        }

        if (serverName.Contains(
                "RQA",
                StringComparison.OrdinalIgnoreCase))
        {
            return "RQA";
        }

        if (serverName.Contains(
                "UAT",
                StringComparison.OrdinalIgnoreCase))
        {
            return "UAT";
        }

        if (serverName.Contains(
                "DEV",
                StringComparison.OrdinalIgnoreCase))
        {
            return "DEV";
        }

        if (serverName.Contains(
                "PROD",
                StringComparison.OrdinalIgnoreCase) ||
            serverName.Contains(
                "PRIV",
                StringComparison.OrdinalIgnoreCase))
        {
            return "PROD";
        }

        return string.Empty;
    }

    private static string ExtractUserName(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var match =
            UserNameRegex().Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static DateTimeOffset? ExtractTimestamp(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

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

    [GeneratedRegex(
        @"\b(?:[A-Za-z_][A-Za-z0-9_.+`]*Exception)\b")]
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
        @"(?i)\b(?:user|userName|loginName)\s*[:=]\s*([A-Za-z0-9._@\\\-]+)")]
    private static partial Regex UserNameRegex();
}