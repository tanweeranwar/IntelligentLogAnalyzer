using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Infrastructure.RepositoryIntelligence.Scanning;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddSingleton<
            CSharpDatabaseOperationExtractor>();

        services.AddSingleton<
            CSharpContextConfigurationExtractor>();

        services.AddSingleton<
            CSharpSourceAnalyzer>();

        services.AddScoped<
            IRepositoryScanner,
            LocalRepositoryScanner>();

        return services;
    }
}