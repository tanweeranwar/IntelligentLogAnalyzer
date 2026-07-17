using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface ILogCorrelationService
{
    IReadOnlyCollection<NormalizedLogEntry> Correlate(
        IReadOnlyCollection<NormalizedLogEntry> entries);
}