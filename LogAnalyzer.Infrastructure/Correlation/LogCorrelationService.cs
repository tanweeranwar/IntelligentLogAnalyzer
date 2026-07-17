using System.Security.Cryptography;
using System.Text;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Correlation;

public sealed class LogCorrelationService : ILogCorrelationService
{
    private static readonly TimeSpan CorrelationWindow =
        TimeSpan.FromMinutes(2);

    public IReadOnlyCollection<NormalizedLogEntry> Correlate(
        IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<NormalizedLogEntry>();
        }

        var orderedEntries = entries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.LineNumber)
            .ToArray();

        return orderedEntries
            .Select(entry => CorrelateEntry(entry, orderedEntries))
            .ToArray();
    }

    private static NormalizedLogEntry CorrelateEntry(
        NormalizedLogEntry entry,
        IReadOnlyCollection<NormalizedLogEntry> allEntries)
    {
        var candidates = allEntries
            .Where(candidate =>
                candidate.LineNumber != entry.LineNumber &&
                IsWithinCorrelationWindow(entry, candidate))
            .Select(candidate => new
            {
                Entry = candidate,
                Score = CalculateMatchScore(entry, candidate)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item =>
                GetLineDistance(entry, item.Entry))
            .ToArray();

        var strongestCandidate =
            candidates.FirstOrDefault();

        var correlationId = FirstValue(
            entry.CorrelationId,
            strongestCandidate?.Entry.CorrelationId);

        var apiPath = FirstValue(
            entry.ApiPath,
            strongestCandidate?.Entry.ApiPath);

        var requestUrl = FirstValue(
            entry.RequestUrl,
            strongestCandidate?.Entry.RequestUrl);

        var serverName = FirstValue(
            entry.ServerName,
            strongestCandidate?.Entry.ServerName);

        var machineName = FirstValue(
            entry.MachineName,
            strongestCandidate?.Entry.MachineName);

        var environment = FirstValue(
            entry.Environment,
            strongestCandidate?.Entry.Environment);

        var userName = FirstValue(
            entry.UserName,
            strongestCandidate?.Entry.UserName);

        var confidence = strongestCandidate?.Score ?? 0;

        var correlationGroup = CreateCorrelationGroup(
            correlationId,
            apiPath,
            serverName,
            machineName,
            environment,
            entry.ExceptionType,
            entry.HttpStatusCode);

        var relatedLines = candidates
            .Where(item => item.Score >= 40)
            .Take(20)
            .Select(item => item.Entry.LineNumber)
            .OrderBy(line => line)
            .ToArray();

        return new NormalizedLogEntry
        {
            LineNumber = entry.LineNumber,
            Timestamp = entry.Timestamp,
            Severity = entry.Severity,
            ExceptionType = entry.ExceptionType,
            Message = entry.Message,
            HttpStatusCode = entry.HttpStatusCode,
            RequestUrl = requestUrl,
            ApiPath = apiPath,
            CorrelationId = correlationId,
            ServerName = serverName,
            MachineName = machineName,
            Environment = environment,
            UserName = userName,
            StackTrace = entry.StackTrace,
            CorrelationGroup = correlationGroup,
            CorrelationConfidence = confidence,
            RelatedLineNumbers = relatedLines,
            RawContent = entry.RawContent
        };
    }

    private static int CalculateMatchScore(
        NormalizedLogEntry source,
        NormalizedLogEntry candidate)
    {
        var score = 0;

        if (HasEqualValue(
                source.CorrelationId,
                candidate.CorrelationId))
        {
            score += 100;
        }

        if (HasEqualValue(
                source.ApiPath,
                candidate.ApiPath))
        {
            score += 35;
        }

        if (HasEqualValue(
                GetServer(source),
                GetServer(candidate)))
        {
            score += 20;
        }

        if (HasEqualValue(
                source.Environment,
                candidate.Environment))
        {
            score += 10;
        }

        if (HasEqualValue(
                source.UserName,
                candidate.UserName))
        {
            score += 15;
        }

        if (HasEqualValue(
                source.ExceptionType,
                candidate.ExceptionType))
        {
            score += 25;
        }

        if (source.HttpStatusCode.HasValue &&
            source.HttpStatusCode ==
            candidate.HttpStatusCode)
        {
            score += 20;
        }

        var lineDistance =
            GetLineDistance(source, candidate);

        if (lineDistance <= 5)
        {
            score += 20;
        }
        else if (lineDistance <= 20)
        {
            score += 10;
        }

        return Math.Min(score, 100);
    }

    private static bool IsWithinCorrelationWindow(
        NormalizedLogEntry first,
        NormalizedLogEntry second)
    {
        if (first.Timestamp.HasValue &&
            second.Timestamp.HasValue)
        {
            var difference = first.Timestamp.Value -
                             second.Timestamp.Value;

            return difference.Duration() <=
                   CorrelationWindow;
        }

        return GetLineDistance(first, second) <= 20;
    }

    private static int GetLineDistance(
        NormalizedLogEntry first,
        NormalizedLogEntry second)
    {
        return Math.Abs(
            first.LineNumber -
            second.LineNumber);
    }

    private static bool HasEqualValue(
        string? first,
        string? second)
    {
        return
            !string.IsNullOrWhiteSpace(first) &&
            !string.IsNullOrWhiteSpace(second) &&
            string.Equals(
                first,
                second,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstValue(
        string? primary,
        string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return fallback ?? string.Empty;
    }

    private static string GetServer(
        NormalizedLogEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.ServerName)
            ? entry.ServerName
            : entry.MachineName;
    }

    private static string CreateCorrelationGroup(
        string correlationId,
        string apiPath,
        string serverName,
        string machineName,
        string environment,
        string exceptionType,
        int? httpStatusCode)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        var value = string.Join(
            "|",
            apiPath,
            FirstValue(serverName, machineName),
            environment,
            exceptionType,
            httpStatusCode);

        if (string.IsNullOrWhiteSpace(
                value.Replace("|", string.Empty)))
        {
            return string.Empty;
        }

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return Convert
            .ToHexString(hash)[..12];
    }
}