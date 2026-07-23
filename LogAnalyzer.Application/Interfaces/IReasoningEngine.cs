using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Application.Interfaces;

public interface IReasoningEngine
{
    Task<InvestigationReport> AnalyzeAsync(
        InvestigationRequest request,
        CancellationToken cancellationToken = default);
}