using LogAnalyzer.ApplicationIntelligence.Models;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IApplicationPackageLoader
{
    Task<IReadOnlyCollection<ApplicationKnowledgePackage>> LoadAllAsync(
        CancellationToken cancellationToken = default);

    Task<ApplicationKnowledgePackage> LoadAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default);
}