using LogAnalyzer.ApplicationIntelligence.Generation;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Loading;
using LogAnalyzer.ApplicationIntelligence.Repository;
using LogAnalyzer.ApplicationIntelligence.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.ApplicationIntelligence.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<KnowledgePackageOptions>()
            .Bind(
                configuration.GetSection(
                    KnowledgePackageOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.RootPath),
                "ApplicationIntelligence:RootPath is required.")
            .ValidateOnStart();

        services.AddSingleton<
            IApplicationPackageValidator,
            ApplicationPackageValidator>();

        services.AddSingleton<
            IApplicationPackageLoader,
            JsonApplicationPackageLoader>();

        services.AddSingleton<
            IApplicationCatalog,
            InMemoryApplicationCatalog>();

        services.AddSingleton<
            IRepositoryScanner,
            RepositoryScanner>();

        services.AddHostedService<
            ApplicationCatalogInitializationService>();

        return services;
    }
}