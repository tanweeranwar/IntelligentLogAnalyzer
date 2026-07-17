using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Intelligence;

public sealed partial class LogIncidentBuilder : ILogIncidentBuilder
{
    /*
     * Repeated errors with the same signature are considered part of
     * the same incident until there has been no occurrence for this
     * amount of time.
     */
    private static readonly TimeSpan IncidentInactivityWindow =
        TimeSpan.FromMinutes(30);

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
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return Array.Empty<LogIncident>();
        }

        var incidentGroups = entries
            .GroupBy(CreateIncidentSignature)
            .SelectMany(signatureGroup =>
                SplitByInactivityWindow(
                    signatureGroup
                        .OrderBy(entry =>
                            entry.Timestamp.HasValue
                                ? 0
                                : 1)
                        .ThenBy(entry =>
                            entry.Timestamp)
                        .ThenBy(entry =>
                            entry.LineNumber)
                        .ToArray()))
            .ToArray();

        var incidents = incidentGroups
            .Select(group =>
                CreateIncident(group))
            .OrderByDescending(incident =>
                incident.Intelligence.PriorityScore)
            .ThenByDescending(incident =>
                incident.EntryCount)
            .ThenBy(incident =>
                incident.StartedAt)
            .ToArray();

        return incidents
            .Select((incident, index) =>
                AssignIncidentId(
                    incident,
                    index + 1))
            .ToArray();
    }

    private static IReadOnlyCollection<
        IReadOnlyCollection<NormalizedLogEntry>>
        SplitByInactivityWindow(
            IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<
                IReadOnlyCollection<NormalizedLogEntry>>();
        }

        var groups =
            new List<IReadOnlyCollection<NormalizedLogEntry>>();

        var currentGroup =
            new List<NormalizedLogEntry>();

        NormalizedLogEntry? previousEntry =
            null;

        foreach (var entry in entries)
        {
            if (currentGroup.Count == 0)
            {
                currentGroup.Add(entry);
                previousEntry = entry;

                continue;
            }

            if (ShouldStartNewIncident(
                    previousEntry,
                    entry))
            {
                groups.Add(
                    currentGroup.ToArray());

                currentGroup =
                    new List<NormalizedLogEntry>();
            }

            currentGroup.Add(entry);
            previousEntry = entry;
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(
                currentGroup.ToArray());
        }

        return groups;
    }

    private static bool ShouldStartNewIncident(
        NormalizedLogEntry? previousEntry,
        NormalizedLogEntry currentEntry)
    {
        if (previousEntry is null)
        {
            return false;
        }

        /*
         * When timestamps are unavailable, entries with the same
         * signature remain grouped together.
         */
        if (!previousEntry.Timestamp.HasValue ||
            !currentEntry.Timestamp.HasValue)
        {
            return false;
        }

        var inactivityGap =
            currentEntry.Timestamp.Value -
            previousEntry.Timestamp.Value;

        return inactivityGap >
               IncidentInactivityWindow;
    }

    private LogIncident CreateIncident(
        IReadOnlyCollection<NormalizedLogEntry> group)
    {
        var orderedGroup = group
            .OrderBy(entry =>
                entry.Timestamp.HasValue
                    ? 0
                    : 1)
            .ThenBy(entry =>
                entry.Timestamp)
            .ThenBy(entry =>
                entry.LineNumber)
            .ToArray();

        var representativeEntry =
            GetRepresentativeEntry(orderedGroup);

        var timestampValues = orderedGroup
            .Where(entry =>
                entry.Timestamp.HasValue)
            .Select(entry =>
                entry.Timestamp!.Value)
            .OrderBy(timestamp =>
                timestamp)
            .ToArray();

        DateTimeOffset? startedAt =
            timestampValues.Length > 0
                ? timestampValues[0]
                : null;

        DateTimeOffset? endedAt =
            timestampValues.Length > 0
                ? timestampValues[^1]
                : null;

        var highestSeverity =
            GetHighestSeverity(orderedGroup);

        var intelligence =
            _incidentIntelligenceService.Analyze(
                representativeEntry.Message,
                representativeEntry.ExceptionType,
                representativeEntry.HttpStatusCode,
                orderedGroup.Length);

        return new LogIncident
        {
            IncidentId =
                string.Empty,

            Title =
                GetIncidentTitle(
                    representativeEntry),

            Signature =
                CreateIncidentSignature(
                    representativeEntry),

            StartedAt =
                startedAt,

            EndedAt =
                endedAt,

            Severity =
                highestSeverity,

            EntryCount =
                orderedGroup.Length,

            ExceptionType =
                GetFirstValue(
                    orderedGroup.Select(entry =>
                        entry.ExceptionType)),

            HttpStatusCode =
                GetFirstStatusCode(
                    orderedGroup),

            ApiPath =
                GetFirstValue(
                    orderedGroup.Select(entry =>
                        entry.ApiPath)),

            ServerName =
                GetFirstValue(
                    orderedGroup.Select(entry =>
                        string.IsNullOrWhiteSpace(
                            entry.ServerName)
                                ? entry.MachineName
                                : entry.ServerName)),

            Environment =
                GetFirstValue(
                    orderedGroup.Select(entry =>
                        entry.Environment)),

            CorrelationId =
                GetCorrelationValue(
                    orderedGroup),

            Entries =
                orderedGroup,

            Intelligence =
                intelligence
        };
    }

    private static LogIncident AssignIncidentId(
        LogIncident incident,
        int sequence)
    {
        return new LogIncident
        {
            IncidentId =
                $"INC-{sequence:0000}",

            Title =
                incident.Title,

            Signature =
                incident.Signature,

            StartedAt =
                incident.StartedAt,

            EndedAt =
                incident.EndedAt,

            Severity =
                incident.Severity,

            EntryCount =
                incident.EntryCount,

            ExceptionType =
                incident.ExceptionType,

            HttpStatusCode =
                incident.HttpStatusCode,

            ApiPath =
                incident.ApiPath,

            ServerName =
                incident.ServerName,

            Environment =
                incident.Environment,

            CorrelationId =
                incident.CorrelationId,

            Entries =
                incident.Entries,

            Intelligence =
                incident.Intelligence
        };
    }

    private static NormalizedLogEntry GetRepresentativeEntry(
        IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        return entries
            .OrderBy(entry =>
                GetSeverityRank(
                    entry.Severity))
            .ThenByDescending(entry =>
                entry.HttpStatusCode ?? 0)
            .ThenByDescending(entry =>
                !string.IsNullOrWhiteSpace(
                    entry.ExceptionType))
            .First();
    }

    private static string GetIncidentTitle(
        NormalizedLogEntry entry)
    {
        var message =
            entry.Message ??
            string.Empty;

        if (message.Contains(
                "signalr",
                StringComparison.OrdinalIgnoreCase) &&
            message.Contains(
                "disconnected",
                StringComparison.OrdinalIgnoreCase))
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

        if (!string.IsNullOrWhiteSpace(
                entry.ApiPath))
        {
            return $"{entry.ApiPath} failure";
        }

        return $"{entry.Severity} application incident";
    }

    private static string CreateIncidentSignature(
        NormalizedLogEntry entry)
    {
        var category =
            GetIncidentTitle(entry);

        var normalizedMessage =
            NormalizeMessage(
                entry.Message);

        var normalizedExceptionType =
            NormalizeValue(
                entry.ExceptionType);

        var normalizedApiPath =
            NormalizeApiPath(
                entry.ApiPath);

        var normalizedServer =
            NormalizeServerName(
                string.IsNullOrWhiteSpace(
                    entry.ServerName)
                        ? entry.MachineName
                        : entry.ServerName);

        var normalizedEnvironment =
            NormalizeValue(
                entry.Environment);

        var value =
            string.Join(
                "|",
                NormalizeValue(category),
                normalizedExceptionType,
                entry.HttpStatusCode?.ToString() ??
                string.Empty,
                normalizedApiPath,
                normalizedServer,
                normalizedEnvironment,
                normalizedMessage);

        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash);
    }

    private static string NormalizeMessage(
        string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var normalized =
            TimestampRegex().Replace(
                message,
                "{TIMESTAMP}");

        normalized =
            GuidRegex().Replace(
                normalized,
                "{GUID}");

        normalized =
            HexIdentifierRegex().Replace(
                normalized,
                "{HEX}");

        normalized =
            FileLineNumberRegex().Replace(
                normalized,
                ":line {LINE}");

        normalized =
            NumberRegex().Replace(
                normalized,
                "{NUMBER}");

        normalized =
            WhitespaceRegex().Replace(
                normalized,
                " ");

        return normalized
            .Trim()
            .ToLowerInvariant();
    }

    private static string NormalizeApiPath(
        string? apiPath)
    {
        if (string.IsNullOrWhiteSpace(apiPath))
        {
            return string.Empty;
        }

        var normalized =
            QueryStringRegex().Replace(
                apiPath,
                string.Empty);

        normalized =
            GuidRegex().Replace(
                normalized,
                "{GUID}");

        normalized =
            LongPathNumberRegex().Replace(
                normalized,
                "/{ID}");

        normalized =
            RepeatedSlashRegex().Replace(
                normalized,
                "/");

        return normalized
            .Trim()
            .TrimEnd('/')
            .ToLowerInvariant();
    }

    private static string NormalizeServerName(
        string? serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return string.Empty;
        }

        return serverName
            .Trim()
            .Split('.')[0]
            .ToUpperInvariant();
    }

    private static string NormalizeValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()
                .ToLowerInvariant();
    }

    private static string GetHighestSeverity(
        IEnumerable<NormalizedLogEntry> entries)
    {
        return entries
            .Select(entry =>
                entry.Severity)
            .OrderBy(GetSeverityRank)
            .FirstOrDefault() ??
            "Information";
    }

    private static int GetSeverityRank(
        string? severity)
    {
        return severity?.ToUpperInvariant() switch
        {
            "CRITICAL" => 0,
            "ERROR" => 1,
            "WARNING" => 2,
            _ => 3
        };
    }

    private static int? GetFirstStatusCode(
        IEnumerable<NormalizedLogEntry> entries)
    {
        return entries
            .Where(entry =>
                entry.HttpStatusCode.HasValue)
            .Select(entry =>
                entry.HttpStatusCode)
            .FirstOrDefault();
    }

    private static string GetCorrelationValue(
        IReadOnlyCollection<NormalizedLogEntry> entries)
    {
        var correlationIds = entries
            .Select(entry =>
                entry.CorrelationId)
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        return correlationIds.Length switch
        {
            0 => string.Empty,
            1 => correlationIds[0],
            _ => "Multiple"
        };
    }

    private static string GetFirstValue(
        IEnumerable<string> values)
    {
        return values.FirstOrDefault(value =>
                   !string.IsNullOrWhiteSpace(value))
               ?? string.Empty;
    }

    [GeneratedRegex(
        @"\b\d{1,4}[-/.]\d{1,2}[-/.]\d{1,4}[ T]\d{1,2}[:.]\d{2}[:.]\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        @"\b(?=[0-9A-Fa-f]{10,}\b)(?=.*[A-Fa-f])[0-9A-Fa-f]+\b")]
    private static partial Regex HexIdentifierRegex();

    [GeneratedRegex(
        @":line\s+\d+",
        RegexOptions.IgnoreCase)]
    private static partial Regex FileLineNumberRegex();

    [GeneratedRegex(
        @"\b\d+\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
        @"\?.*$")]
    private static partial Regex QueryStringRegex();

    [GeneratedRegex(
        @"/\d{4,}(?=/|$)")]
    private static partial Regex LongPathNumberRegex();

    [GeneratedRegex(
        @"/{2,}")]
    private static partial Regex RepeatedSlashRegex();

    [GeneratedRegex(
        @"\s+")]
    private static partial Regex WhitespaceRegex();
}