using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IApplicationHealthService
{
    ApplicationHealth Calculate(
        IReadOnlyCollection<LogIncident> incidents);
}