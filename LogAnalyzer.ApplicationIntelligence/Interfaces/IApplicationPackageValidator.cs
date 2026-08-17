using LogAnalyzer.ApplicationIntelligence.Models;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IApplicationPackageValidator
{
    void Validate(
        ApplicationKnowledgePackage package);
}