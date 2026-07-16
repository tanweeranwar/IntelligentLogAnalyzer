using LogAnalyzer.Web.Components;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Infrastructure.Parsers;
using LogAnalyzer.Infrastructure.Intelligence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ILogParser, PlainTextLogParser>();
builder.Services.AddScoped<
    IIncidentIntelligenceService,
    IncidentIntelligenceService>();
builder.Services.AddScoped<
    ILogParser,
    PlainTextLogParser>();
builder.Services.AddScoped<
    ILogIncidentBuilder,
    LogIncidentBuilder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
