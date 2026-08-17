using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Generation;

public sealed class ApplicationProfileBuilder
    : IApplicationProfileBuilder
{
    private const int MinimumIdentificationScore = 60;

    private readonly IReadOnlyCollection<
        IApplicationKnowledgeSource> _sources;

    public ApplicationProfileBuilder(
        IEnumerable<IApplicationKnowledgeSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources =
            sources.ToArray();
    }

    public async Task<ApplicationProfile> BuildAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceResults =
            new List<KnowledgeSourceResult>();

        var warnings =
            new List<string>();

        /*
         * Phase 1 — evidence discovery.
         *
         * These sources can operate before an application identity
         * has been established.
         */

        var discoverySources =
            _sources
                .Where(source =>
                    source.SourceKind is
                        KnowledgeSourceKind.Log or
                        KnowledgeSourceKind.HistoricalIncident)
                .ToArray();

        foreach (var source in discoverySources)
        {
            var result =
                await ExecuteSourceAsync(
                    source,
                    request,
                    warnings,
                    cancellationToken);

            if (result is not null)
            {
                sourceResults.Add(result);
            }
        }

        var initialContributions =
            GetValidContributions(
                sourceResults);

        var initialCandidates =
            BuildCandidates(
                initialContributions);

        var selectedCandidate =
            SelectCandidate(
                initialCandidates);

        var discoveredApplicationId =
            selectedCandidate?.ApplicationId;

        if (string.IsNullOrWhiteSpace(
                discoveredApplicationId) &&
            !string.IsNullOrWhiteSpace(
                request.ApplicationHint))
        {
            discoveredApplicationId =
                request.ApplicationHint;
        }

        /*
         * Phase 2 — enrichment.
         *
         * Repository, package, OpenAPI, document and source-code
         * knowledge can now enrich the investigation.
         *
         * Repository sources are also permitted when no application
         * is identified because a repository may itself be used to
         * bootstrap a new application profile.
         */

        var enrichmentRequest =
            new ApplicationDiscoveryRequest
            {
                ApplicationHint =
                    discoveredApplicationId ??
                    string.Empty,

                EnvironmentHint =
                    request.EnvironmentHint,

                Evidence =
                    request.Evidence,

                Metadata =
                    request.Metadata
            };

        var enrichmentSources =
            _sources
                .Where(source =>
                    source.SourceKind is not
                        KnowledgeSourceKind.Log and not
                        KnowledgeSourceKind.HistoricalIncident)
                .ToArray();

        foreach (var source in enrichmentSources)
        {
            /*
             * Catalog knowledge requires an identified application.
             *
             * Repository/OpenAPI/document knowledge does not.
             */

            if (source.SourceKind ==
                    KnowledgeSourceKind.KnowledgePackage &&
                string.IsNullOrWhiteSpace(
                    enrichmentRequest.ApplicationHint))
            {
                continue;
            }

            var result =
                await ExecuteSourceAsync(
                    source,
                    enrichmentRequest,
                    warnings,
                    cancellationToken);

            if (result is not null)
            {
                sourceResults.Add(result);
            }
        }

        var allContributions =
            GetValidContributions(
                sourceResults);

        var finalCandidates =
            BuildCandidates(
                allContributions);

        var selected =
            SelectCandidate(
                finalCandidates);

        var identified =
            selected is not null &&
            selected.ConfidenceScore >=
                MinimumIdentificationScore;

        var applicationId =
            identified
                ? selected!.ApplicationId
                : discoveredApplicationId
                  ?? string.Empty;

        var applicationName =
            identified
                ? selected!.ApplicationName
                : applicationId;

        var relevantContributions =
            FilterRelevantContributions(
                allContributions,
                applicationId);

        return new ApplicationProfile
        {
            ApplicationId =
                applicationId,

            ApplicationName =
                applicationName,

            IdentificationConfidenceScore =
                identified
                    ? selected!.ConfidenceScore
                    : 0,

            Candidates =
                finalCandidates,

            Contributions =
                relevantContributions,

            SourceResults =
                sourceResults,

            Warnings =
                warnings,

            GeneratedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private static async Task<KnowledgeSourceResult?>
        ExecuteSourceAsync(
            IApplicationKnowledgeSource source,
            ApplicationDiscoveryRequest request,
            ICollection<string> warnings,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result =
                await source.DiscoverAsync(
                    request,
                    cancellationToken);

            foreach (var warning in
                     result.Warnings)
            {
                warnings.Add(
                    $"{source.SourceName}: " +
                    warning);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add(
                $"{source.SourceName} could not contribute " +
                $"knowledge: {ex.Message}");

            return null;
        }
    }

    private static IReadOnlyCollection<KnowledgeContribution>
        GetValidContributions(
            IEnumerable<KnowledgeSourceResult> results)
    {
        return results
            .SelectMany(result =>
                result.Contributions)
            .Where(IsValidContribution)
            .ToArray();
    }

    private static IReadOnlyCollection<ApplicationProfileCandidate>
        BuildCandidates(
            IReadOnlyCollection<KnowledgeContribution>
                contributions)
    {
        var identityEvidence =
            contributions
                .Where(contribution =>
                    contribution.IsIdentityEvidence)
                .Where(contribution =>
                    !string.IsNullOrWhiteSpace(
                        contribution.ApplicationId))
                .ToArray();

        return identityEvidence
            .GroupBy(
                contribution =>
                    contribution.ApplicationId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var evidence =
                    group
                        .OrderByDescending(item =>
                            item.ConfidenceScore)
                        .ToArray();

                var applicationName =
                    evidence
                        .Select(item =>
                            item.ApplicationName)
                        .FirstOrDefault(value =>
                            !string.IsNullOrWhiteSpace(value))
                    ?? group.Key;

                return new ApplicationProfileCandidate
                {
                    ApplicationId =
                        group.Key,

                    ApplicationName =
                        applicationName,

                    ConfidenceScore =
                        CombineConfidence(
                            evidence.Select(item =>
                                item.ConfidenceScore)),

                    IdentityEvidence =
                        evidence
                };
            })
            .OrderByDescending(candidate =>
                candidate.ConfidenceScore)
            .ThenBy(candidate =>
                candidate.ApplicationName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ApplicationProfileCandidate?
        SelectCandidate(
            IReadOnlyCollection<ApplicationProfileCandidate>
                candidates)
    {
        var ordered =
            candidates
                .OrderByDescending(candidate =>
                    candidate.ConfidenceScore)
                .ToArray();

        if (ordered.Length == 0)
        {
            return null;
        }

        var first =
            ordered[0];

        if (first.ConfidenceScore <
            MinimumIdentificationScore)
        {
            return null;
        }

        if (ordered.Length > 1)
        {
            var second =
                ordered[1];

            if (first.ConfidenceScore -
                second.ConfidenceScore < 10)
            {
                return null;
            }
        }

        return first;
    }

    private static IReadOnlyCollection<KnowledgeContribution>
        FilterRelevantContributions(
            IReadOnlyCollection<KnowledgeContribution>
                contributions,
            string applicationId)
    {
        if (string.IsNullOrWhiteSpace(
                applicationId))
        {
            return contributions
                .Where(contribution =>
                    string.IsNullOrWhiteSpace(
                        contribution.ApplicationId))
                .ToArray();
        }

        return contributions
            .Where(contribution =>
                string.IsNullOrWhiteSpace(
                    contribution.ApplicationId) ||
                contribution.ApplicationId.Equals(
                    applicationId,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static int CombineConfidence(
        IEnumerable<int> scores)
    {
        var probabilityOfNoMatch =
            1d;

        foreach (var rawScore in scores)
        {
            var score =
                Math.Clamp(
                    rawScore,
                    0,
                    100);

            probabilityOfNoMatch *=
                1d -
                score / 100d;
        }

        var combined =
            1d -
            probabilityOfNoMatch;

        return Math.Clamp(
            (int)Math.Round(
                combined * 100d),
            0,
            100);
    }

    private static bool IsValidContribution(
        KnowledgeContribution contribution)
    {
        return contribution is not null &&
               !string.IsNullOrWhiteSpace(
                   contribution.Value);
    }
}