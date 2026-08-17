using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.KnowledgeSources;

public sealed class LogKnowledgeSource
    : IApplicationKnowledgeSource
{
    private readonly IApplicationCatalog _catalog;

    public LogKnowledgeSource(
        IApplicationCatalog catalog)
    {
        _catalog =
            catalog ??
            throw new ArgumentNullException(
                nameof(catalog));
    }

    public string SourceName =>
        "Log Evidence";

    public KnowledgeSourceKind SourceKind =>
        KnowledgeSourceKind.Log;

    public Task<KnowledgeSourceResult> DiscoverAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch =
            Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        var evidence =
            request.Evidence
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item))
                .ToArray();

        if (evidence.Length == 0)
        {
            stopwatch.Stop();

            return Task.FromResult(
                new KnowledgeSourceResult
                {
                    SourceName =
                        SourceName,

                    SourceKind =
                        SourceKind,

                    Duration =
                        stopwatch.Elapsed
                });
        }

        var combinedText =
            string.Join(
                Environment.NewLine,
                evidence);

        var facts =
            DiscoverFacts(
                combinedText);

        var identityContributions =
            MatchApplications(
                combinedText,
                facts);

        var technicalContributions =
            facts
                .Select(CreateContribution)
                .ToArray();

        var contributions =
            identityContributions
                .Concat(technicalContributions)
                .Where(contribution =>
                    !string.IsNullOrWhiteSpace(
                        contribution.Value))
                .ToArray();

        stopwatch.Stop();

        return Task.FromResult(
            new KnowledgeSourceResult
            {
                SourceName =
                    SourceName,

                SourceKind =
                    SourceKind,

                Contributions =
                    contributions,

                Duration =
                    stopwatch.Elapsed
            });
    }

    private IReadOnlyCollection<KnowledgeContribution>
        MatchApplications(
            string logText,
            IReadOnlyCollection<LogDiscoveryFact> facts)
    {
        var results =
            new List<KnowledgeContribution>();

        foreach (var package in _catalog.GetAll())
        {
            var metadata =
                package.Application.Metadata;

            foreach (var fingerprint in
                     package.Application.Fingerprints)
            {
                if (!IsFingerprintMatch(
                        fingerprint,
                        logText,
                        facts))
                {
                    continue;
                }

                results.Add(
                    new KnowledgeContribution
                    {
                        ApplicationId =
                            metadata.ApplicationId,

                        ApplicationName =
                            metadata.ApplicationName,

                        Type =
                            KnowledgeContributionType
                                .ApplicationIdentity,

                        Key =
                            fingerprint.Id,

                        Value =
                            metadata.ApplicationName,

                        ConfidenceScore =
                            Math.Clamp(
                                fingerprint.Weight,
                                1,
                                100),

                        SourceKind =
                            SourceKind,

                        SourceName =
                            SourceName,

                        Evidence =
                            $"Matched fingerprint " +
                            $"'{fingerprint.Value}' " +
                            $"({fingerprint.Type}).",

                        IsIdentityEvidence =
                            true,

                        Tags =
                            fingerprint.Tags
                    });
            }
        }

        return results;
    }

    private static bool IsFingerprintMatch(
        ApplicationFingerprint fingerprint,
        string logText,
        IReadOnlyCollection<LogDiscoveryFact> facts)
    {
        if (string.IsNullOrWhiteSpace(
                fingerprint.Value))
        {
            return false;
        }

        var comparison =
            fingerprint.IsCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

        if (fingerprint.MatchMode.Equals(
                "Exact",
                StringComparison.OrdinalIgnoreCase))
        {
            return facts.Any(fact =>
                fact.Value.Equals(
                    fingerprint.Value,
                    comparison));
        }

        if (fingerprint.MatchMode.Equals(
                "StartsWith",
                StringComparison.OrdinalIgnoreCase))
        {
            return facts.Any(fact =>
                fact.Value.StartsWith(
                    fingerprint.Value,
                    comparison));
        }

        if (fingerprint.MatchMode.Equals(
                "Regex",
                StringComparison.OrdinalIgnoreCase))
        {
            var options =
                fingerprint.IsCaseSensitive
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase;

            try
            {
                return Regex.IsMatch(
                    logText,
                    fingerprint.Value,
                    options);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return logText.Contains(
            fingerprint.Value,
            comparison);
    }

    private static IReadOnlyCollection<LogDiscoveryFact>
        DiscoverFacts(
            string text)
    {
        var facts =
            new List<LogDiscoveryFact>();

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.AlertRule(),
            KnowledgeContributionType.Other,
            "AlertRule",
            95,
            true,
            "alert");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.SourceServer(),
            KnowledgeContributionType.Server,
            "Server",
            100,
            false,
            "server");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.DbContext(),
            KnowledgeContributionType.DatabaseContext,
            "DbContext",
            100,
            false,
            "database",
            "ef");

        AddDatabaseOperations(
            facts,
            text);

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.ExceptionType(),
            KnowledgeContributionType.Exception,
            "Exception",
            95,
            false,
            "exception");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.ClientConnectionId(),
            KnowledgeContributionType.Other,
            "ClientConnectionId",
            100,
            false,
            "sql",
            "connection");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.CorrelationId(),
            KnowledgeContributionType.Other,
            "CorrelationId",
            100,
            false,
            "correlation");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.SqlErrorNumber(),
            KnowledgeContributionType.Other,
            "SqlErrorNumber",
            100,
            false,
            "sql");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.CommandTimeout(),
            KnowledgeContributionType.Configuration,
            "CommandTimeoutSeconds",
            100,
            false,
            "timeout",
            "database");

        AddDbCommandDuration(
            facts,
            text);

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.Url(),
            KnowledgeContributionType.Dependency,
            "Url",
            80,
            false,
            "url",
            "dependency",
            useWholeMatch: true);

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.ApiPath(),
            KnowledgeContributionType.ApiEndpoint,
            "ApiPath",
            90,
            false,
            "api");

        AddMatches(
            facts,
            text,
            LogDiscoveryPatterns.HttpStatus(),
            KnowledgeContributionType.Other,
            "HttpStatus",
            100,
            false,
            "http");

        AddQualifiedNames(
            facts,
            text);

        AddTechnologyFacts(
            facts,
            text);

        if (LogDiscoveryPatterns.SqlTimeout()
            .IsMatch(text))
        {
            facts.Add(
                new LogDiscoveryFact
                {
                    Type =
                        KnowledgeContributionType
                            .Other,

                    Key =
                        "FailurePattern",

                    Value =
                        "SQL execution timeout",

                    ConfidenceScore =
                        100,

                    Evidence =
                        "Execution Timeout Expired",

                    Tags =
                    [
                        "sql",
                        "timeout",
                        "failure"
                    ]
                });
        }

        return DeduplicateFacts(
            facts);
    }

    private static void AddDatabaseOperations(
        ICollection<LogDiscoveryFact> facts,
        string text)
    {
        var values =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (Match match in
                 LogDiscoveryPatterns.DatabaseOperation()
                     .Matches(text))
        {
            var value =
                GetValue(match);

            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        foreach (Match match in
                 LogDiscoveryPatterns
                     .ParameterizedDatabaseOperation()
                     .Matches(text))
        {
            var value =
                GetValue(match);

            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        foreach (var value in values)
        {
            /*
             * Avoid treating common SQL keywords as
             * database-object names.
             */
            if (IsSqlKeyword(value))
            {
                continue;
            }

            facts.Add(
                new LogDiscoveryFact
                {
                    Type =
                        KnowledgeContributionType
                            .DatabaseObject,

                    Key =
                        "DatabaseOperation",

                    Value =
                        value,

                    ConfidenceScore =
                        95,

                    Evidence =
                        value,

                    Tags =
                    [
                        "database",
                        "sql",
                        "operation"
                    ]
                });
        }
    }

    private static void AddDbCommandDuration(
        ICollection<LogDiscoveryFact> facts,
        string text)
    {
        foreach (Match match in
                 LogDiscoveryPatterns.DbCommandDuration()
                     .Matches(text))
        {
            var raw =
                GetValue(match);

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized =
                raw.Replace(
                    ",",
                    string.Empty,
                    StringComparison.Ordinal);

            if (!long.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var milliseconds))
            {
                continue;
            }

            facts.Add(
                new LogDiscoveryFact
                {
                    Type =
                        KnowledgeContributionType.Other,

                    Key =
                        "DbCommandDurationMs",

                    Value =
                        milliseconds.ToString(
                            CultureInfo.InvariantCulture),

                    ConfidenceScore =
                        100,

                    Evidence =
                        match.Value,

                    Tags =
                    [
                        "database",
                        "duration",
                        "performance"
                    ]
                });
        }
    }

    private static void AddQualifiedNames(
        ICollection<LogDiscoveryFact> facts,
        string text)
    {
        foreach (Match match in
                 LogDiscoveryPatterns.QualifiedName()
                     .Matches(text))
        {
            var value =
                GetValue(match);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!LooksLikeApplicationNamespace(value))
            {
                continue;
            }

            facts.Add(
                new LogDiscoveryFact
                {
                    Type =
                        KnowledgeContributionType.Namespace,

                    Key =
                        "Namespace",

                    Value =
                        value,

                    ConfidenceScore =
                        80,

                    Evidence =
                        value,

                    IsApplicationIdentityHint =
                        true,

                    Tags =
                    [
                        "namespace",
                        "code"
                    ]
                });
        }
    }

    private static bool LooksLikeApplicationNamespace(
        string value)
    {
        if (value.StartsWith(
                "System.",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "Microsoft.",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Contains(
                "firstcdn.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static void AddTechnologyFacts(
        ICollection<LogDiscoveryFact> facts,
        string text)
    {
        AddTechnology(
            facts,
            text,
            LogDiscoveryPatterns.EntityFrameworkCore(),
            "Entity Framework Core");

        AddTechnology(
            facts,
            text,
            LogDiscoveryPatterns.MicrosoftDataSqlClient(),
            "Microsoft.Data.SqlClient");

        AddTechnology(
            facts,
            text,
            LogDiscoveryPatterns.SystemDataSqlClient(),
            "System.Data.SqlClient");

        AddTechnology(
            facts,
            text,
            LogDiscoveryPatterns.SignalR(),
            "SignalR");

        AddTechnology(
            facts,
            text,
            LogDiscoveryPatterns.Xpertdoc(),
            "Xpertdoc");
    }

    private static void AddTechnology(
        ICollection<LogDiscoveryFact> facts,
        string text,
        Regex regex,
        string technology)
    {
        if (!regex.IsMatch(text))
        {
            return;
        }

        facts.Add(
            new LogDiscoveryFact
            {
                Type =
                    KnowledgeContributionType.Technology,

                Key =
                    "Technology",

                Value =
                    technology,

                ConfidenceScore =
                    95,

                Evidence =
                    technology,

                Tags =
                [
                    "technology"
                ]
            });
    }

    private static void AddMatches(
        ICollection<LogDiscoveryFact> facts,
        string text,
        Regex regex,
        KnowledgeContributionType type,
        string key,
        int confidenceScore,
        bool identityHint,
        string tag,
        bool useWholeMatch = false)
    {
        AddMatches(
            facts,
            text,
            regex,
            type,
            key,
            confidenceScore,
            identityHint,
            [tag],
            useWholeMatch);
    }

    private static void AddMatches(
        ICollection<LogDiscoveryFact> facts,
        string text,
        Regex regex,
        KnowledgeContributionType type,
        string key,
        int confidenceScore,
        bool identityHint,
        string firstTag,
        string secondTag,
        bool useWholeMatch = false)
    {
        AddMatches(
            facts,
            text,
            regex,
            type,
            key,
            confidenceScore,
            identityHint,
            [firstTag, secondTag],
            useWholeMatch);
    }

    private static void AddMatches(
        ICollection<LogDiscoveryFact> facts,
        string text,
        Regex regex,
        KnowledgeContributionType type,
        string key,
        int confidenceScore,
        bool identityHint,
        string firstTag,
        string secondTag,
        string thirdTag,
        bool useWholeMatch = false)
    {
        AddMatches(
            facts,
            text,
            regex,
            type,
            key,
            confidenceScore,
            identityHint,
            [firstTag, secondTag, thirdTag],
            useWholeMatch);
    }

    private static void AddMatches(
        ICollection<LogDiscoveryFact> facts,
        string text,
        Regex regex,
        KnowledgeContributionType type,
        string key,
        int confidenceScore,
        bool identityHint,
        IReadOnlyCollection<string> tags,
        bool useWholeMatch)
    {
        foreach (Match match in regex.Matches(text))
        {
            var value =
                useWholeMatch
                    ? match.Value.Trim()
                    : GetValue(match);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            facts.Add(
                new LogDiscoveryFact
                {
                    Type =
                        type,

                    Key =
                        key,

                    Value =
                        value,

                    ConfidenceScore =
                        confidenceScore,

                    Evidence =
                        match.Value.Trim(),

                    IsApplicationIdentityHint =
                        identityHint,

                    Tags =
                        tags
                });
        }
    }

    private KnowledgeContribution CreateContribution(
        LogDiscoveryFact fact)
    {
        return new KnowledgeContribution
        {
            Type =
                fact.Type,

            Key =
                fact.Key,

            Value =
                fact.Value,

            ConfidenceScore =
                fact.ConfidenceScore,

            SourceKind =
                SourceKind,

            SourceName =
                SourceName,

            Evidence =
                fact.Evidence,

            IsIdentityEvidence =
                false,

            Tags =
                fact.Tags
        };
    }

    private static IReadOnlyCollection<LogDiscoveryFact>
        DeduplicateFacts(
            IEnumerable<LogDiscoveryFact> facts)
    {
        return facts
            .GroupBy(
                fact =>
                    $"{fact.Type}|{fact.Key}|{fact.Value}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group
                    .OrderByDescending(fact =>
                        fact.ConfidenceScore)
                    .First())
            .ToArray();
    }

    private static string GetValue(
        Match match)
    {
        if (match.Groups["value"].Success)
        {
            return match.Groups["value"]
                .Value
                .Trim();
        }

        return match.Value.Trim();
    }

    private static bool IsSqlKeyword(
        string value)
    {
        return value.Equals(
                   "SELECT",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "FROM",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "WHERE",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "EXEC",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "EXECUTE",
                   StringComparison.OrdinalIgnoreCase);
    }
}