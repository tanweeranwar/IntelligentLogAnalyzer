using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.AI;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Investigation;

public sealed class InvestigationEvidenceBuilder
    : IInvestigationEvidenceBuilder
{
    public InvestigationRequest Build(
    LogIncident incident,
    ApplicationContextResult context,
    IReadOnlyCollection<ErrorSummary> errorPatterns,
    string question = "",
    int maxEvidenceEntries = 10,
    InvestigationMode mode = InvestigationMode.Deep)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(errorPatterns);

        var evidenceLimit =
            Math.Clamp(
                maxEvidenceEntries,
                1,
                25);

        var evidence =
            SelectRepresentativeEvidence(
                incident.Entries,
                evidenceLimit);

        var relevantPatterns =
            errorPatterns
                .Where(pattern =>
                    MatchesIncident(
                        pattern,
                        incident))
                .OrderByDescending(pattern =>
                    pattern.OccurrenceCount)
                .Take(10)
                .ToArray();

        return new InvestigationRequest
        {
            Incident = incident,
            Context = context,
            Evidence = evidence,
            ErrorPatterns = relevantPatterns,
            Question = question,
            Application = context.ApplicationName,
            Environment =
                !string.IsNullOrWhiteSpace(
                    incident.Environment)
                    ? incident.Environment
                    : evidence
                        .Select(entry =>
                            entry.Environment)
                        .FirstOrDefault(value =>
                            !string.IsNullOrWhiteSpace(value))
                      ?? string.Empty,
            MaxEvidenceEntries = evidenceLimit,
            Mode = mode
        };
    }

    private static IReadOnlyCollection<NormalizedLogEntry>
        SelectRepresentativeEvidence(
            IReadOnlyCollection<NormalizedLogEntry> entries,
            int maxEntries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<NormalizedLogEntry>();
        }

        var orderedEntries =
            entries
                .OrderBy(entry =>
                    entry.Timestamp)
                .ThenBy(entry =>
                    entry.LineNumber)
                .ToArray();

        var selected =
            new List<NormalizedLogEntry>();

        AddIfAvailable(
            selected,
            orderedEntries.FirstOrDefault());

        AddIfAvailable(
            selected,
            orderedEntries.LastOrDefault());

        AddIfAvailable(
            selected,
            orderedEntries.FirstOrDefault(entry =>
                !string.IsNullOrWhiteSpace(
                    entry.StackTrace)));

        AddIfAvailable(
            selected,
            orderedEntries.FirstOrDefault(entry =>
                entry.HttpStatusCode is >= 500));

        AddIfAvailable(
            selected,
            orderedEntries.FirstOrDefault(entry =>
                entry.Severity.Equals(
                    "Critical",
                    StringComparison.OrdinalIgnoreCase)));

        AddIfAvailable(
            selected,
            orderedEntries.FirstOrDefault(entry =>
                !string.IsNullOrWhiteSpace(
                    entry.CorrelationId)));

        var groupedByMessage =
            orderedEntries
                .GroupBy(entry =>
                    CreateEvidenceSignature(entry))
                .OrderByDescending(group =>
                    group.Count());

        foreach (var group in groupedByMessage)
        {
            if (selected.Count >= maxEntries)
            {
                break;
            }

            AddIfAvailable(
                selected,
                group.First());
        }

        if (selected.Count < maxEntries)
        {
            foreach (var entry in orderedEntries)
            {
                if (selected.Count >= maxEntries)
                {
                    break;
                }

                AddIfAvailable(
                    selected,
                    entry);
            }
        }

        return selected
            .OrderBy(entry =>
                entry.Timestamp)
            .ThenBy(entry =>
                entry.LineNumber)
            .Take(maxEntries)
            .ToArray();
    }

    private static void AddIfAvailable(
        ICollection<NormalizedLogEntry> selected,
        NormalizedLogEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var alreadySelected =
            selected.Any(existing =>
                existing.LineNumber ==
                    entry.LineNumber &&
                existing.Timestamp ==
                    entry.Timestamp &&
                string.Equals(
                    existing.Message,
                    entry.Message,
                    StringComparison.Ordinal));

        if (!alreadySelected)
        {
            selected.Add(entry);
        }
    }

    private static bool MatchesIncident(
        ErrorSummary pattern,
        LogIncident incident)
    {
        if (!string.IsNullOrWhiteSpace(
                pattern.ExceptionType) &&
            !string.Equals(
                pattern.ExceptionType,
                incident.ExceptionType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (pattern.HttpStatusCode.HasValue &&
            incident.HttpStatusCode.HasValue &&
            pattern.HttpStatusCode.Value !=
                incident.HttpStatusCode.Value)
        {
            return false;
        }

        return true;
    }

    private static string CreateEvidenceSignature(
        NormalizedLogEntry entry)
    {
        return string.Join(
            "|",
            entry.ExceptionType?.Trim()
                .ToLowerInvariant() ??
            string.Empty,

            entry.HttpStatusCode?.ToString() ??
            string.Empty,

            entry.ApiPath?.Trim()
                .ToLowerInvariant() ??
            string.Empty,

            entry.Message?.Trim()
                .ToLowerInvariant() ??
            string.Empty);
    }
}