using System.Diagnostics;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;
using LogAnalyzer.ApplicationIntelligence.Models.Generation;

namespace LogAnalyzer.ApplicationIntelligence.KnowledgeSources;

public sealed class RepositoryKnowledgeSource
    : IRepositoryKnowledgeSource
{
    private const string RepositorySourcePrefix =
        "Source:";

    private readonly IRepositoryScanner _repositoryScanner;

    public RepositoryKnowledgeSource(
        IRepositoryScanner repositoryScanner)
    {
        _repositoryScanner =
            repositoryScanner ??
            throw new ArgumentNullException(
                nameof(repositoryScanner));
    }

    public string SourceName =>
        "Repository Intelligence";

    public KnowledgeSourceKind SourceKind =>
        KnowledgeSourceKind.SourceCode;

    public async Task<KnowledgeSourceResult> DiscoverAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var repositoryPaths =
            ExtractRepositoryPaths(
                request.Metadata);

        if (repositoryPaths.Count == 0)
        {
            return new KnowledgeSourceResult
            {
                SourceName =
                    SourceName,

                SourceKind =
                    SourceKind
            };
        }

        var stopwatch =
            Stopwatch.StartNew();

        var contributions =
            new List<KnowledgeContribution>();

        var warnings =
            new List<string>();

        foreach (var repositoryPath in repositoryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result =
                    await DiscoverRepositoryAsync(
                        repositoryPath,
                        request.ApplicationHint,
                        cancellationToken);

                contributions.AddRange(
                    result.Contributions);

                warnings.AddRange(
                    result.Warnings);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Repository '{repositoryPath}' could not be analyzed: " +
                    ex.Message);
            }
        }

        stopwatch.Stop();

        return new KnowledgeSourceResult
        {
            SourceName =
                SourceName,

            SourceKind =
                SourceKind,

            Contributions =
                Deduplicate(
                    contributions),

            Warnings =
                warnings,

            Duration =
                stopwatch.Elapsed
        };
    }

    public async Task<KnowledgeSourceResult>
        DiscoverRepositoryAsync(
            string repositoryPath,
            string applicationHint = "",
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            repositoryPath);

        var stopwatch =
            Stopwatch.StartNew();

        var scanResult =
            await _repositoryScanner.ScanAsync(
                repositoryPath,
                cancellationToken);

        var contributions =
            BuildContributions(
                scanResult,
                applicationHint);

        stopwatch.Stop();

        return new KnowledgeSourceResult
        {
            SourceName =
                SourceName,

            SourceKind =
                SourceKind,

            Contributions =
                contributions,

            Warnings =
                scanResult.Warnings,

            Duration =
                stopwatch.Elapsed
        };
    }

    private IReadOnlyCollection<KnowledgeContribution>
        BuildContributions(
            RepositoryScanResult scanResult,
            string applicationHint)
    {
        var contributions =
            new List<KnowledgeContribution>();

        var repositoryName =
            GetRepositoryName(
                scanResult.RootPath);

        contributions.Add(
            CreateContribution(
                applicationHint,
                KnowledgeContributionType.Repository,
                "Repository",
                repositoryName,
                100,
                $"Repository root: {scanResult.RootPath}",
                [
                    "repository"
                ]));

        foreach (var solutionFile in
                 scanResult.SolutionFiles)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Repository,
                    "Solution",
                    solutionFile,
                    100,
                    $"Solution discovered in repository: " +
                    solutionFile,
                    [
                        "repository",
                        "solution"
                    ]));
        }

        foreach (var technology in
                 scanResult.Technologies)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Technology,
                    "Technology",
                    technology,
                    95,
                    $"Technology discovered from repository " +
                    $"structure and project metadata: {technology}",
                    [
                        "repository",
                        "technology"
                    ]));
        }

        foreach (var project in
                 scanResult.Projects)
        {
            AddProjectContributions(
                contributions,
                project,
                applicationHint);
        }

        AddFileCategoryContributions(
            contributions,
            scanResult,
            applicationHint);

        return Deduplicate(
            contributions);
    }

    private void AddProjectContributions(
        ICollection<KnowledgeContribution> contributions,
        RepositoryProject project,
        string applicationHint)
    {
        contributions.Add(
            CreateContribution(
                applicationHint,
                KnowledgeContributionType.Component,
                "Project",
                project.Name,
                95,
                $"Project '{project.Name}' discovered at " +
                $"'{project.RelativePath}'.",
                [
                    "repository",
                    "project",
                    project.ProjectType
                ]));

        if (!string.IsNullOrWhiteSpace(
                project.TargetFramework))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Technology,
                    "TargetFramework",
                    project.TargetFramework,
                    100,
                    $"Project '{project.Name}' targets " +
                    $"{project.TargetFramework}.",
                    [
                        "repository",
                        "framework"
                    ]));
        }

        foreach (var technology in
                 project.Technologies)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Technology,
                    project.Name,
                    technology,
                    95,
                    $"Project '{project.Name}' uses " +
                    $"{technology}.",
                    [
                        "repository",
                        "technology"
                    ]));
        }

        foreach (var projectReference in
                 project.ProjectReferences)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Dependency,
                    project.Name,
                    NormalizeProjectReference(
                        projectReference),
                    90,
                    $"Project '{project.Name}' references " +
                    $"'{projectReference}'.",
                    [
                        "repository",
                        "project-reference"
                    ]));
        }

        foreach (var packageReference in
                 project.PackageReferences)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Dependency,
                    project.Name,
                    packageReference,
                    85,
                    $"Project '{project.Name}' references NuGet " +
                    $"package '{packageReference}'.",
                    [
                        "repository",
                        "package-reference"
                    ]));
        }
    }

    private void AddFileCategoryContributions(
        ICollection<KnowledgeContribution> contributions,
        RepositoryScanResult scanResult,
        string applicationHint)
    {
        foreach (var item in
                 scanResult.FilesByCategory)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Other,
                    $"FileCategory:{item.Key}",
                    item.Value.ToString(),
                    100,
                    $"Repository contains {item.Value} " +
                    $"{item.Key} file(s).",
                    [
                        "repository",
                        "file-category",
                        item.Key
                    ]));
        }

        var sqlFiles =
            scanResult.Files
                .Where(file =>
                    file.Extension.Equals(
                        ".sql",
                        StringComparison.OrdinalIgnoreCase))
                .Take(100)
                .ToArray();

        foreach (var sqlFile in sqlFiles)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.DatabaseObject,
                    "SqlFile",
                    sqlFile.RelativePath,
                    70,
                    $"SQL source file discovered: " +
                    sqlFile.RelativePath,
                    [
                        "repository",
                        "database",
                        "sql-file"
                    ]));
        }

        var configurationFiles =
            scanResult.Files
                .Where(file =>
                    file.Category.Equals(
                        "Configuration",
                        StringComparison.OrdinalIgnoreCase))
                .Take(100)
                .ToArray();

        foreach (var configurationFile in
                 configurationFiles)
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Configuration,
                    "ConfigurationFile",
                    configurationFile.RelativePath,
                    70,
                    $"Configuration file discovered: " +
                    configurationFile.RelativePath,
                    [
                        "repository",
                        "configuration"
                    ]));
        }
    }

    private KnowledgeContribution CreateContribution(
        string applicationHint,
        KnowledgeContributionType type,
        string key,
        string value,
        int confidenceScore,
        string evidence,
        IReadOnlyCollection<string> tags)
    {
        return new KnowledgeContribution
        {
            ApplicationId =
                applicationHint,

            ApplicationName =
                applicationHint,

            Type =
                type,

            Key =
                key,

            Value =
                value,

            ConfidenceScore =
                Math.Clamp(
                    confidenceScore,
                    0,
                    100),

            SourceKind =
                SourceKind,

            SourceName =
                SourceName,

            Evidence =
                evidence,

            Tags =
                tags
        };
    }

    private static IReadOnlyCollection<string>
        ExtractRepositoryPaths(
            IReadOnlyDictionary<string, string> metadata)
    {
        var results =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in metadata)
        {
            if (!item.Key.StartsWith(
                    RepositorySourcePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!item.Key.EndsWith(
                    ":Location",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var prefix =
                item.Key[
                    ..^":Location".Length];

            if (!metadata.TryGetValue(
                    $"{prefix}:Type",
                    out var sourceType))
            {
                continue;
            }

            if (!sourceType.Equals(
                    "Repository",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(
                    item.Value))
            {
                results.Add(
                    item.Value);
            }
        }

        return results.ToArray();
    }

    private static string GetRepositoryName(
        string rootPath)
    {
        var trimmed =
            rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var name =
            Path.GetFileName(
                trimmed);

        return string.IsNullOrWhiteSpace(name)
            ? rootPath
            : name;
    }

    private static string NormalizeProjectReference(
        string projectReference)
    {
        var fileName =
            Path.GetFileNameWithoutExtension(
                projectReference);

        return string.IsNullOrWhiteSpace(
                fileName)
            ? projectReference
            : fileName;
    }

    private static IReadOnlyCollection<KnowledgeContribution>
        Deduplicate(
            IEnumerable<KnowledgeContribution> contributions)
    {
        return contributions
            .Where(contribution =>
                !string.IsNullOrWhiteSpace(
                    contribution.Value))
            .GroupBy(
                contribution =>
                    $"{contribution.ApplicationId}|" +
                    $"{contribution.Type}|" +
                    $"{contribution.Key}|" +
                    $"{contribution.Value}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group
                    .OrderByDescending(item =>
                        item.ConfidenceScore)
                    .First())
            .ToArray();
    }
}