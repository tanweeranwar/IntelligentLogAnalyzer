using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IRepositoryKnowledgeSource
    : IApplicationKnowledgeSource
{
    Task<KnowledgeSourceResult> DiscoverRepositoryAsync(
        string repositoryPath,
        string applicationHint = "",
        CancellationToken cancellationToken = default);
}