using LogAnalyzer.Domain.RepositoryIntelligence;

namespace LogAnalyzer.Application.Interfaces;

public interface IRepositoryScanner
{
    Task<RepositoryKnowledge> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken = default);
}