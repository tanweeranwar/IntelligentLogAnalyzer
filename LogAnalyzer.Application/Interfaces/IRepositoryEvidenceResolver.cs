using LogAnalyzer.Domain.RepositoryIntelligence;

namespace LogAnalyzer.Application.Interfaces;

public interface IRepositoryEvidenceResolver
{
    Task<IReadOnlyCollection<RepositoryEvidenceMatch>>
        ResolveAsync(
            RepositoryEvidenceQuery query,
            CancellationToken cancellationToken = default);
}