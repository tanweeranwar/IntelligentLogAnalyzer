using System.Text.RegularExpressions;
using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Infrastructure.AI;

internal sealed partial class InvestigationEvidenceDistiller
{
    private const int MaximumFacts = 24;
    private const int MaximumValueLength = 500;

    public DistilledEvidence Distill(
        ReasoningPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var facts =
            new List<EvidenceFact>();

        AddMetadataFacts(
            facts,
            package);

        AddKnownLabels(
            facts,
            package.InvestigationContext);

        AddSectionFacts(
            facts,
            package.InvestigationContext);

        AddExceptionFacts(
            facts,
            package.InvestigationContext);

        AddDatabaseFacts(
            facts,
            package.InvestigationContext);

        AddStackFacts(
            facts,
            package.InvestigationContext);

        AddTimingFacts(
            facts,
            package.InvestigationContext);

        AddFailurePatternFacts(
            facts,
            package.InvestigationContext);

        var distilled =
            facts
                .Where(fact =>
                    !string.IsNullOrWhiteSpace(
                        fact.Value))
                .GroupBy(
                    fact =>
                        $"{fact.Category}|{fact.Label}|{fact.Value}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group
                        .OrderByDescending(fact =>
                            fact.Priority)
                        .First())
                .OrderByDescending(fact =>
                    fact.Priority)
                .ThenBy(fact =>
                    fact.Category,
                    StringComparer.OrdinalIgnoreCase)
                .Take(MaximumFacts)
                .ToArray();

        return new DistilledEvidence
        {
            Facts =
                distilled
        };
    }

    private static void AddMetadataFacts(
        ICollection<EvidenceFact> facts,
        ReasoningPackage package)
    {
        AddMetadataFact(
            facts,
            package,
            "IncidentId",
            "Incident",
            "Incident ID",
            100);

        AddMetadataFact(
            facts,
            package,
            "Application",
            "Identity",
            "Application",
            95);

        AddMetadataFact(
            facts,
            package,
            "Environment",
            "Identity",
            "Environment",
            95);

        AddMetadataFact(
            facts,
            package,
            "EvidenceCount",
            "Scope",
            "Evidence count",
            80);

        AddMetadataFact(
            facts,
            package,
            "ErrorPatternCount",
            "Scope",
            "Error pattern count",
            80);

        AddMetadataFact(
            facts,
            package,
            "ContextConfidence",
            "Context",
            "Context confidence",
            70);
    }

    private static void AddMetadataFact(
        ICollection<EvidenceFact> facts,
        ReasoningPackage package,
        string key,
        string category,
        string label,
        int priority)
    {
        if (!package.Metadata.TryGetValue(
                key,
                out var value))
        {
            return;
        }

        if (IsIgnoredValue(value))
        {
            return;
        }

        facts.Add(
            CreateFact(
                category,
                label,
                value,
                priority));
    }

    private static void AddKnownLabels(
        ICollection<EvidenceFact> facts,
        string content)
    {
        AddLabel(
            facts,
            content,
            "Exception:",
            "Failure",
            "Exception",
            100);

        AddLabel(
            facts,
            content,
            "Message:",
            "Failure",
            "Message",
            100);

        AddLabel(
            facts,
            content,
            "Occurrence Count:",
            "Scope",
            "Occurrences",
            95);

        AddLabel(
            facts,
            content,
            "API Path:",
            "Application",
            "API",
            90);

        AddLabel(
            facts,
            content,
            "Server:",
            "Infrastructure",
            "Server",
            85);

        AddLabel(
            facts,
            content,
            "Correlation ID:",
            "Request",
            "Correlation ID",
            95);

        AddLabel(
            facts,
            content,
            "Timestamp:",
            "Timeline",
            "Timestamp",
            75);
    }

    private static void AddLabel(
        ICollection<EvidenceFact> facts,
        string content,
        string prefix,
        string category,
        string label,
        int priority)
    {
        var values =
            content
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(line =>
                    line.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                .Select(line =>
                    line[prefix.Length..].Trim())
                .Where(value =>
                    !IsIgnoredValue(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(5);

        foreach (var value in values)
        {
            facts.Add(
                CreateFact(
                    category,
                    label,
                    value,
                    priority));
        }
    }

    private static void AddSectionFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        AddBulletSection(
            facts,
            content,
            "Matched Components:",
            "Application",
            "Component",
            85);

        AddBulletSection(
            facts,
            content,
            "Matched Workflows:",
            "Application",
            "Workflow",
            90);

        AddBulletSection(
            facts,
            content,
            "Dependencies:",
            "Dependency",
            "Dependency",
            85);

        AddBulletSection(
            facts,
            content,
            "Database Objects:",
            "Database",
            "Database object",
            90);

        AddBulletSection(
            facts,
            content,
            "Matched Known Issues:",
            "Knowledge",
            "Known issue",
            75);

        AddBulletSection(
            facts,
            content,
            "Investigation Hints:",
            "Knowledge",
            "Investigation hint",
            65);
    }

    private static void AddBulletSection(
        ICollection<EvidenceFact> facts,
        string content,
        string header,
        string category,
        string label,
        int priority)
    {
        var lines =
            content.Split(
                ['\r', '\n'],
                StringSplitOptions.None);

        var collecting =
            false;

        foreach (var rawLine in lines)
        {
            var line =
                rawLine.Trim();

            if (line.Equals(
                    header,
                    StringComparison.OrdinalIgnoreCase))
            {
                collecting =
                    true;

                continue;
            }

            if (!collecting)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith(
                    "- ",
                    StringComparison.Ordinal))
            {
                if (LooksLikeSectionHeading(line))
                {
                    break;
                }

                continue;
            }

            var value =
                line[2..].Trim();

            if (IsIgnoredValue(value))
            {
                continue;
            }

            facts.Add(
                CreateFact(
                    category,
                    label,
                    value,
                    priority));

            if (facts.Count >= 100)
            {
                return;
            }
        }
    }

    private static void AddExceptionFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        foreach (Match match in
                 ExceptionRegex().Matches(content))
        {
            var value =
                match.Groups["value"]
                    .Value;

            if (IsIgnoredValue(value))
            {
                continue;
            }

            facts.Add(
                CreateFact(
                    "Failure",
                    "Observed exception",
                    value,
                    100));
        }
    }

    private static void AddDatabaseFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        foreach (Match match in
                 DbContextRegex().Matches(content))
        {
            facts.Add(
                CreateFact(
                    "Database",
                    "DbContext",
                    match.Groups["value"].Value,
                    95));
        }

        foreach (Match match in
                 CommandTimeoutRegex().Matches(content))
        {
            facts.Add(
                CreateFact(
                    "Database",
                    "Command timeout seconds",
                    match.Groups["value"].Value,
                    100));
        }

        foreach (Match match in
                 SqlErrorRegex().Matches(content))
        {
            facts.Add(
                CreateFact(
                    "Database",
                    "SQL error number",
                    match.Groups["value"].Value,
                    100));
        }

        foreach (Match match in
                 SqlOperationRegex().Matches(content))
        {
            var value =
                match.Groups["value"]
                    .Value;

            if (IsSqlNoise(value))
            {
                continue;
            }

            facts.Add(
                CreateFact(
                    "Database",
                    "Database operation",
                    value,
                    95));
        }
    }

    private static void AddStackFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        var frames =
            StackFrameRegex()
                .Matches(content)
                .Select(match =>
                    match.Groups["value"]
                        .Value.Trim())
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Where(value =>
                    !value.StartsWith(
                        "System.Threading",
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(8);

        foreach (var frame in frames)
        {
            facts.Add(
                CreateFact(
                    "Stack",
                    "Frame",
                    frame,
                    85));
        }
    }

    private static void AddTimingFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        foreach (Match match in
                 DurationRegex().Matches(content))
        {
            facts.Add(
                CreateFact(
                    "Timing",
                    "Observed duration",
                    match.Groups["value"].Value,
                    90));
        }

        if (content.Contains(
                "Execution Timeout Expired",
                StringComparison.OrdinalIgnoreCase))
        {
            facts.Add(
                CreateFact(
                    "Failure",
                    "Failure pattern",
                    "Execution Timeout Expired",
                    100));
        }

        if (content.Contains(
                "The wait operation timed out",
                StringComparison.OrdinalIgnoreCase))
        {
            facts.Add(
                CreateFact(
                    "Failure",
                    "Failure pattern",
                    "The wait operation timed out",
                    95));
        }
    }

    private static void AddFailurePatternFacts(
        ICollection<EvidenceFact> facts,
        string content)
    {
        var messages =
            content
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(line =>
                    line.StartsWith(
                        "Message:",
                        StringComparison.OrdinalIgnoreCase))
                .Select(line =>
                    line["Message:".Length..]
                        .Trim())
                .Where(value =>
                    !IsIgnoredValue(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(5);

        foreach (var message in messages)
        {
            facts.Add(
                CreateFact(
                    "Failure",
                    "Representative message",
                    message,
                    90));
        }
    }

    private static EvidenceFact CreateFact(
        string category,
        string label,
        string value,
        int priority)
    {
        return new EvidenceFact
        {
            Category =
                category,

            Label =
                label,

            Value =
                Truncate(
                    value),

            Priority =
                priority
        };
    }

    private static bool LooksLikeSectionHeading(
        string value)
    {
        return value.EndsWith(
                   ":",
                   StringComparison.Ordinal) ||
               value.All(character =>
                   !char.IsLetter(character) ||
                   char.IsUpper(character) ||
                   char.IsWhiteSpace(character) ||
                   character is '_' or '-');
    }

    private static bool IsIgnoredValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.Equals(
                   "Unknown",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "None matched",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "Not detected",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSqlNoise(
        string value)
    {
        return value.Equals(
                   "SELECT",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "WHERE",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "FROM",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "ORDER",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(
        string value)
    {
        var normalized =
            value
                .Replace(
                    "\r",
                    " ",
                    StringComparison.Ordinal)
                .Replace(
                    "\n",
                    " ",
                    StringComparison.Ordinal)
                .Trim();

        return normalized.Length <=
               MaximumValueLength
            ? normalized
            : normalized[..MaximumValueLength] +
              "...";
    }

    [GeneratedRegex(
        @"\b(?<value>[A-Za-z_][A-Za-z0-9_.]*Exception)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExceptionRegex();

    [GeneratedRegex(
        @"\b(?<value>[A-Za-z_][A-Za-z0-9_.]*DbContext)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DbContextRegex();

    [GeneratedRegex(
        @"CommandTimeout\s*=\s*'?""?(?<value>\d+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex CommandTimeoutRegex();

    [GeneratedRegex(
        @"Error Number\s*:\s*(?<value>-?\d+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlErrorRegex();

    [GeneratedRegex(
        @"\b(?<value>(?:dbo\.)?[A-Za-z_][A-Za-z0-9_]{3,})\s+@",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlOperationRegex();

    [GeneratedRegex(
        @"\bat\s+(?<value>[A-Za-z_][A-Za-z0-9_.`<>]+\.[A-Za-z_][A-Za-z0-9_<>]+(?:\([^)]*\))?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex StackFrameRegex();

    [GeneratedRegex(
        @"Failed executing DbCommand\s*\(""(?<value>[\d,]+ms)""\)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();
}