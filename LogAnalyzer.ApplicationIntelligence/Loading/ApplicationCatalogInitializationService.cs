using LogAnalyzer.ApplicationIntelligence.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.ApplicationIntelligence.Loading;

public sealed class ApplicationCatalogInitializationService
    : IHostedService
{
    private readonly IApplicationPackageLoader _loader;
    private readonly IApplicationCatalog _catalog;
    private readonly ILogger<ApplicationCatalogInitializationService>
        _logger;

    public ApplicationCatalogInitializationService(
        IApplicationPackageLoader loader,
        IApplicationCatalog catalog,
        ILogger<ApplicationCatalogInitializationService> logger)
    {
        _loader =
            loader ??
            throw new ArgumentNullException(
                nameof(loader));

        _catalog =
            catalog ??
            throw new ArgumentNullException(
                nameof(catalog));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Loading application intelligence packages.");

        var packages =
            await _loader.LoadAllAsync(
                cancellationToken);

        _catalog.ReplaceAll(packages);

        _logger.LogInformation(
            "Application intelligence catalog loaded with " +
            "{ApplicationCount} application package(s).",
            packages.Count);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}