using LogAnalyzer.Domain.RepositoryIntelligence;

namespace LogAnalyzer.Application.Interfaces;

public interface IRepositoryKnowledgeStore
{
    Task SaveAsync(
        RepositoryKnowledge knowledge,
        CancellationToken cancellationToken = default);

    Task<RepositoryKnowledge?> GetAsync(
        string repositoryId,
        CancellationToken cancellationToken = default);
}