using LogAnalyzer.Domain.AI;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IInvestigationService
{
    Task<InvestigationReport> InvestigateAsync(
        LogIncident incident,
        IReadOnlyCollection<ErrorSummary> errorPatterns,
        InvestigationMode mode = InvestigationMode.Deep,
        string question = "",
        int maxEvidenceEntries = 10,
        CancellationToken cancellationToken = default);
}