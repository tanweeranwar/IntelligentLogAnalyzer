using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Intelligence;

public sealed partial class LogIncidentBuilder : ILogIncidentBuilder
{
    private static readonly TimeSpan IncidentWindow =
        TimeSpan.FromMinutes(5);

    private readonly IIncidentIntelligenceService
        _incidentIntelligenceService;

    public LogIncidentBuilder(
        IIncidentIntelligenceService incidentIntelligenceService)
    {
        _incidentIntelligenceService =
            incidentIntelligenceService;
    }

    public IReadOnlyCollection<LogIncident> Build(
        IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<LogIncident>();
        }

        var orderedEntries = entries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.LineNumber)
            .ToArray();

        var groups = new List<List<NormalizedLogEntry>>();

        foreach (var entry in orderedEntries)
        {
            var signature = CreateIncidentSignature(entry);

            var matchingGroup = groups
                .LastOrDefault(group =>
                    BelongsToIncident(
                        group,
                        entry,
                        signature));

            if (matchingGroup is null)
            {
                groups.Add([entry]);
            }
            else
            {
                matchingGroup.Add(entry);
            }
        }

        return groups
            .Select((group, index) =>
                CreateIncident(group, index + 1))
            .OrderByDescending(
                incident =>
                    incident.Intelligence.PriorityScore)
            .ThenByDescending(
                incident => incident.EntryCount)
            .ToArray();
    }

    private static bool BelongsToIncident(
        IReadOnlyCollection<NormalizedLogEntry> group,
        NormalizedLogEntry candidate,
        string candidateSignature)
    {
        var first = group.First();
        var last = group.Last();

        var groupSignature =
            CreateIncidentSignature(first);

        if (!string.Equals(
                groupSignature,
                candidateSignature,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!candidate.Timestamp.HasValue ||
            !last.Timestamp.HasValue)
        {
            return true;
        }

        return candidate.Timestamp.Value -
               last.Timestamp.Value <= IncidentWindow;
    }

    private LogIncident CreateIncident(
        IReadOnlyCollection<NormalizedLogEntry> group,
        int sequence)
    {
        var first = group.First();

        var startedAt = group
            .Where(entry => entry.Timestamp.HasValue)
            .Select(entry => entry.Timestamp)
            .Min();

        var endedAt = group
            .Where(entry => entry.Timestamp.HasValue)
            .Select(entry => entry.Timestamp)
            .Max();

        var intelligence =
            _incidentIntelligenceService.Analyze(
                first.Message,
                first.ExceptionType,
                first.HttpStatusCode,
                group.Count);

        return new LogIncident
        {
            IncidentId = $"INC-{sequence:0000}",
            Title = GetIncidentTitle(first),
            Signature = CreateIncidentSignature(first),
            StartedAt = startedAt,
            EndedAt = endedAt,
            Severity = GetHighestSeverity(group),
            EntryCount = group.Count,
            ExceptionType = first.ExceptionType,
            HttpStatusCode = first.HttpStatusCode,
            ApiPath = first.ApiPath,
            ServerName = GetFirstValue(
                group.Select(entry =>
                    string.IsNullOrWhiteSpace(entry.ServerName)
                        ? entry.MachineName
                        : entry.ServerName)),
            Environment = GetFirstValue(
                group.Select(entry => entry.Environment)),
            CorrelationId = GetFirstValue(
                group.Select(entry => entry.CorrelationId)),
            Entries = group.ToArray(),
            Intelligence = intelligence
        };
    }

    private static string GetIncidentTitle(
        NormalizedLogEntry entry)
    {
        var text = entry.Message.ToLowerInvariant();

        if (text.Contains("signalr") &&
            text.Contains("disconnected"))
        {
            return "SignalR connection failure";
        }

        if (!string.IsNullOrWhiteSpace(
                entry.ExceptionType))
        {
            return entry.ExceptionType;
        }

        if (entry.HttpStatusCode.HasValue)
        {
            return $"HTTP {entry.HttpStatusCode.Value}";
        }

        if (!string.IsNullOrWhiteSpace(entry.ApiPath))
        {
            return $"{entry.ApiPath} failure";
        }

        return $"{entry.Severity} application incident";
    }

    private static string CreateIncidentSignature(
        NormalizedLogEntry entry)
    {
        var category = GetIncidentTitle(entry);

        var normalizedMessage = TimestampRegex()
            .Replace(entry.Message, "{TIMESTAMP}");

        normalizedMessage = GuidRegex()
            .Replace(normalizedMessage, "{GUID}");

        normalizedMessage = NumberRegex()
            .Replace(normalizedMessage, "{NUMBER}");

        normalizedMessage = WhitespaceRegex()
            .Replace(normalizedMessage, " ")
            .Trim()
            .ToLowerInvariant();

        var value = string.Join(
            "|",
            category,
            entry.ExceptionType,
            entry.HttpStatusCode,
            entry.ApiPath,
            normalizedMessage);

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash);
    }

    private static string GetHighestSeverity(
        IEnumerable<NormalizedLogEntry> entries)
    {
        var severities = entries
            .Select(entry => entry.Severity)
            .ToArray();

        if (severities.Contains("Critical"))
        {
            return "Critical";
        }

        if (severities.Contains("Error"))
        {
            return "Error";
        }

        if (severities.Contains("Warning"))
        {
            return "Warning";
        }

        return "Information";
    }

    private static string GetFirstValue(
        IEnumerable<string> values)
    {
        return values.FirstOrDefault(
                   value =>
                       !string.IsNullOrWhiteSpace(value))
               ?? string.Empty;
    }

    [GeneratedRegex(
        @"\b\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}