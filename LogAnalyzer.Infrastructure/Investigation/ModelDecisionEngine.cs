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
            "Starting hybrid AI investigation using provider { Provider}. " +
            "Confirmed facts: {ConfirmedFacts}. Candidates: {Candidates}. " +
            "Prompt characters: {PromptLength}.",
            _modelProvider.ProviderName,
            authorityAwareEvidence.ConfirmedFacts.Count,
            authorityAwareEvidence.CandidateContext.Count,
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
        You are a Production Support Engineer.

        CONFIRMED evidence is authoritative.
        CANDIDATE context is not fact.
        Never invent relationships between facts.
        Prefer Unknown over unsupported conclusions.

        SQL error -2 is an execution timeout.
        It does not by itself prove connection instability.
        Reaching a timeout does not prove the timeout value is too low.

        Return only valid JSON.
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

        Analyze only the confirmed evidence.

        Return ONLY JSON:

        {
          "summary": "brief evidence-grounded summary",
          "hypotheses": [
            {
              "cause": "root-cause hypothesis",
              "evidence": ["confirmed supporting fact"],
              "confidence": 0
            }
          ],
          "nextAction": "single best investigation action"
        }

        Requirements:
        - Maximum 2 hypotheses.
        - Confidence must be 0-100.
        - Keep every string concise.
        - Candidate context is not fact.
        - Prefer Unknown over fabrication.
        - Do not recommend increasing timeout just because a timeout occurred.
        - Complete and close the JSON object.
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
                string.IsNullOrWhiteSpace(
                    model.Summary)
                    ? baseline.ExecutiveSummary
                    : model.Summary.Trim(),

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
                model.Hypotheses.Count > 0
                    ? model.Hypotheses
                        .Take(2)
                        .Select(MapRootCause)
                        .ToArray()
                    : baseline.RootCauses,

            InvestigationSteps =
                baseline.InvestigationSteps,

            SuggestedSqlQueries =
                baseline.SuggestedSqlQueries,

            SuggestedCodeLocations =
                baseline.SuggestedCodeLocations,

            Dependencies =
                baseline.Dependencies,

            BusinessImpact =
                baseline.BusinessImpact,

            ResolutionRecommendations =
                baseline.ResolutionRecommendations,

            EvidenceReferences =
                baseline.EvidenceReferences,

            OverallConfidenceScore =
                baseline.OverallConfidenceScore,

            Unknowns =
                baseline.Unknowns,

            Assumptions =
                baseline.Assumptions,

            GeneratedAt =
                DateTimeOffset.UtcNow
        };
    }

    private static NextRecommendedAction MapNextAction(
    string nextAction,
    NextRecommendedAction fallback)
    {
        if (string.IsNullOrWhiteSpace(
                nextAction))
        {
            return fallback;
        }

        return new NextRecommendedAction
        {
            Title =
                "Recommended next action",

            Action =
                nextAction.Trim(),

            Reason =
                "This action was selected from the strongest confirmed evidence.",

            ExpectedOutcome =
                "Evidence that confirms or eliminates the leading hypothesis.",

            SuggestedOwner =
                fallback.SuggestedOwner,

            EstimatedEffort =
                fallback.EstimatedEffort,

            ConfidenceScore =
                Math.Max(
                    fallback.ConfidenceScore,
                    80)
        };
    }

    private static RootCauseHypothesis MapRootCause(
    ModelRootCause model)
    {
        var evidence =
            model.Evidence
                .Where(value =>
                    !string.IsNullOrWhiteSpace(
                        value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();

        return new RootCauseHypothesis
        {
            Title =
                model.Cause,

            Explanation =
                evidence.Length == 0
                    ? "Additional evidence is required to validate this hypothesis."
                    : string.Join(
                        " ",
                        evidence),

            ConfidenceScore =
                Math.Clamp(
                    model.Confidence,
                    0,
                    100),

            SupportingEvidence =
                evidence,

            ContradictingEvidence =
                []
        };
    }

    //private static InvestigationStep MapInvestigationStep(
    //ModelInvestigationStep model)
    //{
    //    return new InvestigationStep
    //    {
    //        Sequence =
    //            model.Sequence,

    //        Title =
    //            $"Investigation step {model.Sequence}",

    //        Action =
    //            model.Action,

    //        Reason =
    //            "Validate the current incident hypothesis using available evidence.",

    //        ExpectedOutcome =
    //            model.Expected,

    //        Priority =
    //            model.Sequence <= 2
    //                ? "High"
    //                : "Medium",

    //        ConfidenceScore =
    //            80
    //    };
    //}

    //private static ResolutionRecommendation
    //MapResolutionRecommendation(
    //    ModelResolutionRecommendation model)
    //{
    //    return new ResolutionRecommendation
    //    {
    //        Title =
    //            model.Recommendation,

    //        Description =
    //            string.IsNullOrWhiteSpace(
    //                model.Condition)
    //                ? model.Recommendation
    //                : $"{model.Recommendation} " +
    //                  $"Condition: {model.Condition}",

    //        RecommendationType =
    //            "Conditional",

    //        Risk =
    //            "Validate the confirmed root cause before implementation.",

    //        ConfidenceScore =
    //            70
    //    };
    //}

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