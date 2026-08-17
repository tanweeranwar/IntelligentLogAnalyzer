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

        var request =
            BuildModelRequest(
                baselineReport,
                evidence,
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
                "AI investigation failed using provider {Provider}. Falling back to deterministic report. Error: {Error}",
                response.ProviderName,
                response.ErrorMessage);

            return baselineReport;
        }

        if (!_responseParser.TryParse(
                response.Content,
                out var modelResult,
                out var parserError))
        {
            _logger.LogWarning(
                "AI response could not be parsed. Falling back to deterministic report. Error: {Error}",
                parserError);

            return baselineReport;
        }

        var report =
            MergeReports(
                baselineReport,
                modelResult);

        _logger.LogInformation(
            "Distilled AI investigation completed using {Provider}/{Model}. Input tokens: {InputTokens}. Output tokens: {OutputTokens}. Duration: {DurationMs} ms.",
            response.ProviderName,
            response.ModelName,
            response.InputTokenCount,
            response.OutputTokenCount,
            response.Duration.TotalMilliseconds);

        return report;
    }

    private static ModelRequest BuildModelRequest(
        InvestigationReport baseline,
        DistilledEvidence evidence,
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
                InvestigationModelSchema.JsonSchema,

            Metadata =
                metadata
        };
    }

    private static string BuildSystemPrompt()
    {
        return
            """
            You are a senior Production Support Engineer.

            Use only the supplied incident facts.

            Rules:
            - Facts are authoritative.
            - Separate fact from hypothesis.
            - Never invent missing application, source-code, API, database, or infrastructure details.
            - Rank hypotheses by evidence strength.
            - State uncertainty explicitly.
            - Investigation steps must prove or disprove hypotheses.
            - Do not recommend increasing a timeout merely because a timeout occurred.
            - Keep recommendations operational and concise.
            - Return only valid JSON matching the supplied schema.
            """;
    }

    private static string BuildUserPrompt(
    InvestigationReport baseline,
    DistilledEvidence evidence)
    {
        return
            $$"""
        Incident: {{baseline.IncidentId}}
        Application: {{baseline.ApplicationName}}

        Facts:
        {{evidence.ToPromptText()}}

        Analyze only these facts.

        Return ONLY JSON in exactly this shape:

        {
          "executiveSummary": "brief summary",
          "nextAction": {
            "action": "first thing to investigate",
            "why": "brief reason",
            "confidenceScore": 0
          },
          "rootCauses": [
            {
              "cause": "hypothesis",
              "evidence": "supporting fact",
              "confidenceScore": 0
            }
          ],
          "investigationSteps": [
            {
              "sequence": 1,
              "action": "check",
              "expected": "what it proves"
            }
          ],
          "resolutionRecommendations": [
            {
              "recommendation": "fix direction",
              "condition": "when this applies"
            }
          ],
          "overallConfidenceScore": 0,
          "unknowns": ["missing evidence"]
        }

        Maximum:
        2 rootCauses
        3 investigationSteps
        2 resolutionRecommendations
        4 unknowns

        Keep every string under 120 characters.
        Complete and close the JSON object.
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
        return new RootCauseHypothesis
        {
            Title =
                model.Cause,

            Explanation =
                model.Evidence,

            ConfidenceScore =
                Math.Clamp(
                    model.ConfidenceScore,
                    0,
                    100),

            SupportingEvidence =
                string.IsNullOrWhiteSpace(
                    model.Evidence)
                    ? []
                    : [model.Evidence],

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