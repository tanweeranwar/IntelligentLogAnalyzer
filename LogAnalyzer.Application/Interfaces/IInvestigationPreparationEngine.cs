using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Application.Interfaces;

public interface IInvestigationPreparationEngine
{
    ReasoningPackage Prepare(
        InvestigationRequest request);
}