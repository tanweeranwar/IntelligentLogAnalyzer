using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface ILogIncidentBuilder
{
    IReadOnlyCollection<LogIncident> Build(
        IReadOnlyCollection<NormalizedLogEntry> entries);
}