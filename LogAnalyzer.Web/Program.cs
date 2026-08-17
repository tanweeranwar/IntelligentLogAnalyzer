using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Infrastructure.Correlation;
using LogAnalyzer.Infrastructure.EventBuilders;
using LogAnalyzer.Infrastructure.Health;
using LogAnalyzer.Infrastructure.Intelligence;
using LogAnalyzer.Infrastructure.Parsers;
using LogAnalyzer.Web.Components;
using LogAnalyzer.Infrastructure.Services;
using LogAnalyzer.Infrastructure.Context;
using LogAnalyzer.Infrastructure.Investigation;
using LogAnalyzer.ApplicationIntelligence.Extensions;
using LogAnalyzer.Application.AI;
using LogAnalyzer.Infrastructure.AI;
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

app.Run();