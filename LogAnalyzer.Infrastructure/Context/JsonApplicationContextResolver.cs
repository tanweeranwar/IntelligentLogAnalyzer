using System.Text.Json;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace LogAnalyzer.Infrastructure.Context;

public sealed class JsonApplicationContextResolver
    : IApplicationContextResolver
{
    private const int MinimumComponentScore = 20;
    private const int MinimumWorkflowScore = 20;
    private const int MinimumKnownIssueScore = 30;

    private readonly string _knowledgeFilePath;

    private readonly SemaphoreSlim _loadLock =
        new(1, 1);

    private ApplicationKnowledgeBase? _knowledgeBase;

    public JsonApplicationContextResolver(
        IConfiguration configuration)
    {
        var configuredPath =
            configuration[
                "ApplicationKnowledge:FilePath"];

        _knowledgeFilePath =
            string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(
                    AppContext.BaseDirectory,
                    "Knowledge",
                    "application-architecture.json")
                : Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(
                        AppContext.BaseDirectory,
                        configuredPath);
    }

    public async Task<ApplicationContextResult> ResolveAsync(
        LogIncident incident,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var knowledgeBase =
            await LoadKnowledgeBaseAsync(
                cancellationToken);

        var components = knowledgeBase.Components
            .Select(component =>
                MatchComponent(
                    component,
                    incident))
            .Where(match =>
                match.MatchScore >=
                MinimumComponentScore)
            .OrderByDescending(match =>
                match.MatchScore)
            .Take(5)
            .ToArray();

        var workflows = knowledgeBase.Workflows
            .Select(workflow =>
                MatchWorkflow(
                    workflow,
                    incident,
                    components))
            .Where(match =>
                match.MatchScore >=
                MinimumWorkflowScore)
            .OrderByDescending(match =>
                match.MatchScore)
            .Take(3)
            .ToArray();

        var knownIssues = knowledgeBase.KnownIssues
            .Select(issue =>
                MatchKnownIssue(
                    issue,
                    incident))
            .Where(match =>
                match.MatchScore >=
                MinimumKnownIssueScore)
            .OrderByDescending(match =>
                match.MatchScore)
            .Take(3)
            .ToArray();

        var confidenceScore =
            CalculateOverallConfidence(
                components,
                workflows,
                knownIssues);

        return new ApplicationContextResult
        {
            ApplicationName =
                knowledgeBase.ApplicationName,

            IncidentId =
                incident.IncidentId,

            ConfidenceScore =
                confidenceScore,

            Components =
                components,

            Workflows =
                workflows,

            KnownIssues =
                knownIssues,

            Dependencies =
                components
                    .SelectMany(component =>
                        component.Dependencies)
                    .Concat(
                        workflows.SelectMany(workflow =>
                            workflow.Dependencies))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            DatabaseObjects =
                components
                    .SelectMany(component =>
                        component.DatabaseObjects)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            InvestigationHints =
                components
                    .SelectMany(component =>
                        component.InvestigationHints)
                    .Concat(
                        workflows.SelectMany(workflow =>
                            workflow.InvestigationHints))
                    .Concat(
                        knownIssues.SelectMany(issue =>
                            issue.InvestigationSteps))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
        };
    }

    private async Task<ApplicationKnowledgeBase>
        LoadKnowledgeBaseAsync(
            CancellationToken cancellationToken)
    {
        if (_knowledgeBase is not null)
        {
            return _knowledgeBase;
        }

        await _loadLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_knowledgeBase is not null)
            {
                return _knowledgeBase;
            }

            if (!File.Exists(_knowledgeFilePath))
            {
                throw new FileNotFoundException(
                    "The application knowledge file was not found.",
                    _knowledgeFilePath);
            }

            await using var stream =
                File.OpenRead(_knowledgeFilePath);

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            _knowledgeBase =
                await JsonSerializer.DeserializeAsync<
                    ApplicationKnowledgeBase>(
                    stream,
                    options,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The application knowledge file is empty or invalid.");

            return _knowledgeBase;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static MatchedApplicationComponent
        MatchComponent(
            ApplicationComponent component,
            LogIncident incident)
    {
        var score = 0;

        var reasons =
            new List<string>();

        if (MatchesApiPath(
                incident.ApiPath,
                component.ApiPaths))
        {
            score += 50;

            reasons.Add(
                $"API matched {incident.ApiPath}.");
        }

        if (MatchesValue(
                incident.ExceptionType,
                component.ExceptionTypes))
        {
            score += 25;

            reasons.Add(
                $"Exception matched {incident.ExceptionType}.");
        }

        if (MatchesValue(
                incident.ServerName,
                component.Servers))
        {
            score += 15;

            reasons.Add(
                $"Server matched {incident.ServerName}.");
        }

        var content =
            BuildIncidentSearchText(incident);

        var keywordMatches =
            component.Keywords
                .Where(keyword =>
                    Contains(
                        content,
                        keyword))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (keywordMatches.Length > 0)
        {
            score += Math.Min(
                keywordMatches.Length * 10,
                30);

            reasons.Add(
                $"Matched keywords: {string.Join(", ", keywordMatches)}.");
        }

        return new MatchedApplicationComponent
        {
            Name =
                component.Name,

            Type =
                component.Type,

            Description =
                component.Description,

            MatchScore =
                Math.Min(score, 100),

            MatchReasons =
                reasons,

            Dependencies =
                component.Dependencies,

            DatabaseObjects =
                component.DatabaseObjects,

            InvestigationHints =
                component.InvestigationHints
        };
    }

    private static MatchedApplicationWorkflow
        MatchWorkflow(
            ApplicationWorkflow workflow,
            LogIncident incident,
            IReadOnlyCollection<
                MatchedApplicationComponent> components)
    {
        var score = 0;

        var reasons =
            new List<string>();

        if (MatchesApiPath(
                incident.ApiPath,
                workflow.ApiPaths))
        {
            score += 50;

            reasons.Add(
                $"API matched {incident.ApiPath}.");
        }

        var matchedComponentNames =
            components
                .Select(component =>
                    component.Name)
                .Where(componentName =>
                    workflow.Components.Any(
                        workflowComponent =>
                            string.Equals(
                                workflowComponent,
                                componentName,
                                StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        if (matchedComponentNames.Length > 0)
        {
            score += Math.Min(
                matchedComponentNames.Length * 15,
                30);

            reasons.Add(
                $"Matched components: {string.Join(", ", matchedComponentNames)}.");
        }

        var content =
            BuildIncidentSearchText(incident);

        var keywordMatches =
            workflow.Keywords
                .Where(keyword =>
                    Contains(
                        content,
                        keyword))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (keywordMatches.Length > 0)
        {
            score += Math.Min(
                keywordMatches.Length * 10,
                30);

            reasons.Add(
                $"Matched workflow keywords: {string.Join(", ", keywordMatches)}.");
        }

        return new MatchedApplicationWorkflow
        {
            Name =
                workflow.Name,

            Description =
                workflow.Description,

            MatchScore =
                Math.Min(score, 100),

            MatchReasons =
                reasons,

            Steps =
                workflow.Steps,

            Dependencies =
                workflow.Dependencies,

            InvestigationHints =
                workflow.InvestigationHints
        };
    }

    private static MatchedKnownIssue
        MatchKnownIssue(
            KnownApplicationIssue issue,
            LogIncident incident)
    {
        var score = 0;

        var reasons =
            new List<string>();

        if (MatchesApiPath(
                incident.ApiPath,
                issue.ApiPaths))
        {
            score += 35;

            reasons.Add(
                $"API matched {incident.ApiPath}.");
        }

        if (MatchesValue(
                incident.ExceptionType,
                issue.ExceptionTypes))
        {
            score += 30;

            reasons.Add(
                $"Exception matched {incident.ExceptionType}.");
        }

        if (incident.HttpStatusCode.HasValue &&
            issue.HttpStatusCodes.Contains(
                incident.HttpStatusCode.Value))
        {
            score += 15;

            reasons.Add(
                $"HTTP status matched {incident.HttpStatusCode.Value}.");
        }

        var content =
            BuildIncidentSearchText(incident);

        var messageMatches =
            issue.MessagePatterns
                .Where(pattern =>
                    Contains(
                        content,
                        pattern))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (messageMatches.Length > 0)
        {
            score += Math.Min(
                messageMatches.Length * 20,
                40);

            reasons.Add(
                $"Matched message patterns: {string.Join(", ", messageMatches)}.");
        }

        return new MatchedKnownIssue
        {
            Title =
                issue.Title,

            Description =
                issue.Description,

            MatchScore =
                Math.Min(score, 100),

            MatchReasons =
                reasons,

            LikelyCauses =
                issue.LikelyCauses,

            InvestigationSteps =
                issue.InvestigationSteps,

            ResolutionSteps =
                issue.ResolutionSteps,

            SuggestedQueries =
                issue.SuggestedQueries
        };
    }

    private static string BuildIncidentSearchText(
        LogIncident incident)
    {
        var entryContent =
            incident.Entries
                .Select(entry =>
                    string.Join(
                        Environment.NewLine,
                        entry.Message,
                        entry.ExceptionType,
                        entry.StackTrace,
                        entry.RawContent));

        return string.Join(
            Environment.NewLine,
            new[]
            {
                incident.Title,
                incident.ExceptionType,
                incident.ApiPath,
                incident.ServerName,
                incident.Environment
            }.Concat(entryContent));
    }

    private static bool MatchesApiPath(
        string? incidentApiPath,
        IEnumerable<string> configuredApiPaths)
    {
        if (string.IsNullOrWhiteSpace(
                incidentApiPath))
        {
            return false;
        }

        return configuredApiPaths.Any(
            configuredPath =>
                !string.IsNullOrWhiteSpace(
                    configuredPath) &&
                incidentApiPath.Contains(
                    configuredPath,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesValue(
        string? incidentValue,
        IEnumerable<string> configuredValues)
    {
        if (string.IsNullOrWhiteSpace(
                incidentValue))
        {
            return false;
        }

        return configuredValues.Any(
            configuredValue =>
                !string.IsNullOrWhiteSpace(
                    configuredValue) &&
                incidentValue.Contains(
                    configuredValue,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool Contains(
        string source,
        string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               source.Contains(
                   value,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateOverallConfidence(
        IReadOnlyCollection<
            MatchedApplicationComponent> components,
        IReadOnlyCollection<
            MatchedApplicationWorkflow> workflows,
        IReadOnlyCollection<
            MatchedKnownIssue> knownIssues)
    {
        var scores =
            components
                .Select(component =>
                    component.MatchScore)
                .Concat(
                    workflows.Select(workflow =>
                        workflow.MatchScore))
                .Concat(
                    knownIssues.Select(issue =>
                        issue.MatchScore))
                .OrderByDescending(score =>
                    score)
                .Take(3)
                .ToArray();

        if (scores.Length == 0)
        {
            return 0;
        }

        return (int)Math.Round(
            scores.Average());
    }
}