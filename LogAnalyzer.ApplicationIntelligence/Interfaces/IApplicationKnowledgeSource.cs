using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IApplicationKnowledgeSource
{
    string SourceName { get; }

    KnowledgeSourceKind SourceKind { get; }

    Task<KnowledgeSourceResult> DiscoverAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}