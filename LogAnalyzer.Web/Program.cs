using LogAnalyzer.Application.AI;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Extensions;
using LogAnalyzer.Domain.RepositoryIntelligence;
using LogAnalyzer.Infrastructure.AI;
using LogAnalyzer.Infrastructure.Context;
using LogAnalyzer.Infrastructure.Correlation;
using LogAnalyzer.Infrastructure.EventBuilders;
using LogAnalyzer.Infrastructure;
using LogAnalyzer.Infrastructure.Health;
using LogAnalyzer.Infrastructure.Intelligence;
using LogAnalyzer.Infrastructure.Investigation;
using LogAnalyzer.Infrastructure.Parsers;
using LogAnalyzer.Infrastructure.Services;
using LogAnalyzer.Web.Components;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Razor components
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Parsers
// Register the specific parser before the generic fallback parser.
builder.Services.AddScoped<
    ILogParser,
    EventViewerTextLogParser>();

builder.Services.AddScoped<
    ILogParser,
    PlainTextLogParser>();

builder.Services.AddScoped<
    ILogParserResolver,
    LogParserResolver>();

// Event construction and correlation
builder.Services.AddScoped<
    IRawLogEventBuilder,
    MultilineLogEventBuilder>();

builder.Services.AddScoped<
    ILogCorrelationService,
    LogCorrelationService>();

// Incident processing
builder.Services.AddScoped<
    ILogIncidentBuilder,
    LogIncidentBuilder>();

builder.Services.AddScoped<
    IIncidentIntelligenceService,
    IncidentIntelligenceService>();

// Health calculation
builder.Services.AddScoped<
    IApplicationHealthService,
    ApplicationHealthService>();

builder.Services.AddScoped<
    ILogAnalysisPipeline,
    LogAnalysisPipeline>();

builder.Services.AddScoped<
    IApplicationContextResolver,
    JsonApplicationContextResolver>();

builder.Services.AddScoped<
    IInvestigationEvidenceBuilder,
    InvestigationEvidenceBuilder>();

builder.Services.AddScoped<
    IInvestigationPreparationEngine,
    InvestigationPreparationEngine>();

builder.Services.AddSingleton<MockDecisionEngine>();

builder.Services.AddScoped<
    IDecisionEngine,
    ModelDecisionEngine>();

builder.Services.AddScoped<
    IInvestigationService,
    InvestigationService>();

builder.Services.AddApplicationIntelligence(
    builder.Configuration);

builder.Services
    .AddOptions<ModelProviderOptions>()
    .Bind(
        builder.Configuration.GetSection(
            ModelProviderOptions.SectionName));

builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(
        builder.Configuration.GetSection(
            OllamaOptions.SectionName));

builder.Services.AddHttpClient<OllamaModelProvider>(
    (provider, client) =>
    {
        var options =
            provider
                .GetRequiredService<
                    IOptions<OllamaOptions>>()
                .Value;

        client.BaseAddress =
            new Uri(
                options.BaseUrl);

        client.Timeout =
            TimeSpan.FromSeconds(
                Math.Max(
                    options.TimeoutSeconds,
                    30));
    });

builder.Services.AddSingleton<MockModelProvider>();

builder.Services.AddSingleton<ModelProviderResolver>();

builder.Services.AddTransient<IModelProvider>(
    provider =>
        provider
            .GetRequiredService<
                ModelProviderResolver>()
            .Resolve());

builder.Services.AddInfrastructure();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

/* This is for testing */

if (app.Environment.IsDevelopment())
{
    app.MapGet(
        "/dev/repository-source-scan",
        async (
            IRepositoryScanner scanner,
            CancellationToken cancellationToken) =>
        {
            var rootPath =
                Path.GetFullPath(
                    Path.Combine(
                        app.Environment.ContentRootPath,
                        ".."));

            var result =
                await scanner.ScanAsync(
                    new RepositoryScanRequest
                    {
                        Location =
                            rootPath,

                        RepositoryName =
                            "IntelligentLogAnalyzer",

                        Provider =
                            "Local",

                        IncludeTests =
                            false,

                        IncludeGeneratedFiles =
                            false,

                        MaximumFiles =
                            5000
                    },
                    cancellationToken);

            return Results.Ok(
                new
                {
                    Repository =
                        result.Repository.Name,

                    RepositoryId =
                        result.Repository.Id,

                    Provider =
                        result.Repository.Provider,

                    ProjectCount =
                        result.Projects.Count,

                    SourceFileCount =
                        result.Files.Count,

                    TypeCount =
                        result.Files
                            .Sum(file =>
                                file.Types.Count),

                    MethodCount =
                        result.Files
                            .SelectMany(file =>
                                file.Types)
                            .Sum(type =>
                                type.Methods.Count),

                    DatabaseReferenceCount =
                        result.DatabaseReferences.Count,

                    DatabaseReferences =
                        result.DatabaseReferences
                            .Take(50)
                            .Select(reference =>
                                new
                                {
                                    reference.Operation,
                                    reference.Project,
                                    reference.FilePath,
                                    reference.ClassName,
                                    reference.MethodName,
                                    reference.LineNumber,
                                    reference.DatabaseType,
                                    reference.DbContext
                                })
                            .ToArray()
                });
        });
}

//  The above shall be removed during prod release... 

app.Run();