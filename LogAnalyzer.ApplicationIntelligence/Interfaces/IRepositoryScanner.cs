using LogAnalyzer.ApplicationIntelligence.Models.Generation;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IRepositoryScanner
{
    Task<RepositoryScanResult> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}