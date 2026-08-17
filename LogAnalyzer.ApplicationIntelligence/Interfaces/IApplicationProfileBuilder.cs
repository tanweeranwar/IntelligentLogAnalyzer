using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IApplicationProfileBuilder
{
    Task<ApplicationProfile> BuildAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}