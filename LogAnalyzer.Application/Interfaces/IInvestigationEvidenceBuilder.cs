using LogAnalyzer.Domain.AI;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IInvestigationEvidenceBuilder
{
    InvestigationRequest Build(
        LogIncident incident,
        ApplicationContextResult context,
        IReadOnlyCollection<ErrorSummary> errorPatterns,
        string question = "",
        int maxEvidenceEntries = 10,
        InvestigationMode mode = InvestigationMode.Deep);
}