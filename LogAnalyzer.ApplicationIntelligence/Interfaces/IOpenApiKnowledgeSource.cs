using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IOpenApiKnowledgeSource
    : IApplicationKnowledgeSource
{
    Task<KnowledgeSourceResult> DiscoverOpenApiAsync(
        string documentPath,
        string applicationHint = "",
        CancellationToken cancellationToken = default);
}