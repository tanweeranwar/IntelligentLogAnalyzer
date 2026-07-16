using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.EventBuilders;

public sealed partial class MultilineLogEventBuilder
    : IRawLogEventBuilder
{
    public async Task<IReadOnlyCollection<RawLogEvent>> BuildAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var events = new List<RawLogEvent>();

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        StringBuilder? currentContent = null;
        string currentPrimaryLine = string.Empty;
        var currentStartLine = 0;
        var currentEndLine = 0;
        var lineNumber = 0;

        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineNumber++;

            var startsNewEvent =
                StartsWithTimestamp(line) ||
                StartsWithStructuredLogLevel(line);

            if (startsNewEvent)
            {
                AddCurrentEvent(
                    events,
                    currentContent,
                    currentPrimaryLine,
                    currentStartLine,
                    currentEndLine);

                currentContent = new StringBuilder();
                currentPrimaryLine = line;
                currentStartLine = lineNumber;
                currentEndLine = lineNumber;

                currentContent.AppendLine(line);

                continue;
            }

            if (currentContent is not null)
            {
                currentContent.AppendLine(line);
                currentEndLine = lineNumber;

                continue;
            }

            // Preserve non-empty lines that appear before the first
            // timestamped event.
            if (!string.IsNullOrWhiteSpace(line))
            {
                currentContent = new StringBuilder();
                currentPrimaryLine = line;
                currentStartLine = lineNumber;
                currentEndLine = lineNumber;

                currentContent.AppendLine(line);
            }
        }

        AddCurrentEvent(
            events,
            currentContent,
            currentPrimaryLine,
            currentStartLine,
            currentEndLine);

        return events;
    }

    private static void AddCurrentEvent(
        ICollection<RawLogEvent> events,
        StringBuilder? content,
        string primaryLine,
        int startLine,
        int endLine)
    {
        if (content is null ||
            content.Length == 0 ||
            startLine <= 0)
        {
            return;
        }

        events.Add(new RawLogEvent
        {
            StartLineNumber = startLine,
            EndLineNumber = endLine,
            PrimaryLine = primaryLine,
            RawContent = content
                .ToString()
                .TrimEnd()
        });
    }

    private static bool StartsWithTimestamp(string line)
    {
        return TimestampAtStartRegex().IsMatch(line);
    }

    private static bool StartsWithStructuredLogLevel(
        string line)
    {
        return LogLevelAtStartRegex().IsMatch(line);
    }

    [GeneratedRegex(
        @"^\s*[\[\(]?\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?")]
    private static partial Regex TimestampAtStartRegex();

    [GeneratedRegex(
        @"^\s*(?:CRITICAL|FATAL|ERROR|ERR|WARNING|WARN|INFO|DEBUG|TRACE)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LogLevelAtStartRegex();
}