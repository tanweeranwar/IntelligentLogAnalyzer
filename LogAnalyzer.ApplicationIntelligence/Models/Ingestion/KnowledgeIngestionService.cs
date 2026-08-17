using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;
using LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

namespace LogAnalyzer.ApplicationIntelligence.Ingestion;

public sealed class KnowledgeIngestionService
    : IKnowledgeIngestionService
{
    private readonly IApplicationProfileBuilder
        _profileBuilder;

    public KnowledgeIngestionService(
        IApplicationProfileBuilder profileBuilder)
    {
        _profileBuilder =
            profileBuilder ??
            throw new ArgumentNullException(
                nameof(profileBuilder));
    }

    public async Task<KnowledgeIngestionResult> IngestAsync(
        KnowledgeIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var enabledSources =
            request.Sources
                .Where(source => source.IsEnabled)
                .ToArray();

        var discoveryRequest =
            new ApplicationDiscoveryRequest
            {
                ApplicationHint =
                    request.ApplicationHint,

                EnvironmentHint =
                    request.EnvironmentHint,

                Evidence =
                    request.Evidence,

                Metadata =
                    BuildDiscoveryMetadata(
                        request.Metadata,
                        enabledSources)
            };

        var profile =
            await _profileBuilder.BuildAsync(
                discoveryRequest,
                cancellationToken);

        return new KnowledgeIngestionResult
        {
            Profile =
                profile,

            ProcessedSources =
                enabledSources,

            Warnings =
                profile.Warnings,

            CompletedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private static IReadOnlyDictionary<string, string>
        BuildDiscoveryMetadata(
            IReadOnlyDictionary<string, string> metadata,
            IReadOnlyCollection<KnowledgeSourceDescriptor>
                sources)
    {
        var result =
            new Dictionary<string, string>(
                metadata,
                StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var prefix =
                $"Source:{source.Id}";

            result[$"{prefix}:Type"] =
                source.Type.ToString();

            result[$"{prefix}:Name"] =
                source.Name;

            result[$"{prefix}:Location"] =
                source.Location;

            foreach (var pair in source.Metadata)
            {
                result[
                    $"{prefix}:Metadata:{pair.Key}"] =
                    pair.Value;
            }
        }

        return result;
    }
}