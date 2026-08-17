using LogAnalyzer.Application.AI;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.AI;
using LogAnalyzer.Infrastructure.AI;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.Infrastructure.Investigation;

public sealed class ModelDecisionEngine
    : IDecisionEngine
{
    private readonly IModelProvider _modelProvider;
    private readonly MockDecisionEngine _fallbackEngine;
    private readonly InvestigationModelResponseParser _responseParser;
    private readonly InvestigationEvidenceDistiller _evidenceDistiller;
    private readonly ILogger<ModelDecisionEngine> _logger;
    private readonly EvidenceAuthorityEvaluator _authorityEvaluator;
    private readonly InvestigationReportGuardrail _guardrail;
    private readonly AuthorityAwareEvidenceBuilder
    _authorityAwareEvidenceBuilder;

    public ModelDecisionEngine(
        IModelProvider modelProvider,
        MockDecisionEngine fallbackEngine,
        ILogger<ModelDecisionEngine> logger)
    {
        _modelProvider =
            modelProvider ??
            throw new ArgumentNullException(
                nameof(modelProvider));

        _fallbackEngine =
            fallbackEngine ??
            throw new ArgumentNullException(
                nameof(fallbackEngine));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _responseParser =
            new InvestigationModelResponseParser();

        _evidenceDistiller =
            new InvestigationEvidenceDistiller();

        _authorityEvaluator =
            new EvidenceAuthorityEvaluator();

        _guardrail =
            new InvestigationReportGuardrail();

        _authorityAwareEvidenceBuilder =
            new AuthorityAwareEvidenceBuilder();
    }

    public async Task<InvestigationReport> AnalyzeAsync(
        ReasoningPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        cancellationToken.ThrowIfCancellationRequested();

        var baselineReport =
            await _fallbackEngine.AnalyzeAsync(
                package,
                cancellationToken);

        var evidence =
            _evidenceDistiller.Distill(
                package);

        var claims =
            _authorityEvaluator.Evaluate(
                evidence);

        var authorityAwareEvidence =
            _authorityAwareEvidenceBuilder.Build(
                claims);

        var guardedBaselineReport =
            _guardrail.Apply(
                baselineReport,
                claims);

        var request =
            BuildModelRequest(
                baselineReport,
                authorityAwareEvidence,
                package.Metadata);

        _logger.LogInformation(
            "Starting distilled AI investigation using provider {Provider}. Facts: {FactCount}. Prompt characters: {PromptLength}.",
            _modelProvider.ProviderName,
            evidence.Facts.Count,
            request.UserPrompt.Length);

        var response =
            await _modelProvider.GenerateAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessful)
        {
            _logger.LogWarning(
                "AI investigation failed using provider {Provider}. " +
                "Falling back to guarded deterministic report. Error: {Error}",
                response.ProviderName,
                response.ErrorMessage);

            return guardedBaselineReport;
        }

        if (!_responseParser.TryParse(
                response.Content,
                out var modelResult,
                out var parserError))
        {
            _logger.LogWarning(
                "AI response could not be parsed. " +
                "Falling back to guarded deterministic report. Error: {Error}",
                parserError);

            return guardedBaselineReport;
        }

        var report =
            MergeReports(
                baselineReport,
                modelResult);

        var guardedReport =
            _guardrail.Apply(
                report,
                claims);

        _logger.LogInformation(
            "Distilled AI investigation completed using {Provider}/{Model}. " +
            "Input tokens: {InputTokens}. Output tokens: {OutputTokens}. " +
            "Duration: {DurationMs} ms.",
            response.ProviderName,
            response.ModelName,
            response.InputTokenCount,
            response.OutputTokenCount,
            response.Duration.TotalMilliseconds);

        return guardedReport;
    }

    private static ModelRequest BuildModelRequest(
    InvestigationReport baseline,
    AuthorityAwareEvidence evidence,
    IReadOnlyDictionary<string, string> metadata)
    {
        return new ModelRequest
        {
            SystemPrompt =
                BuildSystemPrompt(),

            UserPrompt =
                BuildUserPrompt(
                    baseline,
                    evidence),

            ResponseSchema =
                string.Empty,

            Metadata =
                metadata
        };
    }

    //private static string BuildSystemPrompt()
    //{
    //    return
    //        """
    //    You are a senior Production Support Engineer.

    //    You will receive three categories of information:

    //    CONFIRMED EVIDENCE
    //    These are authoritative observations and explicit identifiers.

    //    CANDIDATE CONTEXT
    //    These are weak, fuzzy, inferred, or knowledge-based matches.
    //    They are NOT facts.

    //    UNKNOWNS
    //    These are facts that have not been established.

    //    Critical reasoning rules:

    //    - Base conclusions primarily on CONFIRMED EVIDENCE.
    //    - Never promote CANDIDATE CONTEXT into a fact without
    //      independent confirmed evidence.
    //    - Never combine unrelated facts into an unsupported relationship.
    //    - If a database object and a database name are separately present,
    //      do not claim that the object belongs to that database unless the
    //      relationship itself is confirmed.
    //    - If a threshold was reached, that proves only that the threshold
    //      was reached. It does not prove the configured threshold is too low.
    //    - A timeout does not by itself prove that increasing the timeout is
    //      the correct fix.
    //    - SQL error -2 indicates an execution timeout, not necessarily a
    //      connection failure.
    //    - Distinguish observed fact from hypothesis.
    //    - Prefer Unknown over fabrication.
    //    - Investigation steps must gather evidence that proves or disproves
    //      the hypotheses.
    //    - Recommendations must remain conditional unless the root cause is
    //      confirmed.
    //    - Return only valid JSON.
    //    """;
    //}

    private static string BuildSystemPrompt()
    {
        return
            """
        You are a senior Production Support Engineer.

        You will receive confirmed evidence, candidate context,
        and explicit unknowns.

        Rules:

        - Base conclusions on confirmed evidence.
        - Candidate context is not fact.
        - Never invent relationships between separately observed facts.
        - SQL error number -2 indicates an execution timeout.
        - SQL error number -2 alone does not establish database connection instability.
        - A command reaching its timeout proves that the execution threshold
          was reached; it does not prove the timeout value is too low.
        - For execution timeouts, investigate the operation itself first:
          execution duration, blocking, waits, execution plan, resource pressure,
          parameter behavior, and downstream/database responsiveness.
        - Do not recommend increasing a timeout until the reason for slow
          execution has been investigated.
        - Separate observed facts from hypotheses.
        - Prefer Unknown over unsupported conclusions.
        - Recommendations must remain conditional unless the root cause is confirmed.
        - Return only valid JSON.
        """;
    }

    private static string BuildUserPrompt(
    InvestigationReport baseline,
    AuthorityAwareEvidence evidence)
    {
        return
            $$"""
        Incident ID: {{baseline.IncidentId}}

        {{evidence.ToPromptText()}}

        Analyze the incident using confirmed evidence first.

        Return ONLY JSON:

        {
          "executiveSummary": "brief evidence-grounded summary",
          "nextAction": {
            "action": "best first investigation action",
            "why": "why this should be checked first",
            "confidenceScore": 0
          },
          "rootCauses": [
                {
              "cause": "hypothesis",
              "evidence": [
                "confirmed evidence supporting it"
              ],
              "confidenceScore": 0
            }
          ],
          "investigationSteps": [
            {
              "sequence": 1,
              "action": "investigation action",
              "expected": "what evidence this will confirm or eliminate"
            }
          ],
          "resolutionRecommendations": [
            {
              "recommendation": "conditional fix direction",
              "condition": "evidence required before applying it"
            }
          ],
          "overallConfidenceScore": 0,
          "unknowns": ["important missing evidence"]
        }

        Requirements:
        - Maximum 2 root causes.
        - Maximum 3 investigation steps.
        - Maximum 2 recommendations.
        - Maximum 5 unknowns.
        - Every string must be concise.
        - Do not identify an application or workflow from candidate context.
        - Do not claim a database/object relationship unless confirmed.
        - Do not recommend increasing a timeout solely because a timeout occurred.
        - Complete and close the JSON object.
        - rootCauses[].evidence MUST always be a JSON array of strings.
        """;
    }

    private static InvestigationReport MergeReports(
        InvestigationReport baseline,
        ModelInvestigationResult model)
    {
        return new InvestigationReport
        {
            IncidentId =
                baseline.IncidentId,

            ApplicationName =
                baseline.ApplicationName,

            Environment =
                baseline.Environment,

            ExecutiveSummary =
                ValueOrFallback(
                    model.ExecutiveSummary,
                    baseline.ExecutiveSummary),

            NextAction =
                MapNextAction(
                    model.NextAction,
                    baseline.NextAction),

            Completeness =
                baseline.Completeness,

            Timeline =
                baseline.Timeline,

            AffectedWorkflow =
                baseline.AffectedWorkflow,

            AffectedComponents =
                baseline.AffectedComponents,

            RootCauses =
                model.RootCauses.Count > 0
                    ? model.RootCauses
                        .Take(3)
                        .Select(MapRootCause)
                        .ToArray()
                    : baseline.RootCauses,

            InvestigationSteps =
                model.InvestigationSteps.Count > 0
                    ? model.InvestigationSteps
                        .OrderBy(step =>
                            step.Sequence)
                        .Take(5)
                        .Select(MapInvestigationStep)
                        .ToArray()
                    : baseline.InvestigationSteps,

            SuggestedSqlQueries =
                baseline.SuggestedSqlQueries,

            SuggestedCodeLocations =
                baseline.SuggestedCodeLocations,

            Dependencies =
                baseline.Dependencies,

            BusinessImpact =
                baseline.BusinessImpact,

            ResolutionRecommendations =
                model.ResolutionRecommendations.Count > 0
                    ? model.ResolutionRecommendations
                        .Take(3)
                        .Select(MapResolutionRecommendation)
                        .ToArray()
                    : baseline.ResolutionRecommendations,

            EvidenceReferences =
                baseline.EvidenceReferences,

            OverallConfidenceScore =
                model.OverallConfidenceScore > 0
                    ? Math.Clamp(
                        model.OverallConfidenceScore,
                        0,
                        100)
                    : baseline.OverallConfidenceScore,

            Unknowns =
                model.Unknowns.Count > 0
                    ? model.Unknowns
                    : baseline.Unknowns,

            Assumptions =
                baseline.Assumptions,

            GeneratedAt =
                DateTimeOffset.UtcNow
        };
    }

    private static NextRecommendedAction MapNextAction(
    ModelNextAction model,
    NextRecommendedAction fallback)
    {
        if (string.IsNullOrWhiteSpace(
                model.Action))
        {
            return fallback;
        }

        return new NextRecommendedAction
        {
            Title =
                "Recommended next action",

            Action =
                model.Action,

            Reason =
                model.Why,

            ExpectedOutcome =
                "Evidence that confirms or eliminates the leading hypothesis.",

            SuggestedOwner =
                fallback.SuggestedOwner,

            EstimatedEffort =
                fallback.EstimatedEffort,

            ConfidenceScore =
                Math.Clamp(
                    model.ConfidenceScore,
                    0,
                    100)
        };
    }

    private static RootCauseHypothesis MapRootCause(
    ModelRootCause model)
    {
        var evidence =
            model.Evidence
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

        return new RootCauseHypothesis
        {
            Title =
                model.Cause,

            Explanation =
                evidence.Length == 0
                    ? "This hypothesis requires additional evidence."
                    : string.Join(
                        " ",
                        evidence),

            ConfidenceScore =
                Math.Clamp(
                    model.ConfidenceScore,
                    0,
                    100),

            SupportingEvidence =
                evidence,

            ContradictingEvidence =
                []
        };
    }

    private static InvestigationStep MapInvestigationStep(
    ModelInvestigationStep model)
    {
        return new InvestigationStep
        {
            Sequence =
                model.Sequence,

            Title =
                $"Investigation step {model.Sequence}",

            Action =
                model.Action,

            Reason =
                "Validate the current incident hypothesis using available evidence.",

            ExpectedOutcome =
                model.Expected,

            Priority =
                model.Sequence <= 2
                    ? "High"
                    : "Medium",

            ConfidenceScore =
                80
        };
    }

    private static ResolutionRecommendation
    MapResolutionRecommendation(
        ModelResolutionRecommendation model)
    {
        return new ResolutionRecommendation
        {
            Title =
                model.Recommendation,

            Description =
                string.IsNullOrWhiteSpace(
                    model.Condition)
                    ? model.Recommendation
                    : $"{model.Recommendation} " +
                      $"Condition: {model.Condition}",

            RecommendationType =
                "Conditional",

            Risk =
                "Validate the confirmed root cause before implementation.",

            ConfidenceScore =
                70
        };
    }

    private static string NormalizePriority(
        string priority)
    {
        if (priority.Equals(
                "High",
                StringComparison.OrdinalIgnoreCase) ||
            priority.Equals(
                "Critical",
                StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        if (priority.Equals(
                "Low",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Low";
        }

        return "Medium";
    }

    private static string ValueOrFallback(
        string? value,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}