using LogAnalyzer.ApplicationIntelligence.Generation;
using LogAnalyzer.ApplicationIntelligence.Ingestion;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.KnowledgeSources;
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

        /*
         * Evidence + declared knowledge.
         */

        services.AddSingleton<
            IApplicationKnowledgeSource,
            LogKnowledgeSource>();

        services.AddSingleton<
            IApplicationKnowledgeSource,
            CatalogKnowledgeSource>();

        /*
         * Repository intelligence.
         */

        services.AddSingleton<
            IRepositoryKnowledgeSource,
            RepositoryKnowledgeSource>();

        services.AddSingleton<
            IApplicationKnowledgeSource>(
                provider =>
                    provider.GetRequiredService<
                        IRepositoryKnowledgeSource>());

        /*
         * OpenAPI intelligence.
         */

        services.AddSingleton<
            IOpenApiKnowledgeSource,
            OpenApiKnowledgeSource>();

        services.AddSingleton<
            IApplicationKnowledgeSource>(
                provider =>
                    provider.GetRequiredService<
                        IOpenApiKnowledgeSource>());

        /*
         * Profile composition + ingestion.
         */

        services.AddSingleton<
            IApplicationProfileBuilder,
            ApplicationProfileBuilder>();

        services.AddSingleton<
            IKnowledgeIngestionService,
            KnowledgeIngestionService>();

        services.AddHostedService<
            ApplicationCatalogInitializationService>();

        return services;
    }
}