using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed partial class PlainTextLogParser : ILogParser
{
    public async Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var entries = new List<NormalizedLogEntry>();

        using var reader = new StreamReader(
    stream,
    System.Text.Encoding.UTF8,
    detectEncodingFromByteOrderMarks: false,
    bufferSize: 4096,
    leaveOpen: true);

        var lineNumber = 0;

        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineNumber++;

            var severity = DetectSeverity(line);
            var exceptionType = ExtractExceptionType(line);
            var statusCode = ExtractHttpStatusCode(line);
            var requestUrl = ExtractUrl(line);
            var timestamp = ExtractTimestamp(line);

            var isRelevant =
                severity is "Error" or "Critical" or "Warning" ||
                !string.IsNullOrWhiteSpace(exceptionType) ||
                statusCode >= 400;

            if (!isRelevant)
                continue;

            entries.Add(new NormalizedLogEntry
            {
                LineNumber = lineNumber,
                Timestamp = timestamp,
                Severity = severity,
                ExceptionType = exceptionType,
                Message = line.Trim(),
                HttpStatusCode = statusCode,
                RequestUrl = requestUrl,
                RawContent = line
            });
        }

        var summaries = entries
            .GroupBy(CreateSignature)
            .Select(group => new ErrorSummary
            {
                Signature = group.Key,
                Message = group.First().Message,
                ExceptionType = group.First().ExceptionType,
                HttpStatusCode = group.First().HttpStatusCode,
                OccurrenceCount = group.Count()
            })
            .OrderByDescending(item => item.OccurrenceCount)
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
            FirstTimestamp = timestamps.FirstOrDefault(),
            LastTimestamp = timestamps.LastOrDefault(),
            Entries = entries,
            ErrorSummaries = summaries
        };
    }

    private static string DetectSeverity(string line)
    {
        if (line.Contains(
                "critical",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "fatal",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        if (line.Contains(
                "error",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "exception",
                StringComparison.OrdinalIgnoreCase) ||
            HttpStatusRegex().IsMatch(line))
        {
            return "Error";
        }

        if (line.Contains(
                "warning",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "warn",
                StringComparison.OrdinalIgnoreCase))
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
        var match = HttpStatusRegex().Match(line);

        return match.Success &&
               int.TryParse(match.Groups[1].Value, out var statusCode)
            ? statusCode
            : null;
    }

    private static string ExtractUrl(string line)
    {
        var match = UrlRegex().Match(line);

        return match.Success
            ? match.Value
            : string.Empty;
    }

    private static string CreateSignature(
    NormalizedLogEntry entry)
    {
        var normalizedMessage = TimestampRegex().Replace(entry.Message, "{TIMESTAMP}");

        normalizedMessage = NumberRegex()
            .Replace(normalizedMessage, "{NUMBER}")
            .ToLowerInvariant()
            .Trim();

        return string.Join(
            "|",
            entry.ExceptionType,
            entry.HttpStatusCode,
            normalizedMessage);
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

    [GeneratedRegex(
        @"\b(?:[A-Za-z_][A-Za-z0-9_.]*Exception)\b")]
    private static partial Regex ExceptionRegex();

    [GeneratedRegex(@"\b([45]\d{2})\b")]
    private static partial Regex HttpStatusRegex();

    [GeneratedRegex(@"https?://[^\s""']+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b\d{5,}\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
    @"\b\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex TimestampRegex();
}