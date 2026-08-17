using System.Diagnostics;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.KnowledgeSources;

public sealed class CatalogKnowledgeSource
    : IApplicationKnowledgeSource
{
    private readonly IApplicationCatalog _catalog;

    public CatalogKnowledgeSource(
        IApplicationCatalog catalog)
    {
        _catalog =
            catalog ??
            throw new ArgumentNullException(
                nameof(catalog));
    }

    public string SourceName =>
        "Application Catalog";

    public KnowledgeSourceKind SourceKind =>
        KnowledgeSourceKind.KnowledgePackage;

    public Task<KnowledgeSourceResult> DiscoverAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch =
            Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        var package =
            FindPackage(
                request.ApplicationHint);

        if (package is null)
        {
            stopwatch.Stop();

            return Task.FromResult(
                new KnowledgeSourceResult
                {
                    SourceName =
                        SourceName,

                    SourceKind =
                        SourceKind,

                    Duration =
                        stopwatch.Elapsed
                });
        }

        var contributions =
            BuildContributions(
                package.Application);

        stopwatch.Stop();

        return Task.FromResult(
            new KnowledgeSourceResult
            {
                SourceName =
                    SourceName,

                SourceKind =
                    SourceKind,

                Contributions =
                    contributions,

                Duration =
                    stopwatch.Elapsed
            });
    }

    private ApplicationKnowledgePackage? FindPackage(
        string applicationHint)
    {
        if (string.IsNullOrWhiteSpace(
                applicationHint))
        {
            return null;
        }

        var exactId =
            _catalog.GetByApplicationId(
                applicationHint);

        if (exactId is not null)
        {
            return exactId;
        }

        return _catalog
            .GetAll()
            .FirstOrDefault(package =>
                package.Application.Metadata.ApplicationName
                    .Equals(
                        applicationHint,
                        StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyCollection<KnowledgeContribution>
        BuildContributions(
            ApplicationKnowledge application)
    {
        var contributions =
            new List<KnowledgeContribution>();

        var metadata =
            application.Metadata;

        contributions.Add(
            CreateContribution(
                metadata,
                KnowledgeContributionType.ApplicationIdentity,
                "ApplicationId",
                metadata.ApplicationId,
                100,
                true));

        contributions.Add(
            CreateContribution(
                metadata,
                KnowledgeContributionType.ApplicationIdentity,
                "ApplicationName",
                metadata.ApplicationName,
                100,
                true));

        AddIfPresent(
            contributions,
            metadata,
            KnowledgeContributionType.Owner,
            "OwnerTeam",
            metadata.OwnerTeam);

        AddIfPresent(
            contributions,
            metadata,
            KnowledgeContributionType.Version,
            "Version",
            metadata.Version);

        foreach (var technology in metadata.Technologies)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Technology,
                "Technology",
                technology);
        }

        foreach (var environment in metadata.Environments)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Environment,
                "Environment",
                environment);
        }

        foreach (var repository in application.Repositories)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Repository,
                repository.Id,
                repository.Name);
        }

        foreach (var component in application.Components)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Component,
                component.Id,
                component.Name);
        }

        foreach (var workflow in application.Workflows)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Workflow,
                workflow.Id,
                workflow.Name);
        }

        foreach (var dependency in application.Dependencies)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Dependency,
                dependency.Id,
                dependency.Name);
        }

        foreach (var endpoint in application.ApiEndpoints)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.ApiEndpoint,
                endpoint.Id,
                endpoint.Route);
        }

        foreach (var databaseObject in
                 application.DatabaseObjects)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.DatabaseObject,
                databaseObject.Id,
                databaseObject.Name);
        }

        foreach (var runbook in application.Runbooks)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Runbook,
                runbook.Id,
                runbook.Title);
        }

        foreach (var knownIssue in application.KnownIssues)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.KnownIssue,
                knownIssue.Id,
                knownIssue.Title);
        }

        foreach (var configuration in
                 application.Configurations)
        {
            AddIfPresent(
                contributions,
                metadata,
                KnowledgeContributionType.Configuration,
                configuration.Id,
                configuration.Key);
        }

        return contributions;
    }

    private KnowledgeContribution CreateContribution(
        ApplicationMetadata metadata,
        KnowledgeContributionType type,
        string key,
        string value,
        int confidenceScore,
        bool isIdentityEvidence = false)
    {
        return new KnowledgeContribution
        {
            ApplicationId =
                metadata.ApplicationId,

            ApplicationName =
                metadata.ApplicationName,

            Type =
                type,

            Key =
                key,

            Value =
                value,

            ConfidenceScore =
                confidenceScore,

            SourceKind =
                SourceKind,

            SourceName =
                SourceName,

            Evidence =
                "Declared in the application knowledge package.",

            IsIdentityEvidence =
                isIdentityEvidence
        };
    }

    private void AddIfPresent(
        ICollection<KnowledgeContribution> contributions,
        ApplicationMetadata metadata,
        KnowledgeContributionType type,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        contributions.Add(
            CreateContribution(
                metadata,
                type,
                key,
                value,
                100));
    }
}