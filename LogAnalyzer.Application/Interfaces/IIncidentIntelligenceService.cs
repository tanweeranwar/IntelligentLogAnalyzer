using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IIncidentIntelligenceService
{
    IncidentIntelligence Analyze(
        string message,
        string exceptionType,
        int? httpStatusCode,
        int occurrenceCount);
}