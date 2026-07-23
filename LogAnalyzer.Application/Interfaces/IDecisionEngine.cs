using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Application.Interfaces;

public interface IDecisionEngine
{
    Task<InvestigationReport> AnalyzeAsync(
        ReasoningPackage package,
        CancellationToken cancellationToken = default);
}