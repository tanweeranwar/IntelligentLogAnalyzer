using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IApplicationContextResolver
{
    Task<ApplicationContextResult> ResolveAsync(
        LogIncident incident,
        CancellationToken cancellationToken = default);
}