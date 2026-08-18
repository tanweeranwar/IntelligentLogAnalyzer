using LogAnalyzer.Domain.AI;
using LogAnalyzer.Infrastructure.AI;

namespace LogAnalyzer.Infrastructure.Investigation;

internal sealed class InvestigationReportGuardrail
{
    public InvestigationReport Apply(
        InvestigationReport report,
        IReadOnlyCollection<EvidenceClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(claims);

        var strongClaims =
            claims
                .Where(claim =>
                    claim.IsConfirmed)
                .ToArray();

        var weakClaims =
            claims
                .Where(claim =>
                    claim.IsWeak)
                .ToArray();

        var application =
            GuardApplicationName(
                report.ApplicationName,
                strongClaims);

        var workflow =
            GuardWorkflow(
                report.AffectedWorkflow,
                strongClaims);

        var timeoutEvidence =
            HasExecutionTimeoutEvidence(
                strongClaims);

        return new InvestigationReport
        {
            IncidentId =
                report.IncidentId,

            ApplicationName =
                application,

            Environment =
                report.Environment,

            ExecutiveSummary =
                GuardExecutiveSummary(
                    report.ExecutiveSummary,
                    timeoutEvidence),

            NextAction =
                GuardNextAction(
                    report.NextAction,
                    timeoutEvidence),

            Completeness =
                report.Completeness,

            Timeline =
                report.Timeline,

            AffectedWorkflow =
                workflow,

            AffectedComponents =
                GuardComponents(
                    report.AffectedComponents,
                    strongClaims),

            RootCauses =
                GuardRootCauses(
                    report.RootCauses,
                    timeoutEvidence),

            InvestigationSteps =
                GuardIdentityReferences(
                    report.InvestigationSteps,
                    application,
                    workflow),

            SuggestedSqlQueries =
                GuardSqlQueries(
                    report.SuggestedSqlQueries,
                    strongClaims),

            SuggestedCodeLocations =
                GuardCodeLocations(
                    report.SuggestedCodeLocations,
                    strongClaims),

            Dependencies =
                GuardDependencies(
                    report.Dependencies,
                    strongClaims),

            BusinessImpact =
                GuardBusinessImpact(
                    report.BusinessImpact,
                    application,
                    workflow),

            ResolutionRecommendations =
                GuardRecommendations(
                    report.ResolutionRecommendations,
                    timeoutEvidence),

            EvidenceReferences =
                report.EvidenceReferences,

            OverallConfidenceScore =
                RecalculateOverallConfidence(
                    report,
                    strongClaims),

            Unknowns =
                BuildUnknowns(
                    report.Unknowns,
                    weakClaims,
                    application,
                    workflow,
                    timeoutEvidence),

            Assumptions =
                report.Assumptions,

            GeneratedAt =
                DateTimeOffset.UtcNow
        };
    }

    private static bool HasExecutionTimeoutEvidence(
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        return strongClaims.Any(claim =>
                   claim.Authority ==
                   EvidenceAuthority.RawLog &&
                   (
                       claim.Value.Contains(
                           "Execution Timeout Expired",
                           StringComparison.OrdinalIgnoreCase) ||
                       claim.Value.Contains(
                           "wait operation timed out",
                           StringComparison.OrdinalIgnoreCase)
                   )) ||
               strongClaims.Any(claim =>
                   claim.Authority ==
                   EvidenceAuthority.RawLog &&
                   claim.Label.Contains(
                       "SQL error number",
                       StringComparison.OrdinalIgnoreCase) &&
                   claim.Value.Trim().Equals(
                       "-2",
                       StringComparison.OrdinalIgnoreCase));
    }

    private static string GuardApplicationName(
        string applicationName,
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        if (string.IsNullOrWhiteSpace(
                applicationName) ||
            applicationName.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        var supported =
            strongClaims.Any(claim =>
                claim.Category.Equals(
                    "Application",
                    StringComparison.OrdinalIgnoreCase) &&
                claim.Value.Equals(
                    applicationName,
                    StringComparison.OrdinalIgnoreCase));

        return supported
            ? applicationName
            : "Unknown";
    }

    private static string GuardWorkflow(
        string workflow,
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        if (string.IsNullOrWhiteSpace(
                workflow) ||
            workflow.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        var supported =
            strongClaims.Any(claim =>
                claim.Label.Contains(
                    "Workflow",
                    StringComparison.OrdinalIgnoreCase) &&
                claim.Value.Equals(
                    workflow,
                    StringComparison.OrdinalIgnoreCase));

        return supported
            ? workflow
            : "Unknown";
    }

    private static IReadOnlyCollection<string>
        GuardComponents(
            IReadOnlyCollection<string> components,
            IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        return components
            .Where(component =>
                strongClaims.Any(claim =>
                    claim.Value.Equals(
                        component,
                        StringComparison.OrdinalIgnoreCase)))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<DependencyFinding>
        GuardDependencies(
            IReadOnlyCollection<DependencyFinding> dependencies,
            IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        return dependencies
            .Where(dependency =>
                strongClaims.Any(claim =>
                    claim.Category.Equals(
                        "Dependency",
                        StringComparison.OrdinalIgnoreCase) &&
                    claim.Value.Equals(
                        dependency.Name,
                        StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string GuardExecutiveSummary(
        string summary,
        bool timeoutEvidence)
    {
        if (!timeoutEvidence ||
            string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        /*
         * We only remove unsupported connection conclusions.
         * We do not rewrite otherwise valid model reasoning.
         */

        if (!ContainsConnectionConclusion(summary))
        {
            return summary;
        }

        return
            "The incident contains confirmed database command execution-timeout evidence. " +
            "The available evidence does not establish a database connection failure.";
    }

    private static NextRecommendedAction GuardNextAction(
        NextRecommendedAction action,
        bool timeoutEvidence)
    {
        if (!timeoutEvidence)
        {
            return action;
        }

        if (!ContainsConnectionConclusion(
                $"{action.Title} {action.Action} {action.Reason}"))
        {
            return action;
        }

        return new NextRecommendedAction
        {
            Title =
                "Investigate the timed-out database operation",

            Action =
                "Review the failing database operation for execution duration, blocking, " +
                "execution-plan behavior, resource pressure, and parameter-specific behavior.",

            Reason =
                "The confirmed evidence shows a database command execution timeout. " +
                "It does not independently establish a connection failure.",

            ExpectedOutcome =
                "Evidence identifying why the database operation exceeded its execution threshold.",

            SuggestedOwner =
                action.SuggestedOwner,

            EstimatedEffort =
                action.EstimatedEffort,

            ConfidenceScore =
                Math.Max(
                    action.ConfidenceScore,
                    85)
        };
    }

    private static IReadOnlyCollection<RootCauseHypothesis>
        GuardRootCauses(
            IReadOnlyCollection<RootCauseHypothesis> rootCauses,
            bool timeoutEvidence)
    {
        return rootCauses
            .Select(rootCause =>
                GuardRootCause(
                    rootCause,
                    timeoutEvidence))
            .ToArray();
    }

    private static RootCauseHypothesis GuardRootCause(
        RootCauseHypothesis rootCause,
        bool timeoutEvidence)
    {
        var confidence =
            NormalizeHypothesisConfidence(
                rootCause);

        if (!timeoutEvidence ||
            !ContainsConnectionConclusion(
                $"{rootCause.Title} {rootCause.Explanation}"))
        {
            return new RootCauseHypothesis
            {
                Title =
                    rootCause.Title,

                Explanation =
                    rootCause.Explanation,

                ConfidenceScore =
                    confidence,

                SupportingEvidence =
                    rootCause.SupportingEvidence,

                ContradictingEvidence =
                    rootCause.ContradictingEvidence
            };
        }

        return new RootCauseHypothesis
        {
            Title =
                "Database operation exceeded execution timeout",

            Explanation =
                "Confirmed evidence indicates that a database command exceeded its configured " +
                "execution threshold. The evidence does not establish that the database " +
                "connection itself was unstable.",

            ConfidenceScore =
                Math.Max(
                    confidence,
                    70),

            SupportingEvidence =
                rootCause.SupportingEvidence,

            ContradictingEvidence =
                rootCause.ContradictingEvidence
                    .Concat(
                    [
                        "No authoritative evidence currently confirms a database connection failure."
                    ])
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
        };
    }

    private static IReadOnlyCollection<InvestigationStep>
        GuardInvestigationSteps(
            IReadOnlyCollection<InvestigationStep> steps,
            bool timeoutEvidence)
    {
        if (!timeoutEvidence)
        {
            return steps;
        }

        return steps
            .Select(step =>
                GuardInvestigationStep(
                    step,
                    timeoutEvidence))
            .OrderBy(step =>
                step.Sequence)
            .ToArray();
    }

    private static InvestigationStep GuardInvestigationStep(
        InvestigationStep step,
        bool timeoutEvidence)
    {
        if (!timeoutEvidence)
        {
            return step;
        }

        var content =
            $"{step.Title} {step.Action} {step.ExpectedOutcome}";

        if (!ContainsConnectionConclusion(content) &&
            !ContainsUnsafeTimeoutConclusion(content))
        {
            return step;
        }

        if (step.Sequence <= 1)
        {
            return new InvestigationStep
            {
                Sequence =
                    step.Sequence,

                Title =
                    "Inspect the timed-out database operation",

                Action =
                    "Identify the exact failing database operation and review execution time, " +
                    "blocking, waits, execution plan, and database resource conditions.",

                Reason =
                    "Execution-timeout evidence points first to database command execution behavior, " +
                    "not automatically to connection instability.",

                ExpectedOutcome =
                    "Determine whether slow execution, blocking, plan quality, resource pressure, " +
                    "or another execution condition caused the timeout.",

                Priority =
                    "High",

                ConfidenceScore =
                    Math.Max(
                        step.ConfidenceScore,
                        90)
            };
        }

        return new InvestigationStep
        {
            Sequence =
                step.Sequence,

            Title =
                "Validate timeout context",

            Action =
                "Compare the configured execution timeout with normal successful execution times " +
                "after investigating the cause of the slow operation.",

            Reason =
                "Reaching a timeout threshold does not prove the configured value is too low.",

            ExpectedOutcome =
                "Determine whether timeout configuration is appropriate only after execution behavior is understood.",

            Priority =
                step.Priority,

            ConfidenceScore =
                Math.Max(
                    step.ConfidenceScore,
                    85)
        };
    }

    private static bool ContainsConnectionConclusion(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(
                   "connection issue",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "connection failure",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "connection instability",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "connection stability",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "check database connection",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsUnsafeTimeoutConclusion(
    string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var containsTimeout =
            value.Contains(
                "timeout",
                StringComparison.OrdinalIgnoreCase);

        if (!containsTimeout)
        {
            return false;
        }

        var recommendsIncrease =
            value.Contains(
                "increase",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "raise",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "extend",
                StringComparison.OrdinalIgnoreCase);

        var claimsTooLow =
            value.Contains(
                "too low",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "insufficient timeout",
                StringComparison.OrdinalIgnoreCase);

        return recommendsIncrease ||
               claimsTooLow;
    }

    private static int NormalizeHypothesisConfidence(
        RootCauseHypothesis rootCause)
    {
        if (rootCause.ConfidenceScore <= 0)
        {
            return rootCause.SupportingEvidence.Count > 0
                ? 40
                : 20;
        }

        return Math.Clamp(
            rootCause.ConfidenceScore,
            10,
            95);
    }

    private static IReadOnlyCollection<SuggestedSqlQuery>
        GuardSqlQueries(
            IReadOnlyCollection<SuggestedSqlQuery> queries,
            IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        return queries
            .Where(query =>
                IsSqlQueryGrounded(
                    query,
                    strongClaims))
            .ToArray();
    }

    private static bool IsSqlQueryGrounded(
        SuggestedSqlQuery query,
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        if (string.IsNullOrWhiteSpace(
                query.Query))
        {
            return false;
        }

        var databaseClaims =
            strongClaims
                .Where(claim =>
                    claim.Category.Equals(
                        "Database",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (databaseClaims.Length == 0)
        {
            return false;
        }

        /*
         * A database name must be explicitly confirmed.
         */

        if (!string.IsNullOrWhiteSpace(
                query.DatabaseName) &&
            databaseClaims.Any(claim =>
                claim.Label.Contains(
                    "Database name",
                    StringComparison.OrdinalIgnoreCase) &&
                claim.Value.Equals(
                    query.DatabaseName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        /*
         * Otherwise the query must reference a confirmed DB object
         * or operation from authoritative evidence.
         */

        return databaseClaims.Any(claim =>
            (
                claim.Label.Contains(
                    "Database object",
                    StringComparison.OrdinalIgnoreCase) ||
                claim.Label.Contains(
                    "Database operation",
                    StringComparison.OrdinalIgnoreCase)
            ) &&
            query.Query.Contains(
                claim.Value,
                StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyCollection<SuggestedCodeLocation>
        GuardCodeLocations(
            IReadOnlyCollection<SuggestedCodeLocation> locations,
            IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        return locations
            .Where(location =>
                IsCodeLocationGrounded(
                    location,
                    strongClaims))
            .ToArray();
    }

    private static bool IsCodeLocationGrounded(
        SuggestedCodeLocation location,
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        var repositoryClaims =
            strongClaims
                .Where(claim =>
                    claim.Authority ==
                    EvidenceAuthority.RepositoryMatch)
                .ToArray();

        var stackClaims =
            strongClaims
                .Where(claim =>
                    claim.Authority ==
                    EvidenceAuthority.RawLog &&
                    claim.Category.Equals(
                        "Stack",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (!string.IsNullOrWhiteSpace(
                location.FilePath) &&
            repositoryClaims.Any(claim =>
                claim.Value.Contains(
                    location.FilePath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(
                location.ClassName) &&
            repositoryClaims.Any(claim =>
                claim.Value.Contains(
                    location.ClassName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(
                location.MethodName) &&
            repositoryClaims.Any(claim =>
                claim.Value.Contains(
                    location.MethodName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(
                location.ClassName) &&
            stackClaims.Any(claim =>
                claim.Value.Contains(
                    location.ClassName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return
            !string.IsNullOrWhiteSpace(
                location.MethodName) &&
            stackClaims.Any(claim =>
                claim.Value.Contains(
                    location.MethodName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static BusinessImpactAssessment
        GuardBusinessImpact(
            BusinessImpactAssessment impact,
            string application,
            string workflow)
    {
        if (!application.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase) &&
            !workflow.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return impact;
        }

        return new BusinessImpactAssessment
        {
            Severity =
                impact.Severity,

            CustomerImpact =
                "Customer impact has not been confirmed from the available evidence.",

            OperationalImpact =
                string.IsNullOrWhiteSpace(
                    impact.OperationalImpact)
                    ? "Operational impact requires validation."
                    : impact.OperationalImpact,

            FinancialImpact =
                impact.FinancialImpact,

            Scope =
                "The affected application/workflow scope has not been confirmed.",

            ConfidenceScore =
                Math.Min(
                    impact.ConfidenceScore,
                    50)
        };
    }

    private static IReadOnlyCollection<ResolutionRecommendation>
        GuardRecommendations(
            IReadOnlyCollection<ResolutionRecommendation>
                recommendations,
            bool timeoutEvidence)
    {
        return recommendations
            .Select(recommendation =>
                GuardRecommendation(
                    recommendation,
                    timeoutEvidence))
            .ToArray();
    }

    private static ResolutionRecommendation
        GuardRecommendation(
            ResolutionRecommendation recommendation,
            bool timeoutEvidence)
    {
        if (!timeoutEvidence)
        {
            return recommendation;
        }

        var content =
            $"{recommendation.Title} {recommendation.Description}";

        if (!ContainsUnsafeTimeoutConclusion(content))
        {
            return recommendation;
        }

        return new ResolutionRecommendation
        {
            Title =
                "Review timeout only after confirming root cause",

            Description =
                "A timeout proves that the configured execution threshold was reached. " +
                "It does not prove that the threshold is too low. Validate query execution, " +
                "blocking, resource pressure, execution plan, and dependency behavior first.",

            RecommendationType =
                "Conditional",

            Risk =
                "Increasing the timeout may mask the underlying performance problem.",

            ConfidenceScore =
                90
        };
    }

    private static IReadOnlyCollection<string> BuildUnknowns(
        IReadOnlyCollection<string> existingUnknowns,
        IReadOnlyCollection<EvidenceClaim> weakClaims,
        string application,
        string workflow,
        bool timeoutEvidence)
    {
        var unknowns =
            new List<string>();

        foreach (var value in existingUnknowns)
        {
            AddUnknown(
                unknowns,
                value);
        }

        if (application.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            AddUnknown(
                unknowns,
                "Application identity has not been confirmed by authoritative evidence.");
        }

        if (workflow.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            AddUnknown(
                unknowns,
                "Affected workflow has not been confirmed by authoritative evidence.");
        }

        if (timeoutEvidence)
        {
            AddUnknown(
                unknowns,
                "The reason the database operation exceeded the execution timeout has not been confirmed.");
        }

        foreach (var claim in
                 weakClaims
                     .Where(claim =>
                         claim.Authority ==
                         EvidenceAuthority.FuzzyContext)
                     .Take(5))
        {
            AddUnknown(
                unknowns,
                $"Candidate context not treated as fact: " +
                $"{claim.Label} = {claim.Value}");
        }

        return unknowns.ToArray();
    }

    private static void AddUnknown(
        ICollection<string> unknowns,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized =
            NormalizeUnknown(
                value);

        var alreadyExists =
            unknowns.Any(existing =>
                UnknownsAreEquivalent(
                    NormalizeUnknown(existing),
                    normalized));

        if (!alreadyExists)
        {
            unknowns.Add(
                value.Trim());
        }
    }

    private static string NormalizeUnknown(
        string value)
    {
        return value
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();
    }

    private static bool UnknownsAreEquivalent(
        string left,
        string right)
    {
        if (left.Equals(
                right,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (RepresentsApplicationIdentity(left) &&
            RepresentsApplicationIdentity(right))
        {
            return true;
        }

        if (RepresentsWorkflow(left) &&
            RepresentsWorkflow(right))
        {
            return true;
        }

        if (RepresentsDatabaseRelationship(left) &&
            RepresentsDatabaseRelationship(right))
        {
            return true;
        }

        return false;
    }

    private static bool RepresentsApplicationIdentity(
        string value)
    {
        return value.Contains(
            "application identity",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool RepresentsWorkflow(
        string value)
    {
        return value.Contains(
                   "affected workflow",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "workflow",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool RepresentsDatabaseRelationship(
        string value)
    {
        return value.Contains(
                   "database object relationship",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "physical database",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int RecalculateOverallConfidence(
        InvestigationReport report,
        IReadOnlyCollection<EvidenceClaim> strongClaims)
    {
        var evidenceScore =
            strongClaims.Count == 0
                ? 25
                : (int)Math.Round(
                    strongClaims.Average(
                        claim =>
                            claim.ConfidenceScore));

        if (report.OverallConfidenceScore <= 0)
        {
            return Math.Clamp(
                evidenceScore,
                20,
                90);
        }

        return Math.Clamp(
            (report.OverallConfidenceScore +
             evidenceScore) / 2,
            20,
            90);
    }

    private static IReadOnlyCollection<InvestigationStep>
    GuardIdentityReferences(
        IReadOnlyCollection<InvestigationStep> steps,
        string application,
        string workflow)
    {
        return steps
            .Select(step =>
            {
                var action =
                    step.Action;

                if (workflow.Equals(
                        "Unknown",
                        StringComparison.OrdinalIgnoreCase))
                {
                    action =
                        RemoveUnconfirmedWorkflowReference(
                            action);
                }

                return new InvestigationStep
                {
                    Sequence =
                        step.Sequence,

                    Title =
                        step.Title,

                    Action =
                        action,

                    Reason =
                        step.Reason,

                    ExpectedOutcome =
                        step.ExpectedOutcome,

                    Priority =
                        step.Priority,

                    ConfidenceScore =
                        step.ConfidenceScore
                };
            })
            .ToArray();
    }

    private static string RemoveUnconfirmedWorkflowReference(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        /*
         * Do not attempt to guess the rejected workflow name here.
         * Replace workflow-specific scope language with neutral language
         * when authoritative workflow identity is unavailable.
         */

        if (value.Contains(
                "determine whether all requests or only specific requests are affected",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Validate whether the issue is still occurring and determine " +
                "whether all requests or only specific requests are affected.";
        }

        return value;
    }
}