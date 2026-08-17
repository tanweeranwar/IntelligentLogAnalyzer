using LogAnalyzer.ApplicationIntelligence.Models;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IApplicationCatalog
{
    IReadOnlyCollection<ApplicationKnowledgePackage> GetAll();

    ApplicationKnowledgePackage? GetByApplicationId(
        string applicationId);

    bool TryGetByApplicationId(
        string applicationId,
        out ApplicationKnowledgePackage? package);

    void ReplaceAll(
        IEnumerable<ApplicationKnowledgePackage> packages);
}