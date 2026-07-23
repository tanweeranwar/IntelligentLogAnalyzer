using System.Globalization;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Infrastructure.Investigation;

public sealed class MockDecisionEngine : IDecisionEngine
{
    public Task<InvestigationReport> AnalyzeAsync(
        ReasoningPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        cancellationToken.ThrowIfCancellationRequested();

        var incidentId =
            GetMetadataValue(
                package,
                "IncidentId",
                "INC-UNKNOWN");

        var applicationName =
            GetMetadataValue(
                package,
                "Application",
                "Unknown");

        var environment =
            GetMetadataValue(
                package,
                "Environment",
                "Unknown");

        var contextConfidence =
            GetMetadataInteger(
                package,
                "ContextConfidence");

        var evidenceCount =
            GetMetadataInteger(
                package,
                "EvidenceCount");

        var errorPatternCount =
            GetMetadataInteger(
                package,
                "ErrorPatternCount");

        var workflow =
            ExtractFirstNamedItem(
                package.InvestigationContext,
                "Matched Workflows:",
                "Matched Known Issues:");

        if (IsUnknown(workflow))
        {
            workflow =
                InferWorkflowFromEvidence(
                    package.InvestigationContext);
        }

        var exceptionType =
            ExtractLabelValue(
                package.InvestigationContext,
                "Exception:");

        var apiPath =
            ExtractLabelValue(
                package.InvestigationContext,
                "API Path:");

        if (IsUnknown(apiPath))
        {
            apiPath =
                ExtractFirstEvidenceValue(
                    package.InvestigationContext,
                    "API Path:");
        }

        var server =
            ExtractLabelValue(
                package.InvestigationContext,
                "Server:");

        if (IsUnknown(server))
        {
            server =
                ExtractFirstEvidenceValue(
                    package.InvestigationContext,
                    "Server:");
        }

        var correlationId =
            ExtractLabelValue(
                package.InvestigationContext,
                "Correlation ID:");

        if (IsUnknown(correlationId))
        {
            correlationId =
                ExtractFirstEvidenceValue(
                    package.InvestigationContext,
                    "Correlation ID:");
        }

        var occurrenceCount =
            ExtractIntegerValue(
                package.InvestigationContext,
                "Occurrence Count:");

        var rootCauseTitle =
            BuildRootCauseTitle(
                exceptionType,
                apiPath);

        var executiveSummary =
            BuildExecutiveSummary(
                applicationName,
                environment,
                workflow,
                exceptionType,
                apiPath,
                occurrenceCount);

        var overallConfidence =
            CalculateOverallConfidence(
                contextConfidence,
                evidenceCount,
                errorPatternCount);

        var report =
            new InvestigationReport
            {
                IncidentId =
                    incidentId,

                ApplicationName =
                    applicationName,

                Environment =
                    environment,

                ExecutiveSummary =
                    executiveSummary,

                NextAction =
                    BuildNextAction(
                        workflow,
                        exceptionType,
                        apiPath,
                        correlationId,
                        overallConfidence),

                Completeness =
                    BuildInvestigationCompleteness(
                        package,
                        workflow,
                        apiPath,
                        correlationId,
                        overallConfidence),

                Timeline =
                    BuildIncidentTimeline(
                        package.InvestigationContext),

                AffectedWorkflow =
                    workflow,

                AffectedComponents =
                    ExtractSectionItems(
                        package.InvestigationContext,
                        "Matched Components:",
                        "Matched Workflows:"),

                RootCauses =
                    BuildRootCauses(
                        rootCauseTitle,
                        exceptionType,
                        apiPath,
                        server,
                        occurrenceCount,
                        overallConfidence),

                InvestigationSteps =
                    BuildInvestigationSteps(
                        exceptionType,
                        apiPath,
                        server,
                        correlationId,
                        workflow),

                SuggestedSqlQueries =
                    BuildSuggestedSqlQueries(
                        package.InvestigationContext,
                        correlationId),

                SuggestedCodeLocations =
                    BuildSuggestedCodeLocations(
                        package.InvestigationContext,
                        apiPath),

                Dependencies =
                    BuildDependencies(
                        package.InvestigationContext),

                BusinessImpact =
                    BuildBusinessImpact(
                        workflow,
                        occurrenceCount,
                        environment,
                        overallConfidence),

                ResolutionRecommendations =
                    BuildResolutionRecommendations(
                        exceptionType,
                        apiPath,
                        overallConfidence),

                EvidenceReferences =
                    BuildEvidenceReferences(
                        package.InvestigationContext,
                        correlationId),

                OverallConfidenceScore =
                    overallConfidence,

                Unknowns =
                    BuildUnknowns(
                        workflow,
                        apiPath,
                        correlationId),

                Assumptions =
                    BuildAssumptions(),

                GeneratedAt =
                    DateTimeOffset.UtcNow
            };

        return Task.FromResult(report);
    }

    private static NextRecommendedAction BuildNextAction(
        string workflow,
        string exceptionType,
        string apiPath,
        string correlationId,
        int overallConfidence)
    {
        if (!IsUnknown(correlationId))
        {
            return new NextRecommendedAction
            {
                Title =
                    "Trace the failed request",

                Action =
                    $"Search application, dependency, and database logs " +
                    $"using correlation ID {correlationId}.",

                Reason =
                    "The correlation ID is the strongest available link " +
                    "for reconstructing the request path and locating the " +
                    "first failure point.",

                ExpectedOutcome =
                    "A chronological request timeline showing the controller, " +
                    "service, database, and dependency involved in the failure.",

                SuggestedOwner =
                    "Production Support",

                EstimatedEffort =
                    "10–15 minutes",

                ConfidenceScore =
                    Math.Clamp(
                        overallConfidence + 5,
                        50,
                        95)
            };
        }

        if (!IsUnknown(apiPath))
        {
            return new NextRecommendedAction
            {
                Title =
                    "Compare successful and failed API requests",

                Action =
                    $"Compare a successful request with a failed request " +
                    $"for {apiPath}, including input, headers, response, " +
                    "and stack trace.",

                Reason =
                    "The affected API is known, but a correlation ID is not " +
                    "available. Comparing executions is the fastest way to " +
                    "identify the differing input or dependency response.",

                ExpectedOutcome =
                    "Identification of the request, data, configuration, or " +
                    "dependency difference that triggers the failure.",

                SuggestedOwner =
                    "Production Support",

                EstimatedEffort =
                    "15–30 minutes",

                ConfidenceScore =
                    Math.Clamp(
                        overallConfidence,
                        45,
                        90)
            };
        }

        return new NextRecommendedAction
        {
            Title =
                "Inspect the earliest complete stack trace",

            Action =
                $"Locate the earliest complete stack trace for " +
                $"{ValueOrUnknown(exceptionType)} within the " +
                $"{ValueOrUnknown(workflow)} workflow.",

            Reason =
                "The API and correlation identifier are not currently known. " +
                "The earliest stack trace is the strongest available evidence " +
                "for identifying the first failing component.",

            ExpectedOutcome =
                "Identification of the class, method, component, or dependency " +
                "where the failure originated.",

            SuggestedOwner =
                "Production Support",

            EstimatedEffort =
                "15–30 minutes",

            ConfidenceScore =
                Math.Clamp(
                    overallConfidence - 5,
                    35,
                    85)
        };
    }

    private static InvestigationCompleteness
        BuildInvestigationCompleteness(
            ReasoningPackage package,
            string workflow,
            string apiPath,
            string correlationId,
            int rootCauseConfidence)
    {
        var evidenceCount =
            GetMetadataInteger(
                package,
                "EvidenceCount");

        var contextConfidence =
            GetMetadataInteger(
                package,
                "ContextConfidence");

        var dependencyCount =
            ExtractSectionItems(
                    package.InvestigationContext,
                    "Dependencies:",
                    "Database Objects:")
                .Count;

        var evidenceScore =
            evidenceCount switch
            {
                >= 8 => 100,
                >= 5 => 80,
                >= 3 => 60,
                >= 1 => 40,
                _ => 0
            };

        var workflowScore =
            IsUnknown(workflow)
                ? 0
                : 100;

        var dependencyScore =
            dependencyCount switch
            {
                >= 3 => 100,
                2 => 80,
                1 => 60,
                _ => 0
            };

        var businessImpactScore =
            IsUnknown(workflow)
                ? 30
                : 70;

        const int changeHistoryScore = 0;

        var overallScore =
            (int)Math.Round(
                evidenceScore * 0.25 +
                contextConfidence * 0.20 +
                workflowScore * 0.15 +
                dependencyScore * 0.10 +
                rootCauseConfidence * 0.15 +
                businessImpactScore * 0.10 +
                changeHistoryScore * 0.05);

        var missingInformation =
            new List<string>();

        if (IsUnknown(apiPath))
        {
            missingInformation.Add(
                "Affected API or operation is not available.");
        }

        if (IsUnknown(correlationId))
        {
            missingInformation.Add(
                "Correlation ID is unavailable.");
        }

        missingInformation.Add(
            "Deployment and configuration change history is unavailable.");

        if (dependencyCount == 0)
        {
            missingInformation.Add(
                "No dependency mapping was identified.");
        }

        return new InvestigationCompleteness
        {
            OverallScore =
                Math.Clamp(
                    overallScore,
                    0,
                    100),

            Evidence =
                CreateCompletenessItem(
                    "Evidence",
                    evidenceScore,
                    $"{evidenceCount} representative evidence entries were selected."),

            ApplicationContext =
                CreateCompletenessItem(
                    "Application context",
                    contextConfidence,
                    "Score produced by the application context resolver."),

            Workflow =
                CreateCompletenessItem(
                    "Workflow",
                    workflowScore,
                    IsUnknown(workflow)
                        ? "The business workflow could not be identified."
                        : $"Matched workflow: {workflow}."),

            Dependencies =
                CreateCompletenessItem(
                    "Dependencies",
                    dependencyScore,
                    $"{dependencyCount} mapped dependencies were identified."),

            RootCause =
                CreateCompletenessItem(
                    "Root cause",
                    rootCauseConfidence,
                    "Current confidence in the leading root-cause hypothesis."),

            BusinessImpact =
                CreateCompletenessItem(
                    "Business impact",
                    businessImpactScore,
                    IsUnknown(workflow)
                        ? "Business impact cannot be fully mapped without a workflow."
                        : "Business impact was inferred from the mapped workflow and occurrences."),

            ChangeHistory =
                CreateCompletenessItem(
                    "Change history",
                    changeHistoryScore,
                    "Deployment and configuration history are not connected yet."),

            MissingInformation =
                missingInformation
        };
    }

    private static InvestigationCompletenessItem
        CreateCompletenessItem(
            string name,
            int score,
            string explanation)
    {
        return new InvestigationCompletenessItem
        {
            Name =
                name,

            Score =
                Math.Clamp(
                    score,
                    0,
                    100),

            Status =
                score switch
                {
                    >= 80 => "Complete",
                    >= 50 => "Partial",
                    > 0 => "Limited",
                    _ => "Missing"
                },

            Explanation =
                explanation
        };
    }

    private static IReadOnlyCollection<IncidentTimelineEvent>
        BuildIncidentTimeline(
            string investigationContext)
    {
        var evidenceRecords =
            ExtractTimelineEvidence(
                investigationContext);

        if (evidenceRecords.Count == 0)
        {
            return Array.Empty<IncidentTimelineEvent>();
        }

        var orderedEvidence =
            evidenceRecords
                .OrderBy(record =>
                    record.Timestamp.HasValue
                        ? 0
                        : 1)
                .ThenBy(record =>
                    record.Timestamp)
                .ThenBy(record =>
                    record.LineNumber)
                .ToArray();

        var groupedEvents =
    GroupConsecutiveTimelineEvents(
        orderedEvidence)
    .ToArray();

        var timeline =
            new List<IncidentTimelineEvent>();

        for (var index = 0;
             index < groupedEvents.Length;
             index++)
        {
            var group =
                groupedEvents[index];

            var first =
                group.First();

            var eventType =
                DetermineTimelineEventType(
                    index,
                    groupedEvents.Length,
                    first);

            timeline.Add(
                new IncidentTimelineEvent
                {
                    Sequence =
                        index + 1,

                    Timestamp =
                        first.Timestamp,

                    EventType =
                        eventType,

                    Title =
                        BuildTimelineTitle(
                            eventType,
                            first),

                    Description =
                        BuildTimelineDescription(
                            group),

                    Severity =
                        first.Severity,

                    ExceptionType =
                        first.ExceptionType,

                    ApiPath =
                        first.ApiPath,

                    ServerName =
                        first.ServerName,

                    CorrelationId =
                        first.CorrelationId,

                    LineNumber =
                        first.LineNumber,

                    OccurrenceCount =
                        group.Count,

                    ConfidenceScore =
                        CalculateTimelineConfidence(
                            first)
                });
        }

        return timeline;
    }

    private static IReadOnlyCollection<TimelineEvidenceRecord>
        ExtractTimelineEvidence(
            string investigationContext)
    {
        if (string.IsNullOrWhiteSpace(
                investigationContext))
        {
            return Array.Empty<TimelineEvidenceRecord>();
        }

        var evidenceSectionIndex =
            investigationContext.IndexOf(
                "REPRESENTATIVE EVIDENCE",
                StringComparison.OrdinalIgnoreCase);

        if (evidenceSectionIndex < 0)
        {
            return Array.Empty<TimelineEvidenceRecord>();
        }

        var evidenceSection =
            investigationContext[
                evidenceSectionIndex..];

        var errorPatternsIndex =
            evidenceSection.IndexOf(
                "RELATED ERROR PATTERNS",
                StringComparison.OrdinalIgnoreCase);

        if (errorPatternsIndex >= 0)
        {
            evidenceSection =
                evidenceSection[
                    ..errorPatternsIndex];
        }

        var lines =
            evidenceSection.Split(
                ['\r', '\n'],
                StringSplitOptions.None);

        var records =
            new List<TimelineEvidenceRecord>();

        TimelineEvidenceRecordBuilder? current =
            null;

        foreach (var rawLine in lines)
        {
            var line =
                rawLine.Trim();

            if (line.StartsWith(
                    "Evidence ",
                    StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith(
                    ":",
                    StringComparison.Ordinal))
            {
                AddTimelineEvidenceRecord(
                    records,
                    current);

                current =
                    new TimelineEvidenceRecordBuilder();

                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Line:",
                    out var lineValue) &&
                int.TryParse(
                    lineValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var lineNumber))
            {
                current.LineNumber =
                    lineNumber;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Timestamp:",
                    out var timestampValue))
            {
                current.Timestamp =
                    ParseTimelineTimestamp(
                        timestampValue);

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Severity:",
                    out var severity))
            {
                current.Severity =
                    severity;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Exception:",
                    out var exceptionType))
            {
                current.ExceptionType =
                    exceptionType;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Message:",
                    out var message))
            {
                current.Message =
                    message;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "API Path:",
                    out var apiPath))
            {
                current.ApiPath =
                    apiPath;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Server:",
                    out var serverName))
            {
                current.ServerName =
                    serverName;

                continue;
            }

            if (TryReadTimelineValue(
                    line,
                    "Correlation ID:",
                    out var correlationId))
            {
                current.CorrelationId =
                    correlationId;
            }
        }

        AddTimelineEvidenceRecord(
            records,
            current);

        return records;
    }

    private static IReadOnlyCollection<
            IReadOnlyCollection<TimelineEvidenceRecord>>
        GroupConsecutiveTimelineEvents(
            IReadOnlyCollection<TimelineEvidenceRecord> records)
    {
        var groups =
            new List<
                IReadOnlyCollection<TimelineEvidenceRecord>>();

        var currentGroup =
            new List<TimelineEvidenceRecord>();

        TimelineEvidenceRecord? previous =
            null;

        foreach (var record in records)
        {
            if (currentGroup.Count == 0)
            {
                currentGroup.Add(record);
                previous = record;
                continue;
            }

            if (ShouldStartNewTimelineEvent(
                    previous!,
                    record))
            {
                groups.Add(
                    currentGroup.ToArray());

                currentGroup =
                    new List<TimelineEvidenceRecord>();
            }

            currentGroup.Add(record);
            previous = record;
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(
                currentGroup.ToArray());
        }

        return groups;
    }

    private static bool ShouldStartNewTimelineEvent(
        TimelineEvidenceRecord previous,
        TimelineEvidenceRecord current)
    {
        if (!string.Equals(
                NormalizeTimelineValue(
                    previous.ExceptionType),
                NormalizeTimelineValue(
                    current.ExceptionType),
                StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(
                NormalizeTimelineValue(
                    previous.Message),
                NormalizeTimelineValue(
                    current.Message),
                StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(
                NormalizeTimelineValue(
                    previous.ApiPath),
                NormalizeTimelineValue(
                    current.ApiPath),
                StringComparison.Ordinal))
        {
            return true;
        }

        if (previous.Timestamp.HasValue &&
            current.Timestamp.HasValue)
        {
            var gap =
                current.Timestamp.Value -
                previous.Timestamp.Value;

            if (gap > TimeSpan.FromMinutes(10))
            {
                return true;
            }
        }

        return false;
    }

    private static string DetermineTimelineEventType(
        int index,
        int totalEvents,
        TimelineEvidenceRecord record)
    {
        if (index == 0)
        {
            return "FirstFailure";
        }

        if (index == totalEvents - 1)
        {
            return "LastObserved";
        }

        if (record.Severity.Equals(
                "Critical",
                StringComparison.OrdinalIgnoreCase))
        {
            return "CriticalFailure";
        }

        return "RepeatedFailure";
    }

    private static string BuildTimelineTitle(
        string eventType,
        TimelineEvidenceRecord record)
    {
        return eventType switch
        {
            "FirstFailure" =>
                "First detected failure",

            "LastObserved" =>
                "Last observed failure",

            "CriticalFailure" =>
                "Critical failure detected",

            _ =>
                !IsUnknown(record.ExceptionType)
                    ? record.ExceptionType
                    : "Repeated application failure"
        };
    }

    private static string BuildTimelineDescription(
        IReadOnlyCollection<TimelineEvidenceRecord> group)
    {
        var first =
            group.First();

        var description =
            ValueOrUnknown(
                first.Message);

        if (group.Count > 1)
        {
            description +=
                $" This pattern occurred {group.Count} times " +
                "within the same timeline window.";
        }

        return description;
    }

    private static int CalculateTimelineConfidence(
        TimelineEvidenceRecord record)
    {
        var score = 30;

        if (record.Timestamp.HasValue)
        {
            score += 20;
        }

        if (!IsUnknown(
                record.ExceptionType))
        {
            score += 20;
        }

        if (!IsUnknown(
                record.Message))
        {
            score += 10;
        }

        if (!IsUnknown(
                record.CorrelationId))
        {
            score += 15;
        }

        if (!IsUnknown(
                record.ApiPath))
        {
            score += 5;
        }

        return Math.Clamp(
            score,
            0,
            100);
    }

    private static void AddTimelineEvidenceRecord(
        ICollection<TimelineEvidenceRecord> records,
        TimelineEvidenceRecordBuilder? builder)
    {
        if (builder is null)
        {
            return;
        }

        if (builder.Timestamp is null &&
            builder.LineNumber is null &&
            IsUnknown(builder.Message) &&
            IsUnknown(builder.ExceptionType))
        {
            return;
        }

        records.Add(
            new TimelineEvidenceRecord
            {
                Timestamp =
                    builder.Timestamp,

                LineNumber =
                    builder.LineNumber,

                Severity =
                    ValueOrUnknown(
                        builder.Severity),

                ExceptionType =
                    ValueOrUnknown(
                        builder.ExceptionType),

                Message =
                    ValueOrUnknown(
                        builder.Message),

                ApiPath =
                    ValueOrUnknown(
                        builder.ApiPath),

                ServerName =
                    ValueOrUnknown(
                        builder.ServerName),

                CorrelationId =
                    ValueOrUnknown(
                        builder.CorrelationId)
            });
    }

    private static bool TryReadTimelineValue(
        string line,
        string label,
        out string value)
    {
        value = string.Empty;

        if (!line.StartsWith(
                label,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value =
            line[label.Length..]
                .Trim();

        return true;
    }

    private static DateTimeOffset?
        ParseTimelineTimestamp(
            string value)
    {
        if (IsUnknown(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var timestamp)
            ? timestamp
            : null;
    }

    private static string NormalizeTimelineValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value
                .Trim()
                .ToLowerInvariant();
    }

    private static IReadOnlyCollection<RootCauseHypothesis>
        BuildRootCauses(
            string rootCauseTitle,
            string exceptionType,
            string apiPath,
            string server,
            int occurrenceCount,
            int overallConfidence)
    {
        var supportingEvidence =
            new List<string>();

        if (!IsUnknown(exceptionType))
        {
            supportingEvidence.Add(
                $"The incident consistently reports {exceptionType}.");
        }

        if (!IsUnknown(apiPath))
        {
            supportingEvidence.Add(
                $"The failure is associated with API path {apiPath}.");
        }

        if (!IsUnknown(server))
        {
            supportingEvidence.Add(
                $"The failure was observed on server {server}.");
        }

        if (occurrenceCount > 0)
        {
            supportingEvidence.Add(
                $"The failure occurred {occurrenceCount} times.");
        }

        if (supportingEvidence.Count == 0)
        {
            supportingEvidence.Add(
                "The prepared reasoning package contains limited incident evidence.");
        }

        return
        [
            new RootCauseHypothesis
            {
                Title =
                    rootCauseTitle,

                Explanation =
                    BuildRootCauseExplanation(
                        exceptionType,
                        apiPath),

                ConfidenceScore =
                    Math.Clamp(
                        overallConfidence,
                        35,
                        90),

                SupportingEvidence =
                    supportingEvidence,

                ContradictingEvidence =
                [
                    "The mock decision engine cannot inspect source code, deployment history, database state, or downstream responses."
                ]
            },

            new RootCauseHypothesis
            {
                Title =
                    "Downstream dependency or invalid response",

                Explanation =
                    "The error may have originated from a downstream dependency returning invalid, incomplete, or unexpected data to the affected application workflow.",

                ConfidenceScore =
                    Math.Clamp(
                        overallConfidence - 15,
                        20,
                        75),

                SupportingEvidence =
                    !IsUnknown(apiPath)
                        ?
                        [
                            $"The error is associated with endpoint {apiPath}.",
                            "Application context may include downstream dependencies."
                        ]
                        :
                        [
                            "The application context contains dependency information."
                        ],

                ContradictingEvidence =
                [
                    "No downstream response payload or availability evidence was provided."
                ]
            },

            new RootCauseHypothesis
            {
                Title =
                    "Application validation or error-handling gap",

                Explanation =
                    "The application may not be validating an input or dependency response before processing it, allowing an invalid value to produce the observed exception.",

                ConfidenceScore =
                    Math.Clamp(
                        overallConfidence - 25,
                        15,
                        65),

                SupportingEvidence =
                    !IsUnknown(exceptionType)
                        ?
                        [
                            $"The observed exception is {exceptionType}.",
                            "Validation and controlled error handling should be reviewed near the failure point."
                        ]
                        :
                        [
                            "The failure produced an application-level error."
                        ],

                ContradictingEvidence =
                [
                    "The exact method and failing statement have not yet been confirmed."
                ]
            }
        ];
    }

    private static IReadOnlyCollection<InvestigationStep>
        BuildInvestigationSteps(
            string exceptionType,
            string apiPath,
            string server,
            string correlationId,
            string workflow)
    {
        return
        [
            new InvestigationStep
            {
                Sequence = 1,
                Title = "Confirm the failure scope",
                Action =
                    $"Validate whether the issue is still occurring in {ValueOrUnknown(workflow)} and determine whether all requests or only specific requests are affected.",
                Reason =
                    "Confirming scope prevents unnecessary troubleshooting and helps establish business impact.",
                ExpectedOutcome =
                    "Clear understanding of whether the incident is active, intermittent, request-specific, or system-wide.",
                Priority = "High",
                ConfidenceScore = 95
            },

            new InvestigationStep
            {
                Sequence = 2,
                Title = "Trace the affected request",
                Action =
                    !IsUnknown(correlationId)
                        ? $"Search all application and dependency logs using correlation ID {correlationId}."
                        : "Identify a failed request and capture its correlation ID, request ID, or timestamp.",
                Reason =
                    "A correlation identifier provides the most reliable way to reconstruct the request flow.",
                ExpectedOutcome =
                    "A complete request timeline across the controller, services, database, and downstream integrations.",
                Priority = "High",
                ConfidenceScore =
                    IsUnknown(correlationId)
                        ? 75
                        : 95
            },

            new InvestigationStep
            {
                Sequence = 3,
                Title = "Inspect the first failure point",
                Action =
                    $"Review the earliest stack trace and error associated with {ValueOrUnknown(apiPath)} and {ValueOrUnknown(exceptionType)}.",
                Reason =
                    "The earliest failure generally identifies the true origin, while later errors may only be consequences.",
                ExpectedOutcome =
                    "Identification of the controller, service, repository, database, or downstream call where processing first failed.",
                Priority = "High",
                ConfidenceScore = 90
            },

            new InvestigationStep
            {
                Sequence = 4,
                Title = "Compare successful and failed requests",
                Action =
                    $"Compare a successful request with a failed request on {ValueOrUnknown(server)}, including payload, headers, dependency responses, and processing time.",
                Reason =
                    "Differences between successful and failed executions often reveal invalid input, configuration, or dependency behavior.",
                ExpectedOutcome =
                    "A specific input, response, configuration, or workflow difference that explains the failure.",
                Priority = "High",
                ConfidenceScore = 85
            },

            new InvestigationStep
            {
                Sequence = 5,
                Title = "Validate dependencies and database operations",
                Action =
                    "Check the health and response details of mapped dependencies and validate related database reads or writes.",
                Reason =
                    "Application errors frequently originate from unavailable dependencies, malformed responses, connectivity failures, or data inconsistencies.",
                ExpectedOutcome =
                    "Confirmation or elimination of database and downstream-system involvement.",
                Priority = "Medium",
                ConfidenceScore = 80
            }
        ];
    }

    private static IReadOnlyCollection<SuggestedSqlQuery>
        BuildSuggestedSqlQueries(
            string investigationContext,
            string correlationId)
    {
        var databaseObjects =
            ExtractSectionItems(
                investigationContext,
                "Database Objects:",
                "Investigation Hints:");

        if (databaseObjects.Count == 0)
        {
            return Array.Empty<SuggestedSqlQuery>();
        }

        var queries =
            new List<SuggestedSqlQuery>();

        foreach (var databaseObject in databaseObjects.Take(3))
        {
            var safeObjectName =
                databaseObject.Trim();

            if (!IsSafeSqlIdentifier(safeObjectName))
            {
                continue;
            }

            queries.Add(
                new SuggestedSqlQuery
                {
                    Title =
                        $"Review recent records in {safeObjectName}",

                    DatabaseName =
                        "Unknown",

                    Purpose =
                        "Validate whether the affected request created or updated related database records.",

                    Query =
                        BuildReadOnlyQuery(
                            safeObjectName,
                            correlationId),

                    ExpectedOutcome =
                        "Records related to the incident timeframe or correlation ID are returned for validation.",

                    ConfidenceScore =
                        IsUnknown(correlationId)
                            ? 55
                            : 75
                });
        }

        return queries;
    }

    private static IReadOnlyCollection<SuggestedCodeLocation>
        BuildSuggestedCodeLocations(
            string investigationContext,
            string apiPath)
    {
        var components =
            ExtractSectionItems(
                investigationContext,
                "Matched Components:",
                "Matched Workflows:");

        if (components.Count == 0 &&
            IsUnknown(apiPath))
        {
            return Array.Empty<SuggestedCodeLocation>();
        }

        var results =
            new List<SuggestedCodeLocation>();

        foreach (var component in components.Take(3))
        {
            results.Add(
                new SuggestedCodeLocation
                {
                    Project =
                        "Unknown",

                    FilePath =
                        "Unknown",

                    ClassName =
                        RemoveScoreAndType(
                            component),

                    MethodName =
                        ExtractMethodName(
                            apiPath),

                    Reason =
                        "This component was matched by the application context resolver and is associated with the affected API or workflow.",

                    ConfidenceScore =
                        70
                });
        }

        if (results.Count == 0 &&
            !IsUnknown(apiPath))
        {
            results.Add(
                new SuggestedCodeLocation
                {
                    Project =
                        "Unknown",

                    FilePath =
                        "Unknown",

                    ClassName =
                        ExtractControllerName(
                            apiPath),

                    MethodName =
                        ExtractMethodName(
                            apiPath),

                    Reason =
                        "The API path indicates the likely controller and action entry point for investigation.",

                    ConfidenceScore =
                        60
                });
        }

        return results;
    }

    private static IReadOnlyCollection<DependencyFinding>
        BuildDependencies(
            string investigationContext)
    {
        var dependencies =
            ExtractSectionItems(
                investigationContext,
                "Dependencies:",
                "Database Objects:");

        return dependencies
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(dependency =>
                new DependencyFinding
                {
                    Name =
                        dependency,

                    Type =
                        DetermineDependencyType(
                            dependency),

                    Role =
                        "Mapped dependency associated with the affected application component or workflow.",

                    Risk =
                        "Validate availability, latency, authentication, response format, and recent changes.",

                    ConfidenceScore =
                        70
                })
            .ToArray();
    }

    private static BusinessImpactAssessment
        BuildBusinessImpact(
            string workflow,
            int occurrenceCount,
            string environment,
            int confidence)
    {
        var isProduction =
            environment.Equals(
                "PROD",
                StringComparison.OrdinalIgnoreCase) ||
            environment.Equals(
                "PRODUCTION",
                StringComparison.OrdinalIgnoreCase);

        var severity =
            isProduction && occurrenceCount >= 100
                ? "High"
                : isProduction && occurrenceCount > 0
                    ? "Medium"
                    : occurrenceCount >= 100
                        ? "Medium"
                        : "Low";

        return new BusinessImpactAssessment
        {
            Severity =
                severity,

            CustomerImpact =
                IsUnknown(workflow)
                    ? "Unknown. The affected customer journey has not been fully identified."
                    : $"Users of the {workflow} workflow may experience failed or incomplete requests.",

            OperationalImpact =
                occurrenceCount > 0
                    ? $"{occurrenceCount} failed or error-producing occurrences were identified."
                    : "The occurrence scope is currently unknown.",

            FinancialImpact =
                "Unknown. No transaction value or financial-impact data was supplied.",

            Scope =
                isProduction
                    ? "Production environment impact detected."
                    : $"Impact detected in {ValueOrUnknown(environment)}.",

            ConfidenceScore =
                Math.Clamp(
                    confidence - 10,
                    20,
                    85)
        };
    }

    private static IReadOnlyCollection<ResolutionRecommendation>
        BuildResolutionRecommendations(
            string exceptionType,
            string apiPath,
            int confidence)
    {
        return
        [
            new ResolutionRecommendation
            {
                Title =
                    "Add input and dependency-response validation",

                Description =
                    $"Validate all required inputs and downstream responses before processing them in {ValueOrUnknown(apiPath)}.",

                RecommendationType =
                    "Permanent",

                Risk =
                    "Low when implemented with backward-compatible validation and controlled error handling.",

                ConfidenceScore =
                    Math.Clamp(
                        confidence,
                        40,
                        85)
            },

            new ResolutionRecommendation
            {
                Title =
                    "Improve structured diagnostic logging",

                Description =
                    $"Log the correlation ID, sanitized request identifiers, dependency status, response content type, and the exact component producing {ValueOrUnknown(exceptionType)}.",

                RecommendationType =
                    "Preventive",

                Risk =
                    "Low, provided sensitive values and payloads are not logged.",

                ConfidenceScore =
                    90
            },

            new ResolutionRecommendation
            {
                Title =
                    "Add targeted monitoring and alerting",

                Description =
                    "Create alerts for repeated failures on the affected API and dependency, using occurrence thresholds and a defined incident window.",

                RecommendationType =
                    "Preventive",

                Risk =
                    "Low. Alert thresholds should be tuned to avoid noise.",

                ConfidenceScore =
                    85
            }
        ];
    }

    private static IReadOnlyCollection<EvidenceReference>
        BuildEvidenceReferences(
            string investigationContext,
            string correlationId)
    {
        var messages =
            ExtractEvidenceMessages(
                investigationContext);

        return messages
            .Take(10)
            .Select((message, index) =>
                new EvidenceReference
                {
                    EvidenceType =
                        "Log Entry",

                    Description =
                        message,

                    Source =
                        $"Prepared Evidence {index + 1}",

                    LineNumber =
                        null,

                    CorrelationId =
                        IsUnknown(correlationId)
                            ? string.Empty
                            : correlationId
                })
            .ToArray();
    }

    private static IReadOnlyCollection<string>
        BuildUnknowns(
            string workflow,
            string apiPath,
            string correlationId)
    {
        var unknowns =
            new List<string>
            {
                "Deployment and configuration change history were not provided.",
                "The mock decision engine cannot verify source-code implementation details.",
                "Downstream response payloads and database state were not provided."
            };

        if (IsUnknown(workflow))
        {
            unknowns.Add(
                "The affected business workflow could not be determined.");
        }

        if (IsUnknown(apiPath))
        {
            unknowns.Add(
                "The affected API or operation could not be determined.");
        }

        if (IsUnknown(correlationId))
        {
            unknowns.Add(
                "No correlation ID was available to reconstruct the complete request flow.");
        }

        return unknowns;
    }

    private static IReadOnlyCollection<string>
        BuildAssumptions()
    {
        return
        [
            "The supplied representative evidence accurately reflects the selected incident.",
            "The application knowledge file is current and correctly maps APIs, workflows, and dependencies.",
            "The incident grouping has not combined unrelated failures."
        ];
    }

    private static string BuildExecutiveSummary(
        string applicationName,
        string environment,
        string workflow,
        string exceptionType,
        string apiPath,
        int occurrenceCount)
    {
        var occurrenceText =
            occurrenceCount > 0
                ? $"{occurrenceCount} occurrences were detected"
                : "The occurrence count could not be determined";

        var failureLocation =
            !IsUnknown(apiPath)
                ? $"for {apiPath}"
                : !IsUnknown(workflow)
                    ? $"within the {workflow} workflow"
                    : "within the application";

        var exceptionText =
            !IsUnknown(exceptionType)
                ? $"The failures produced {exceptionType}."
                : "The exact exception type could not be determined.";

        return
            $"{applicationName} experienced an incident in " +
            $"{ValueOrUnknown(environment)}. " +
            $"{occurrenceText} {failureLocation}. " +
            $"{exceptionText} " +
            "Additional evidence validation is required before " +
            "confirming the final root cause.";
    }

    private static string BuildRootCauseTitle(
        string exceptionType,
        string apiPath)
    {
        if (!IsUnknown(exceptionType))
        {
            return $"{exceptionType} in the affected application workflow";
        }

        if (!IsUnknown(apiPath))
        {
            return $"Failure while processing {apiPath}";
        }

        return "Application processing failure";
    }

    private static string BuildRootCauseExplanation(
        string exceptionType,
        string apiPath)
    {
        return
            $"The incident evidence indicates that {ValueOrUnknown(apiPath)} " +
            $"failed while processing a request and produced " +
            $"{ValueOrUnknown(exceptionType)}. " +
            "The precise failing statement and invalid condition must be confirmed using the earliest stack trace and correlated request evidence.";
    }

    private static string BuildReadOnlyQuery(
        string databaseObject,
        string correlationId)
    {
        if (!IsUnknown(correlationId))
        {
            return
                $"SELECT TOP (100) *{Environment.NewLine}" +
                $"FROM {databaseObject}{Environment.NewLine}" +
                $"WHERE CorrelationId = '{EscapeSqlLiteral(correlationId)}'{Environment.NewLine}" +
                $"ORDER BY 1 DESC;";
        }

        return
            $"SELECT TOP (100) *{Environment.NewLine}" +
            $"FROM {databaseObject};";
    }

    private static bool IsSafeSqlIdentifier(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '_' or '.' or '[' or ']');
    }

    private static string EscapeSqlLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    private static string DetermineDependencyType(
        string dependency)
    {
        if (dependency.Contains(
                "database",
                StringComparison.OrdinalIgnoreCase) ||
            dependency.Contains(
                "sql",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Database";
        }

        if (dependency.Contains(
                "api",
                StringComparison.OrdinalIgnoreCase) ||
            dependency.Contains(
                "service",
                StringComparison.OrdinalIgnoreCase) ||
            dependency.Contains(
                "integration",
                StringComparison.OrdinalIgnoreCase))
        {
            return "External or Application Service";
        }

        if (dependency.Contains(
                "queue",
                StringComparison.OrdinalIgnoreCase) ||
            dependency.Contains(
                "message",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Messaging";
        }

        return "Application Dependency";
    }

    private static string ExtractControllerName(
        string apiPath)
    {
        if (IsUnknown(apiPath))
        {
            return "Unknown";
        }

        var segments =
            apiPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var apiIndex =
            Array.FindIndex(
                segments,
                segment =>
                    segment.Equals(
                        "api",
                        StringComparison.OrdinalIgnoreCase));

        if (apiIndex >= 0 &&
            apiIndex + 1 < segments.Length)
        {
            return segments[apiIndex + 1];
        }

        return segments.Length >= 2
            ? segments[^2]
            : "Unknown";
    }

    private static string ExtractMethodName(
        string apiPath)
    {
        if (IsUnknown(apiPath))
        {
            return "Unknown";
        }

        var segments =
            apiPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        return segments.Length > 0
            ? segments[^1]
            : "Unknown";
    }

    private static string RemoveScoreAndType(
        string component)
    {
        var commaIndex =
            component.IndexOf(
                ", Match Score:",
                StringComparison.OrdinalIgnoreCase);

        var withoutScore =
            commaIndex >= 0
                ? component[..commaIndex]
                : component;

        var parenthesisIndex =
            withoutScore.IndexOf(
                '(',
                StringComparison.Ordinal);

        return parenthesisIndex > 0
            ? withoutScore[..parenthesisIndex].Trim()
            : withoutScore.Trim();
    }

    private static int CalculateOverallConfidence(
        int contextConfidence,
        int evidenceCount,
        int errorPatternCount)
    {
        var score = 25;

        score += Math.Min(
            contextConfidence / 2,
            40);

        score += Math.Min(
            evidenceCount * 3,
            20);

        score += Math.Min(
            errorPatternCount * 2,
            15);

        return Math.Clamp(
            score,
            20,
            90);
    }

    private static string GetMetadataValue(
        ReasoningPackage package,
        string key,
        string fallback)
    {
        if (package.Metadata.TryGetValue(
                key,
                out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    private static int GetMetadataInteger(
        ReasoningPackage package,
        string key)
    {
        var value =
            GetMetadataValue(
                package,
                key,
                "0");

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private static string ExtractLabelValue(
        string content,
        string label)
    {
        var line =
            content.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .FirstOrDefault(item =>
                    item.StartsWith(
                        label,
                        StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(line))
        {
            return "Unknown";
        }

        var value =
            line[label.Length..]
                .Trim();

        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value;
    }

    private static int ExtractIntegerValue(
        string content,
        string label)
    {
        var value =
            ExtractLabelValue(
                content,
                label);

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private static string ExtractFirstNamedItem(
        string content,
        string sectionHeader,
        string? endingHeader)
    {
        var items =
            ExtractSectionItems(
                content,
                sectionHeader,
                endingHeader);

        if (items.Count == 0)
        {
            return "Unknown";
        }

        var value =
            items.First();

        var scoreIndex =
            value.IndexOf(
                ", Match Score:",
                StringComparison.OrdinalIgnoreCase);

        if (scoreIndex >= 0)
        {
            value =
                value[..scoreIndex];
        }

        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
    }

    private static string ExtractFirstEvidenceValue(
        string content,
        string label)
    {
        var evidenceIndex =
            content.IndexOf(
                "REPRESENTATIVE EVIDENCE",
                StringComparison.OrdinalIgnoreCase);

        if (evidenceIndex < 0)
        {
            return "Unknown";
        }

        var evidenceContent =
            content[evidenceIndex..];

        return ExtractLabelValue(
            evidenceContent,
            label);
    }

    private static string InferWorkflowFromEvidence(
        string content)
    {
        if (content.Contains(
                "Xpertdoc",
                StringComparison.OrdinalIgnoreCase) ||
            content.Contains(
                "TemplateExecutionException",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Document or Template Generation";
        }

        if (content.Contains(
                "getMpacImages",
                StringComparison.OrdinalIgnoreCase) ||
            content.Contains(
                "MPAC",
                StringComparison.OrdinalIgnoreCase))
        {
            return "MPAC Image Retrieval";
        }

        if (content.Contains(
                "AciApi",
                StringComparison.OrdinalIgnoreCase))
        {
            return "ACI Integration";
        }

        if (content.Contains(
                "authenticateuser",
                StringComparison.OrdinalIgnoreCase))
        {
            return "User Authentication";
        }

        return "Unknown";
    }

    private static IReadOnlyCollection<string>
        ExtractSectionItems(
            string content,
            string sectionHeader,
            string? endingHeader)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        var lines =
            content.Split(
                ['\r', '\n'],
                StringSplitOptions.None);

        var collecting = false;
        var items =
            new List<string>();

        foreach (var rawLine in lines)
        {
            var line =
                rawLine.Trim();

            if (line.Equals(
                    sectionHeader,
                    StringComparison.OrdinalIgnoreCase))
            {
                collecting = true;
                continue;
            }

            if (collecting &&
                !string.IsNullOrWhiteSpace(endingHeader) &&
                line.Equals(
                    endingHeader,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!collecting)
            {
                continue;
            }

            if (line.StartsWith(
                    "=",
                    StringComparison.Ordinal) ||
                IsMajorSectionHeading(line))
            {
                break;
            }

            if (!line.StartsWith(
                    "- ",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var value =
                line[2..].Trim();

            if (!string.IsNullOrWhiteSpace(value) &&
                !value.Equals(
                    "Unknown",
                    StringComparison.OrdinalIgnoreCase) &&
                !value.Equals(
                    "None matched",
                    StringComparison.OrdinalIgnoreCase))
            {
                items.Add(value);
            }
        }

        return items;
    }

    private static IReadOnlyCollection<string>
        ExtractEvidenceMessages(
            string content)
    {
        var lines =
            content.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        return lines
            .Where(line =>
                line.StartsWith(
                    "Message:",
                    StringComparison.OrdinalIgnoreCase))
            .Select(line =>
                line["Message:".Length..].Trim())
            .Where(message =>
                !string.IsNullOrWhiteSpace(message) &&
                !message.Equals(
                    "Unknown",
                    StringComparison.OrdinalIgnoreCase))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsMajorSectionHeading(
        string value)
    {
        return value is
            "APPLICATION CONTEXT" or
            "REPRESENTATIVE EVIDENCE" or
            "RELATED ERROR PATTERNS" or
            "INVESTIGATION OBJECTIVES" or
            "INCIDENT SUMMARY";
    }

    private static bool IsUnknown(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.Equals(
                   "Unknown",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrUnknown(
        string? value)
    {
        return IsUnknown(value)
            ? "Unknown"
            : value!.Trim();
    }

    private sealed class TimelineEvidenceRecordBuilder
    {
        public DateTimeOffset? Timestamp { get; set; }

        public int? LineNumber { get; set; }

        public string Severity { get; set; } =
            string.Empty;

        public string ExceptionType { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public string ApiPath { get; set; } =
            string.Empty;

        public string ServerName { get; set; } =
            string.Empty;

        public string CorrelationId { get; set; } =
            string.Empty;
    }

    private sealed class TimelineEvidenceRecord
    {
        public DateTimeOffset? Timestamp { get; init; }

        public int? LineNumber { get; init; }

        public string Severity { get; init; } =
            string.Empty;

        public string ExceptionType { get; init; } =
            string.Empty;

        public string Message { get; init; } =
            string.Empty;

        public string ApiPath { get; init; } =
            string.Empty;

        public string ServerName { get; init; } =
            string.Empty;

        public string CorrelationId { get; init; } =
            string.Empty;
    }
}