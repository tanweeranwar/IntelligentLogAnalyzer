using System.Globalization;
using System.Text;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.AI;
using LogAnalyzer.Domain.Models;
using LogAnalyzer.Infrastructure.Investigation.Templates;

namespace LogAnalyzer.Infrastructure.Investigation;

public sealed class InvestigationPreparationEngine
    : IInvestigationPreparationEngine
{
    private const string PackageVersion = "1.0";

    public ReasoningPackage Prepare(
        InvestigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Incident);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.Evidence);
        ArgumentNullException.ThrowIfNull(request.ErrorPatterns);

        var systemInstructions =
            BuildSystemInstructions();

        var investigationContext =
            BuildInvestigationContext(request);

        var outputInstructions =
            BuildOutputInstructions(request.Mode);

        var estimatedTokenCount =
            EstimateTokenCount(
                systemInstructions,
                investigationContext,
                outputInstructions);

        return new ReasoningPackage
        {
            SystemInstructions =
                systemInstructions,

            InvestigationContext =
                investigationContext,

            OutputInstructions =
                outputInstructions,

            Mode =
                request.Mode,

            Version =
                PackageVersion,

            EstimatedTokenCount =
                estimatedTokenCount,

            Metadata =
                BuildMetadata(request)
        };
    }

    private static string BuildSystemInstructions()
    {
        return """
               You are acting as a Principal Software Engineer specializing
               in Production Support and production incident investigation.

               Your responsibility is to analyze the supplied engineering
               evidence and produce a structured investigation report.

               Mandatory rules:

               1. Use only the supplied incident evidence and application context.
               2. Do not invent controllers, methods, classes, database objects,
                  stored procedures, dependencies, workflows, customers, or impact.
               3. When evidence is insufficient, state "Unknown".
               4. Clearly distinguish facts, hypotheses, assumptions, and unknowns.
               5. Every root-cause hypothesis must include supporting evidence.
               6. Every recommendation must explain why it is being recommended.
               7. Confidence scores must range from 0 to 100.
               8. Lower confidence when evidence is incomplete or contradictory.
               9. Suggested SQL must be read-only unless explicitly supported.
               10. Do not recommend destructive production actions.
               11. Do not expose secrets, credentials, tokens, or personal data.
               12. Return valid JSON only, matching the required schema.
               """;
    }

    private static string BuildInvestigationContext(
        InvestigationRequest request)
    {
        var builder =
            new StringBuilder();

        AppendHeader(
            builder,
            "INVESTIGATION REQUEST");

        AppendValue(
            builder,
            "Application",
            request.Application);

        AppendValue(
            builder,
            "Environment",
            request.Environment);

        AppendValue(
            builder,
            "Mode",
            request.Mode.ToString());

        AppendValue(
            builder,
            "Engineer Question",
            request.Question);

        builder.AppendLine();

        AppendIncidentSection(
            builder,
            request.Incident);

        AppendApplicationContextSection(
            builder,
            request.Context,
            request.IncludeKnownIssues);

        AppendEvidenceSection(
            builder,
            request.Evidence,
            request.IncludeStackTrace,
            request.IncludeRawLogContent);

        AppendErrorPatternsSection(
            builder,
            request.ErrorPatterns);

        AppendHeader(
            builder,
            "INVESTIGATION OBJECTIVES");

        builder.AppendLine(
            InvestigationModeTemplate.GetObjectives(
                request.Mode));

        return builder
            .ToString()
            .Trim();
    }

    private static void AppendIncidentSection(
        StringBuilder builder,
        LogIncident incident)
    {
        AppendHeader(
            builder,
            "INCIDENT SUMMARY");

        AppendValue(
            builder,
            "Incident ID",
            incident.IncidentId);

        AppendValue(
            builder,
            "Title",
            incident.Title);

        AppendValue(
            builder,
            "Severity",
            incident.Severity);

        AppendValue(
            builder,
            "Exception",
            incident.ExceptionType);

        AppendValue(
            builder,
            "HTTP Status",
            incident.HttpStatusCode?.ToString(
                CultureInfo.InvariantCulture));

        AppendValue(
            builder,
            "API Path",
            incident.ApiPath);

        AppendValue(
            builder,
            "Server",
            incident.ServerName);

        AppendValue(
            builder,
            "Environment",
            incident.Environment);

        AppendValue(
            builder,
            "Correlation ID",
            incident.CorrelationId);

        AppendValue(
            builder,
            "Occurrence Count",
            incident.EntryCount.ToString(
                CultureInfo.InvariantCulture));

        AppendValue(
            builder,
            "Started At",
            FormatTimestamp(
                incident.StartedAt));

        AppendValue(
            builder,
            "Ended At",
            FormatTimestamp(
                incident.EndedAt));

        builder.AppendLine();
    }

    private static void AppendApplicationContextSection(
        StringBuilder builder,
        ApplicationContextResult context,
        bool includeKnownIssues)
    {
        AppendHeader(
            builder,
            "APPLICATION CONTEXT");

        AppendValue(
            builder,
            "Application",
            context.ApplicationName);

        AppendValue(
            builder,
            "Context Confidence",
            $"{context.ConfidenceScore}%");

        AppendCollection(
            builder,
            "Dependencies",
            context.Dependencies);

        AppendCollection(
            builder,
            "Database Objects",
            context.DatabaseObjects);

        AppendCollection(
            builder,
            "Investigation Hints",
            context.InvestigationHints);

        AppendComponents(
            builder,
            context.Components);

        AppendWorkflows(
            builder,
            context.Workflows);

        if (includeKnownIssues)
        {
            AppendKnownIssues(
                builder,
                context.KnownIssues);
        }

        builder.AppendLine();
    }

    private static void AppendComponents(
        StringBuilder builder,
        IReadOnlyCollection<MatchedApplicationComponent> components)
    {
        builder.AppendLine("Matched Components:");

        if (components.Count == 0)
        {
            builder.AppendLine("- Unknown");
            return;
        }

        foreach (var component in components)
        {
            builder.AppendLine(
                $"- {Sanitize(component.Name)} " +
                $"({Sanitize(component.Type)}), " +
                $"Match Score: {component.MatchScore}%");

            AppendNestedValue(
                builder,
                "Description",
                component.Description);

            AppendNestedCollection(
                builder,
                "Match Reasons",
                component.MatchReasons);

            AppendNestedCollection(
                builder,
                "Dependencies",
                component.Dependencies);

            AppendNestedCollection(
                builder,
                "Database Objects",
                component.DatabaseObjects);
        }
    }

    private static void AppendWorkflows(
        StringBuilder builder,
        IReadOnlyCollection<MatchedApplicationWorkflow> workflows)
    {
        builder.AppendLine("Matched Workflows:");

        if (workflows.Count == 0)
        {
            builder.AppendLine("- Unknown");
            return;
        }

        foreach (var workflow in workflows)
        {
            builder.AppendLine(
                $"- {Sanitize(workflow.Name)}, " +
                $"Match Score: {workflow.MatchScore}%");

            AppendNestedValue(
                builder,
                "Description",
                workflow.Description);

            AppendNestedCollection(
                builder,
                "Match Reasons",
                workflow.MatchReasons);

            AppendNestedCollection(
                builder,
                "Workflow Steps",
                workflow.Steps);

            AppendNestedCollection(
                builder,
                "Dependencies",
                workflow.Dependencies);
        }
    }

    private static void AppendKnownIssues(
        StringBuilder builder,
        IReadOnlyCollection<MatchedKnownIssue> knownIssues)
    {
        builder.AppendLine("Matched Known Issues:");

        if (knownIssues.Count == 0)
        {
            builder.AppendLine("- None matched");
            return;
        }

        foreach (var issue in knownIssues)
        {
            builder.AppendLine(
                $"- {Sanitize(issue.Title)}, " +
                $"Match Score: {issue.MatchScore}%");

            AppendNestedValue(
                builder,
                "Description",
                issue.Description);

            AppendNestedCollection(
                builder,
                "Match Reasons",
                issue.MatchReasons);

            AppendNestedCollection(
                builder,
                "Likely Causes",
                issue.LikelyCauses);

            AppendNestedCollection(
                builder,
                "Investigation Steps",
                issue.InvestigationSteps);

            AppendNestedCollection(
                builder,
                "Resolution Steps",
                issue.ResolutionSteps);

            AppendNestedCollection(
                builder,
                "Suggested Queries",
                issue.SuggestedQueries);
        }
    }

    private static void AppendEvidenceSection(
        StringBuilder builder,
        IReadOnlyCollection<NormalizedLogEntry> evidence,
        bool includeStackTrace,
        bool includeRawLogContent)
    {
        AppendHeader(
            builder,
            "REPRESENTATIVE EVIDENCE");

        if (evidence.Count == 0)
        {
            builder.AppendLine(
                "No representative evidence was available.");

            builder.AppendLine();
            return;
        }

        var sequence = 1;

        foreach (var entry in evidence)
        {
            builder.AppendLine(
                $"Evidence {sequence}:");

            AppendNestedValue(
                builder,
                "Line",
                entry.LineNumber.ToString(
                    CultureInfo.InvariantCulture));

            AppendNestedValue(
                builder,
                "Timestamp",
                FormatTimestamp(
                    entry.Timestamp));

            AppendNestedValue(
                builder,
                "Severity",
                entry.Severity);

            AppendNestedValue(
                builder,
                "Exception",
                entry.ExceptionType);

            AppendNestedValue(
                builder,
                "Message",
                entry.Message);

            AppendNestedValue(
                builder,
                "HTTP Status",
                entry.HttpStatusCode?.ToString(
                    CultureInfo.InvariantCulture));

            AppendNestedValue(
                builder,
                "API Path",
                entry.ApiPath);

            AppendNestedValue(
                builder,
                "Request URL",
                entry.RequestUrl);

            AppendNestedValue(
                builder,
                "Server",
                entry.ServerName);

            AppendNestedValue(
                builder,
                "Machine",
                entry.MachineName);

            AppendNestedValue(
                builder,
                "Environment",
                entry.Environment);

            AppendNestedValue(
                builder,
                "Correlation ID",
                entry.CorrelationId);

            if (includeStackTrace)
            {
                AppendNestedValue(
                    builder,
                    "Stack Trace",
                    LimitText(
                        entry.StackTrace,
                        4000));
            }

            if (includeRawLogContent)
            {
                AppendNestedValue(
                    builder,
                    "Raw Content",
                    LimitText(
                        entry.RawContent,
                        5000));
            }

            builder.AppendLine();
            sequence++;
        }
    }

    private static void AppendErrorPatternsSection(
        StringBuilder builder,
        IReadOnlyCollection<ErrorSummary> errorPatterns)
    {
        AppendHeader(
            builder,
            "RELATED ERROR PATTERNS");

        if (errorPatterns.Count == 0)
        {
            builder.AppendLine(
                "No related error patterns were available.");

            builder.AppendLine();
            return;
        }

        foreach (var pattern in errorPatterns)
        {
            builder.AppendLine(
                $"- Exception: {ValueOrUnknown(pattern.ExceptionType)}");

            builder.AppendLine(
                $"  HTTP Status: " +
                $"{pattern.HttpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"}");

            builder.AppendLine(
                $"  Occurrences: " +
                $"{pattern.OccurrenceCount.ToString(CultureInfo.InvariantCulture)}");

            builder.AppendLine(
                $"  Message: {ValueOrUnknown(pattern.Message)}");
        }

        builder.AppendLine();
    }

    private static string BuildOutputInstructions(
        InvestigationMode mode)
    {
        var modeNote =
            mode switch
            {
                InvestigationMode.Quick =>
                    """
                    For Quick mode:
                    - Return no more than 3 root causes.
                    - Return no more than 5 investigation steps.
                    - Keep the executive summary concise.
                    """,

                InvestigationMode.RootCauseAnalysis =>
                    """
                    For Root Cause Analysis mode:
                    - Emphasize timeline, failure propagation,
                      contributing factors, corrective actions,
                      and preventive actions.
                    """,

                _ =>
                    """
                    For Deep mode:
                    - Provide complete application-aware analysis.
                    - Include SQL and code suggestions only when
                      supported by supplied context.
                    """
            };

        return $$"""
                 Return ONLY valid JSON.

                 Do not wrap the JSON in markdown fences.
                 Do not add commentary before or after the JSON.

                 {{modeNote}}

                 Required JSON structure:

                 {
                   "incidentId": "",
                   "applicationName": "",
                   "environment": "",
                   "executiveSummary": "",
                   "affectedWorkflow": "",
                   "affectedComponents": [],
                   "rootCauses": [
                     {
                       "title": "",
                       "explanation": "",
                       "confidenceScore": 0,
                       "supportingEvidence": [],
                       "contradictingEvidence": []
                     }
                   ],
                   "investigationSteps": [
                     {
                       "sequence": 1,
                       "title": "",
                       "action": "",
                       "reason": "",
                       "expectedOutcome": "",
                       "priority": "High|Medium|Low",
                       "confidenceScore": 0
                     }
                   ],
                   "suggestedSqlQueries": [
                     {
                       "title": "",
                       "databaseName": "",
                       "purpose": "",
                       "query": "",
                       "expectedOutcome": "",
                       "confidenceScore": 0
                     }
                   ],
                   "suggestedCodeLocations": [
                     {
                       "project": "",
                       "filePath": "",
                       "className": "",
                       "methodName": "",
                       "reason": "",
                       "confidenceScore": 0
                     }
                   ],
                   "dependencies": [
                     {
                       "name": "",
                       "type": "",
                       "role": "",
                       "risk": "",
                       "confidenceScore": 0
                     }
                   ],
                   "businessImpact": {
                     "severity": "Critical|High|Medium|Low|Unknown",
                     "customerImpact": "",
                     "operationalImpact": "",
                     "financialImpact": "",
                     "scope": "",
                     "confidenceScore": 0
                   },
                   "resolutionRecommendations": [
                     {
                       "title": "",
                       "description": "",
                       "recommendationType": "Immediate|Permanent|Preventive",
                       "risk": "",
                       "confidenceScore": 0
                     }
                   ],
                   "evidenceReferences": [
                     {
                       "evidenceType": "",
                       "description": "",
                       "source": "",
                       "lineNumber": null,
                       "correlationId": ""
                     }
                   ],
                   "overallConfidenceScore": 0,
                   "unknowns": [],
                   "assumptions": [],
                   "generatedAt": ""
                 }

                 Validation rules:

                 - confidenceScore values must be between 0 and 100.
                 - Use empty arrays when no supported result exists.
                 - Use "Unknown" when a string value cannot be determined.
                 - Do not fabricate SQL table names or code locations.
                 - Suggested SQL must be read-only.
                 - generatedAt must be an ISO-8601 UTC timestamp.
                 """;
    }

    private static IReadOnlyDictionary<string, string>
        BuildMetadata(
            InvestigationRequest request)
    {
        return new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["IncidentId"] =
                ValueOrUnknown(
                    request.Incident.IncidentId),

            ["Application"] =
                ValueOrUnknown(
                    request.Application),

            ["Environment"] =
                ValueOrUnknown(
                    request.Environment),

            ["Mode"] =
                request.Mode.ToString(),

            ["EvidenceCount"] =
                request.Evidence.Count.ToString(
                    CultureInfo.InvariantCulture),

            ["ErrorPatternCount"] =
                request.ErrorPatterns.Count.ToString(
                    CultureInfo.InvariantCulture),

            ["ContextConfidence"] =
                request.Context.ConfidenceScore.ToString(
                    CultureInfo.InvariantCulture),

            ["PreparedAtUtc"] =
                DateTimeOffset.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture)
        };
    }

    private static int EstimateTokenCount(
        params string[] sections)
    {
        var totalCharacters =
            sections
                .Where(section =>
                    !string.IsNullOrWhiteSpace(section))
                .Sum(section =>
                    section.Length);

        /*
         * A simple provider-independent approximation.
         * English technical text typically averages around
         * four characters per token.
         */
        return Math.Max(
            1,
            (int)Math.Ceiling(
                totalCharacters / 4d));
    }

    private static void AppendHeader(
        StringBuilder builder,
        string title)
    {
        builder.AppendLine(
            new string('=', 72));

        builder.AppendLine(title);

        builder.AppendLine(
            new string('=', 72));
    }

    private static void AppendValue(
        StringBuilder builder,
        string label,
        string? value)
    {
        builder.AppendLine(
            $"{label}: {ValueOrUnknown(value)}");
    }

    private static void AppendCollection(
        StringBuilder builder,
        string label,
        IEnumerable<string> values)
    {
        var normalizedValues =
            values
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(Sanitize)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        builder.AppendLine($"{label}:");

        if (normalizedValues.Length == 0)
        {
            builder.AppendLine("- Unknown");
            return;
        }

        foreach (var value in normalizedValues)
        {
            builder.AppendLine(
                $"- {value}");
        }
    }

    private static void AppendNestedValue(
        StringBuilder builder,
        string label,
        string? value)
    {
        builder.AppendLine(
            $"  {label}: {ValueOrUnknown(value)}");
    }

    private static void AppendNestedCollection(
        StringBuilder builder,
        string label,
        IEnumerable<string> values)
    {
        var normalizedValues =
            values
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(Sanitize)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (normalizedValues.Length == 0)
        {
            return;
        }

        builder.AppendLine(
            $"  {label}:");

        foreach (var value in normalizedValues)
        {
            builder.AppendLine(
                $"    - {value}");
        }
    }

    private static string FormatTimestamp(
        DateTimeOffset? timestamp)
    {
        return timestamp.HasValue
            ? timestamp.Value.ToString(
                "O",
                CultureInfo.InvariantCulture)
            : "Unknown";
    }

    private static string ValueOrUnknown(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : Sanitize(value);
    }

    private static string Sanitize(
        string value)
    {
        return value
            .Replace(
                "\0",
                string.Empty,
                StringComparison.Ordinal)
            .Trim();
    }

    private static string LimitText(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var sanitized =
            Sanitize(value);

        return sanitized.Length <= maximumLength
            ? sanitized
            : $"{sanitized[..maximumLength]}...[truncated]";
    }
}