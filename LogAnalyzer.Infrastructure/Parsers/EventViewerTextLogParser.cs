using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed partial class EventViewerTextLogParser : ILogParser
{
    private static readonly string[] SupportedTimestampFormats =
    [
        "M/d/yyyy h:mm:ss tt",
        "M/d/yyyy hh:mm:ss tt",
        "MM/dd/yyyy h:mm:ss tt",
        "MM/dd/yyyy hh:mm:ss tt",
        "M/d/yyyy H:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "d.M.yyyy H.mm.ss",
        "dd.MM.yyyy HH.mm.ss",
        "d.M.yyyy H:mm:ss",
        "dd.MM.yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fffK"
    ];

    private readonly ILogAnalysisPipeline
        _analysisPipeline;

    public EventViewerTextLogParser(
        ILogAnalysisPipeline analysisPipeline)
    {
        _analysisPipeline =
            analysisPipeline;
    }

    public string Name =>
        "Event Viewer Text Parser";

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
    [
        ".txt"
    ];

    public bool CanParse(
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return Path.GetExtension(fileName)
            .Equals(
                ".txt",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> CanParseAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!CanParse(fileName))
        {
            return false;
        }

        if (!stream.CanSeek)
        {
            return false;
        }

        var originalPosition =
            stream.Position;

        try
        {
            stream.Position = 0;

            using var reader =
                new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 4096,
                    leaveOpen: true);

            for (var index = 0; index < 50; index++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var line =
                    await reader.ReadLineAsync(
                        cancellationToken);

                if (line is null)
                {
                    break;
                }

                if (IsHeaderLine(line))
                {
                    return true;
                }

                if (LooksLikeEventViewerRecord(line))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            stream.Position =
                originalPosition;
        }
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

        using var reader =
            new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 8192,
                leaveOpen: true);

        var records =
            new List<EventViewerRecord>();

        EventViewerRecord? currentRecord =
            null;

        var descriptionBuilder =
            new StringBuilder();

        var totalLines = 0;

        while (await reader.ReadLineAsync(
                   cancellationToken) is { } line)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            totalLines++;

            if (IsHeaderLine(line))
            {
                continue;
            }

            if (TryParseRecordStart(
                    line,
                    totalLines,
                    out var newRecord,
                    out var firstDescriptionLine))
            {
                AddCompletedRecord(
                    records,
                    currentRecord,
                    descriptionBuilder);

                currentRecord =
                    newRecord;

                descriptionBuilder.Clear();

                if (!string.IsNullOrWhiteSpace(
                        firstDescriptionLine))
                {
                    descriptionBuilder.Append(
                        firstDescriptionLine.Trim());
                }

                continue;
            }

            if (currentRecord is null)
            {
                continue;
            }

            if (descriptionBuilder.Length > 0)
            {
                descriptionBuilder.AppendLine();
            }

            descriptionBuilder.Append(line);
        }

        AddCompletedRecord(
            records,
            currentRecord,
            descriptionBuilder);

        var entries = records
            .Select(CreateNormalizedEntry)
            .Where(entry =>
                entry is not null)
            .Cast<NormalizedLogEntry>()
            .ToArray();

        return _analysisPipeline.Build(
            totalLines,
            entries);
    }

    private static bool LooksLikeEventViewerRecord(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts =
            line.Split('\t');

        if (parts.Length < 5)
        {
            return false;
        }

        return IsRecognizedLevel(
                   parts[0].Trim()) &&
               TryParseTimestamp(
                   parts[1].Trim(),
                   out _);
    }

    private static bool IsHeaderLine(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized =
            line.Trim();

        return normalized.Contains(
                   "Level",
                   StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(
                   "Date and Time",
                   StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(
                   "Source",
                   StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(
                   "Event ID",
                   StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(
                   "Task Category",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRecordStart(
        string line,
        int lineNumber,
        out EventViewerRecord? record,
        out string description)
    {
        record = null;
        description = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts =
            line.Split('\t');

        if (parts.Length < 5)
        {
            return false;
        }

        var level =
            parts[0].Trim();

        if (!IsRecognizedLevel(level))
        {
            return false;
        }

        var timestampText =
            parts[1].Trim();

        if (!TryParseTimestamp(
                timestampText,
                out var timestamp))
        {
            return false;
        }

        var source =
            parts[2].Trim();

        var eventId =
            int.TryParse(
                parts[3].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedEventId)
                    ? parsedEventId
                    : 0;

        var taskCategory =
            parts[4].Trim();

        if (parts.Length > 5)
        {
            description =
                string.Join(
                    "\t",
                    parts.Skip(5));
        }

        record =
            new EventViewerRecord
            {
                LineNumber =
                    lineNumber,

                Level =
                    level,

                Timestamp =
                    timestamp,

                Source =
                    source,

                EventId =
                    eventId,

                TaskCategory =
                    taskCategory
            };

        return true;
    }

    private static void AddCompletedRecord(
        ICollection<EventViewerRecord> records,
        EventViewerRecord? currentRecord,
        StringBuilder descriptionBuilder)
    {
        if (currentRecord is null)
        {
            return;
        }

        currentRecord.Description =
            CleanDescription(
                descriptionBuilder.ToString());

        records.Add(currentRecord);
    }

    private static bool IsRecognizedLevel(
        string level)
    {
        return level.Equals(
                   "Critical",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Error",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Warning",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Information",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Verbose",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Audit Success",
                   StringComparison.OrdinalIgnoreCase) ||
               level.Equals(
                   "Audit Failure",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static NormalizedLogEntry? CreateNormalizedEntry(
        EventViewerRecord record)
    {
        var description =
            record.Description.Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var exceptionType =
            ExtractExceptionType(description);

        var statusCode =
            ExtractHttpStatusCode(description);

        var requestUrl =
            ExtractRequestUrl(description);

        var apiPath =
            ExtractApiPath(requestUrl);

        var correlationId =
            ExtractCorrelationId(description);

        var serverName =
            ExtractServerName(description);

        var machineName =
            ExtractMachineName(description);

        var environment =
            ExtractEnvironment(
                description,
                serverName,
                machineName);

        var userName =
            ExtractUserName(description);

        var severity =
            NormalizeSeverity(
                record.Level,
                description,
                statusCode);

        var message =
            ExtractPrimaryMessage(
                description,
                exceptionType);

        return new NormalizedLogEntry
        {
            LineNumber =
                record.LineNumber,

            Timestamp =
                record.Timestamp,

            Severity =
                severity,

            ExceptionType =
                exceptionType,

            Message =
                message,

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
                ExtractStackTrace(description),

            RawContent =
                BuildRawContent(record)
        };
    }

    private static string NormalizeSeverity(
        string eventLevel,
        string description,
        int? statusCode)
    {
        if (IsDiagnosticApplicationEvent(
                description))
        {
            return "Information";
        }

        if (eventLevel.Equals(
                "Critical",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        if (eventLevel.Equals(
                "Warning",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (eventLevel.Equals(
                "Error",
                StringComparison.OrdinalIgnoreCase) ||
            eventLevel.Equals(
                "Audit Failure",
                StringComparison.OrdinalIgnoreCase) ||
            statusCode is >= 400)
        {
            return "Error";
        }

        return "Information";
    }

    private static bool IsDiagnosticApplicationEvent(
        string description)
    {
        var normalized =
            description.Trim();

        return normalized.StartsWith(
                   "outputValue.Value",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(
                   "isSkipReviewEnabled:",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPrimaryMessage(
        string description,
        string exceptionType)
    {
        var content =
            UnwrapOuterQuotes(description)
                .Trim();

        var metadataIndex =
            FindFirstMetadataIndex(content);

        if (metadataIndex > 0)
        {
            content =
                content[..metadataIndex]
                    .Trim();
        }

        var lines =
            content.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(line =>
                    !IsStackTraceLine(line))
                .Where(line =>
                    !IsMetadataLine(line))
                .ToArray();

        if (lines.Length == 0)
        {
            return !string.IsNullOrWhiteSpace(
                exceptionType)
                    ? exceptionType
                    : "Windows Event Viewer event";
        }

        var firstMeaningfulLine =
            lines.FirstOrDefault(line =>
                !line.Equals(
                    "Error Details:",
                    StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(
                firstMeaningfulLine))
        {
            firstMeaningfulLine =
                lines[0];
        }

        firstMeaningfulLine =
            firstMeaningfulLine.Trim('"');

        if (firstMeaningfulLine.Length > 600)
        {
            firstMeaningfulLine =
                firstMeaningfulLine[..600];
        }

        return firstMeaningfulLine;
    }

    private static string ExtractStackTrace(
        string description)
    {
        var lines =
            description.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var stackTraceLines =
            lines.Where(IsStackTraceLine)
                .ToArray();

        return stackTraceLines.Length == 0
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                stackTraceLines);
    }

    private static int FindFirstMetadataIndex(
        string content)
    {
        var markers = new[]
        {
            "\r\nDate and Time:",
            "\nDate and Time:",
            "\r\nHost Name:",
            "\nHost Name:",
            "\r\nError Type:",
            "\nError Type:",
            "\r\nError Details:",
            "\nError Details:"
        };

        return markers
            .Select(marker =>
                content.IndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase))
            .Where(index =>
                index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static bool IsStackTraceLine(
        string line)
    {
        var trimmed =
            line.TrimStart();

        return trimmed.StartsWith(
                   "at ",
                   StringComparison.Ordinal) ||
               trimmed.StartsWith(
                   "--- End of",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "at lambda_method",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetadataLine(
        string line)
    {
        return line.StartsWith(
                   "Date and Time:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Host Name:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Error Type:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Error Source:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Error Status Code:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Error Request Url:",
                   StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith(
                   "Error Details:",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractExceptionType(
        string content)
    {
        var explicitMatch =
            ExplicitExceptionTypeRegex()
                .Match(content);

        if (explicitMatch.Success)
        {
            return explicitMatch
                .Groups[1]
                .Value
                .Trim();
        }

        var exceptionMatches =
            ExceptionRegex()
                .Matches(content);

        if (exceptionMatches.Count == 0)
        {
            return string.Empty;
        }

        var preferredExceptions =
            exceptionMatches
                .Select(match =>
                    match.Value)
                .Where(value =>
                    !value.Equals(
                        "System.Web.HttpUnhandledException",
                        StringComparison.OrdinalIgnoreCase))
                .Where(value =>
                    !value.Equals(
                        "System.Data.Entity.Core.EntityCommandExecutionException",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        return preferredExceptions.Length > 0
            ? preferredExceptions[0]
            : exceptionMatches[0].Value;
    }

    private static int? ExtractHttpStatusCode(
        string content)
    {
        var explicitMatch =
            ExplicitStatusCodeRegex()
                .Match(content);

        if (explicitMatch.Success &&
            int.TryParse(
                explicitMatch.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var explicitStatusCode) &&
            explicitStatusCode is >= 100 and <= 599)
        {
            return explicitStatusCode;
        }

        var jsonStatusMatch =
            JsonStatusRegex()
                .Match(content);

        if (jsonStatusMatch.Success &&
            int.TryParse(
                jsonStatusMatch.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var jsonStatusCode))
        {
            return jsonStatusCode;
        }

        var httpFailureMatch =
            HttpFailureStatusRegex()
                .Match(content);

        if (httpFailureMatch.Success &&
            int.TryParse(
                httpFailureMatch.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var httpFailureStatus))
        {
            return httpFailureStatus;
        }

        return null;
    }

    private static string ExtractRequestUrl(
        string content)
    {
        var explicitMatch =
            RequestUrlRegex()
                .Match(content);

        if (explicitMatch.Success)
        {
            return SanitizeUrl(
                explicitMatch.Groups[1].Value);
        }

        var jsonUrlMatches =
            JsonUrlRegex()
                .Matches(content);

        if (jsonUrlMatches.Count > 0)
        {
            var gatewayUrl =
                jsonUrlMatches
                    .Select(match =>
                        SanitizeUrl(
                            match.Groups[1].Value))
                    .FirstOrDefault(url =>
                        url.Contains(
                            "/vsgateway/",
                            StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(
                    gatewayUrl))
            {
                return gatewayUrl;
            }

            return SanitizeUrl(
                jsonUrlMatches[0]
                    .Groups[1]
                    .Value);
        }

        var urlMatches =
            UrlRegex()
                .Matches(content);

        if (urlMatches.Count == 0)
        {
            return string.Empty;
        }

        var preferredUrl =
            urlMatches
                .Select(match =>
                    SanitizeUrl(match.Value))
                .FirstOrDefault(url =>
                    url.Contains(
                        "/vsgateway/",
                        StringComparison.OrdinalIgnoreCase));

        return !string.IsNullOrWhiteSpace(
            preferredUrl)
                ? preferredUrl
                : SanitizeUrl(
                    urlMatches[0].Value);
    }

    private static string ExtractApiPath(
        string requestUrl)
    {
        if (string.IsNullOrWhiteSpace(
                requestUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(
                requestUrl,
                UriKind.Absolute,
                out var uri))
        {
            return string.Empty;
        }

        var path =
            uri.AbsolutePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var gatewayIndex =
            path.IndexOf(
                "/vsgateway/",
                StringComparison.OrdinalIgnoreCase);

        return gatewayIndex >= 0
            ? path[gatewayIndex..]
            : path;
    }

    private static string ExtractServerName(
        string content)
    {
        var match =
            ServerNameRegex()
                .Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static string ExtractMachineName(
        string content)
    {
        var match =
            MachineNameRegex()
                .Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : ExtractServerName(content);
    }

    private static string ExtractEnvironment(
        string content,
        string serverName,
        string machineName)
    {
        var explicitMatch =
            EnvironmentRegex()
                .Match(content);

        if (explicitMatch.Success)
        {
            return NormalizeEnvironment(
                explicitMatch.Groups[1].Value);
        }

        var hostName =
            !string.IsNullOrWhiteSpace(serverName)
                ? serverName
                : machineName;

        if (string.IsNullOrWhiteSpace(hostName))
        {
            return string.Empty;
        }

        if (hostName.Contains(
                "PREPROD",
                StringComparison.OrdinalIgnoreCase))
        {
            return "PREPROD";
        }

        if (hostName.Contains(
                "RQA",
                StringComparison.OrdinalIgnoreCase))
        {
            return "RQA";
        }

        if (hostName.Contains(
                "UAT",
                StringComparison.OrdinalIgnoreCase))
        {
            return "UAT";
        }

        if (hostName.Contains(
                "DEV",
                StringComparison.OrdinalIgnoreCase))
        {
            return "DEV";
        }

        if (hostName.Contains(
                "PROD",
                StringComparison.OrdinalIgnoreCase) ||
            hostName.Contains(
                "PRIV",
                StringComparison.OrdinalIgnoreCase))
        {
            return "PROD";
        }

        return string.Empty;
    }

    private static string NormalizeEnvironment(
        string environment)
    {
        return environment.Equals(
            "PRODUCTION",
            StringComparison.OrdinalIgnoreCase)
                ? "PROD"
                : environment.ToUpperInvariant();
    }

    private static string ExtractUserName(
        string content)
    {
        var match =
            UserNameRegex()
                .Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static string ExtractCorrelationId(
        string content)
    {
        var match =
            CorrelationIdRegex()
                .Match(content);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static bool TryParseTimestamp(
        string value,
        out DateTimeOffset timestamp)
    {
        value =
            value.Trim();

        if (DateTimeOffset.TryParseExact(
                value,
                SupportedTimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.AssumeLocal,
                out timestamp))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.AssumeLocal,
                out timestamp))
        {
            return true;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
        {
            timestamp =
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        dateTime,
                        DateTimeKind.Local));

            return true;
        }

        timestamp = default;

        return false;
    }

    private static string CleanDescription(
        string value)
    {
        return UnwrapOuterQuotes(value)
            .Replace(
                "\"\"",
                "\"",
                StringComparison.Ordinal)
            .Trim();
    }

    private static string UnwrapOuterQuotes(
        string value)
    {
        var trimmed =
            value.Trim();

        if (trimmed.Length >= 2 &&
            trimmed[0] == '"' &&
            trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static string SanitizeUrl(
        string value)
    {
        return value
            .Trim()
            .Trim(
                '"',
                '\'',
                '.',
                ',',
                ':',
                ';',
                ')',
                ']',
                '}');
    }

    private static string BuildRawContent(
        EventViewerRecord record)
    {
        return string.Join(
            "\t",
            record.Level,
            record.Timestamp.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            record.Source,
            record.EventId.ToString(
                CultureInfo.InvariantCulture),
            record.TaskCategory,
            record.Description);
    }

    [GeneratedRegex(
        @"(?im)^\s*Error Type\s*:\s*([A-Za-z_][A-Za-z0-9_.+`]*Exception)\s*$")]
    private static partial Regex ExplicitExceptionTypeRegex();

    [GeneratedRegex(
        @"\b(?:[A-Za-z_][A-Za-z0-9_.+`]*Exception)\b")]
    private static partial Regex ExceptionRegex();

    [GeneratedRegex(
        @"(?im)^\s*Error Status Code\s*:\s*(\d{1,3})\s*$")]
    private static partial Regex ExplicitStatusCodeRegex();

    [GeneratedRegex(
        @"""status""\s*:\s*([45]\d{2})",
        RegexOptions.IgnoreCase)]
    private static partial Regex JsonStatusRegex();

    [GeneratedRegex(
        @"Http failure response.+?:\s*([45]\d{2})\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex HttpFailureStatusRegex();

    [GeneratedRegex(
        @"(?im)^\s*Error Request Url\s*:\s*(https?://\S+)\s*$")]
    private static partial Regex RequestUrlRegex();

    [GeneratedRegex(
        @"""url""\s*:\s*""(https?://[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex JsonUrlRegex();

    [GeneratedRegex(
        @"https?://[^\s""']+",
        RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:Host Name|Server Name|Server)\s*:\s*([A-Za-z0-9._\-]+)\s*$")]
    private static partial Regex ServerNameRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:Machine Name|Machine|Computer)\s*:\s*([A-Za-z0-9._\-]+)\s*$")]
    private static partial Regex MachineNameRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:Environment|Env)\s*:\s*(DEV|QA|RQA\d*|UAT|PREPROD|PROD|PRODUCTION)\s*$")]
    private static partial Regex EnvironmentRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:User Name|Username|User|Login Name|LoginName)\s*:\s*([A-Za-z0-9._@\\\-]+)\s*$")]
    private static partial Regex UserNameRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:Correlation[-_ ]?Id|Request[-_ ]?Id|Trace[-_ ]?Id)\s*:\s*([A-Za-z0-9\-]+)\s*$")]
    private static partial Regex CorrelationIdRegex();

    private sealed class EventViewerRecord
    {
        public int LineNumber { get; init; }

        public string Level { get; init; } =
            string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public string Source { get; init; } =
            string.Empty;

        public int EventId { get; init; }

        public string TaskCategory { get; init; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;
    }
}