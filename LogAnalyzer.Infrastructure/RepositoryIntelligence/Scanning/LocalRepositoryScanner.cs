using System.Xml.Linq;
using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.RepositoryIntelligence;

namespace LogAnalyzer.Infrastructure.RepositoryIntelligence.Scanning;

public sealed class LocalRepositoryScanner
    : IRepositoryScanner
{
    private readonly CSharpSourceAnalyzer _sourceAnalyzer;

    public LocalRepositoryScanner(
        CSharpSourceAnalyzer sourceAnalyzer)
    {
        _sourceAnalyzer =
            sourceAnalyzer;
    }

    public async Task<RepositoryKnowledge> ScanAsync(
        RepositoryScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Location))
        {
            throw new ArgumentException(
                "Repository location is required.",
                nameof(request));
        }

        var rootPath =
            Path.GetFullPath(
                request.Location);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(
                $"Repository path '{rootPath}' does not exist.");
        }

        var repository =
            new RepositoryDescriptor
            {
                Id =
                    CreateRepositoryId(
                        request,
                        rootPath),

                Name =
                    ResolveRepositoryName(
                        request,
                        rootPath),

                Provider =
                    string.IsNullOrWhiteSpace(
                        request.Provider)
                        ? "Local"
                        : request.Provider.Trim(),

                Location =
                    rootPath,

                DefaultBranch =
                    request.Branch,

                CommitId =
                    string.Empty,

                ScannedAt =
                    DateTimeOffset.UtcNow
            };

        var projects =
            await DiscoverProjectsAsync(
                rootPath,
                request,
                cancellationToken);

        var files =
            await DiscoverSourceFilesAsync(
                rootPath,
                projects,
                request,
                cancellationToken);

        var databaseReferences =
            BuildDatabaseReferences(
                files);

        var configurationReferences =
            BuildConfigurationReferences(
                files);

        return new RepositoryKnowledge
        {
            Repository =
                repository,

            Projects =
                projects,

            Files =
                files,

            ApiEndpoints =
                [],

            DatabaseReferences =
                databaseReferences,

            ConfigurationReferences =
                configurationReferences
        };
    }

    private static async Task<IReadOnlyCollection<RepositoryProject>>
        DiscoverProjectsAsync(
            string rootPath,
            RepositoryScanRequest request,
            CancellationToken cancellationToken)
    {
        var maximumFiles =
            Math.Max(
                request.MaximumFiles,
                0);

        if (maximumFiles == 0)
        {
            return [];
        }

        var projectFiles =
            Directory
                .EnumerateFiles(
                    rootPath,
                    "*.csproj",
                    SearchOption.AllDirectories)
                .Where(path =>
                    ShouldIncludePath(
                        path,
                        request))
                .Take(
                    maximumFiles)
                .ToArray();

        var projects =
            new List<RepositoryProject>();

        foreach (var projectFile in projectFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var project =
                await ReadProjectAsync(
                    rootPath,
                    projectFile,
                    cancellationToken);

            projects.Add(
                project);
        }

        return projects
            .OrderBy(
                project =>
                    project.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<RepositoryProject>
        ReadProjectAsync(
            string rootPath,
            string projectFile,
            CancellationToken cancellationToken)
    {
        await using var stream =
            File.OpenRead(
                projectFile);

        var document =
            await XDocument.LoadAsync(
                stream,
                LoadOptions.None,
                cancellationToken);

        var targetFramework =
            GetPropertyValue(
                document,
                "TargetFramework");

        if (string.IsNullOrWhiteSpace(
                targetFramework))
        {
            targetFramework =
                GetPropertyValue(
                    document,
                    "TargetFrameworks");
        }

        var projectReferences =
            document
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals(
                        "ProjectReference",
                        StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                    element.Attribute("Include")
                        ?.Value)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(
                        value))
                .Select(value =>
                    value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var packageReferences =
            document
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals(
                        "PackageReference",
                        StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                    element.Attribute("Include")
                        ?.Value)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(
                        value))
                .Select(value =>
                    value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new RepositoryProject
        {
            Name =
                Path.GetFileNameWithoutExtension(
                    projectFile),

            FilePath =
                Path.GetRelativePath(
                    rootPath,
                    projectFile),

            ProjectType =
                DetermineProjectType(
                    document),

            TargetFramework =
                targetFramework,

            ProjectReferences =
                projectReferences,

            PackageReferences =
                packageReferences
        };
    }

    private async Task<IReadOnlyCollection<SourceFileKnowledge>>
        DiscoverSourceFilesAsync(
            string rootPath,
            IReadOnlyCollection<RepositoryProject> projects,
            RepositoryScanRequest request,
            CancellationToken cancellationToken)
    {
        var results =
            new List<SourceFileKnowledge>();

        var remaining =
            Math.Max(
                request.MaximumFiles,
                0);

        if (remaining == 0)
        {
            return results;
        }

        foreach (var project in projects)
        {
            if (remaining <= 0)
            {
                break;
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            var projectFile =
                Path.Combine(
                    rootPath,
                    project.FilePath);

            var projectDirectory =
                Path.GetDirectoryName(
                    projectFile);

            if (string.IsNullOrWhiteSpace(
                    projectDirectory) ||
                !Directory.Exists(
                    projectDirectory))
            {
                continue;
            }

            var sourceFiles =
                Directory
                    .EnumerateFiles(
                        projectDirectory,
                        "*.cs",
                        SearchOption.AllDirectories)
                    .Where(path =>
                        ShouldIncludePath(
                            path,
                            request))
                    .Take(
                        remaining)
                    .ToArray();

            foreach (var sourceFile in sourceFiles)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var knowledge =
                    await _sourceAnalyzer.AnalyzeAsync(
                        rootPath,
                        project.Name,
                        sourceFile,
                        cancellationToken);

                results.Add(
                    knowledge);

                remaining--;

                if (remaining <= 0)
                {
                    break;
                }
            }
        }

        return results;
    }

    private static IReadOnlyCollection<RepositoryDatabaseReference>
        BuildDatabaseReferences(
            IReadOnlyCollection<SourceFileKnowledge> files)
    {
        var references =
            new List<RepositoryDatabaseReference>();

        foreach (var file in files)
        {
            foreach (var type in file.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.DatabaseOperations.Count == 0)
                    {
                        continue;
                    }

                    var dbContexts =
                        method.DbContexts
                            .Where(context =>
                                !string.IsNullOrWhiteSpace(
                                    context))
                            .Select(context =>
                                context.Trim())
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                    foreach (var operation in
                             method.DatabaseOperations)
                    {
                        if (string.IsNullOrWhiteSpace(
                                operation))
                        {
                            continue;
                        }

                        /*
                         * If a DbContext was discovered in the same
                         * method, preserve that relationship.
                         *
                         * If no context was found, still preserve the
                         * database operation as valid repository evidence.
                         */

                        if (dbContexts.Length == 0)
                        {
                            references.Add(
                                CreateDatabaseReference(
                                    file,
                                    type,
                                    method,
                                    operation,
                                    string.Empty));

                            continue;
                        }

                        foreach (var dbContext in dbContexts)
                        {
                            references.Add(
                                CreateDatabaseReference(
                                    file,
                                    type,
                                    method,
                                    operation,
                                    dbContext));
                        }
                    }
                }
            }
        }

        return references
            .GroupBy(
                reference =>
                    $"{reference.Project}|" +
                    $"{reference.FilePath}|" +
                    $"{reference.ClassName}|" +
                    $"{reference.MethodName}|" +
                    $"{reference.Operation}|" +
                    $"{reference.DbContext}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.First())
            .OrderBy(
                reference =>
                    reference.Project,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                reference =>
                    reference.FilePath,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                reference =>
                    reference.LineNumber)
            .ThenBy(
                reference =>
                    reference.Operation,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RepositoryDatabaseReference
        CreateDatabaseReference(
            SourceFileKnowledge file,
            SourceTypeKnowledge type,
            SourceMethodKnowledge method,
            string operation,
            string dbContext)
    {
        return new RepositoryDatabaseReference
        {
            Operation =
                operation.Trim(),

            DatabaseType =
                string.IsNullOrWhiteSpace(
                    dbContext)
                    ? "Unknown"
                    : "EntityFrameworkCore",

            DbContext =
                dbContext,

            Project =
                file.ProjectName,

            FilePath =
                file.FilePath,

            ClassName =
                type.Name,

            MethodName =
                method.Name,

            LineNumber =
                method.LineNumber
        };
    }

    private static IReadOnlyCollection<RepositoryConfigurationReference>
        BuildConfigurationReferences(
            IReadOnlyCollection<SourceFileKnowledge> files)
    {
        var references =
            new List<RepositoryConfigurationReference>();

        foreach (var file in files)
        {
            foreach (var type in file.Types)
            {
                foreach (var method in type.Methods)
                {
                    foreach (var key in
                             method.ConfigurationKeys)
                    {
                        if (string.IsNullOrWhiteSpace(
                                key))
                        {
                            continue;
                        }

                        references.Add(
                            new RepositoryConfigurationReference
                            {
                                Key =
                                    key.Trim(),

                                Project =
                                    file.ProjectName,

                                FilePath =
                                    file.FilePath,

                                ClassName =
                                    type.Name,

                                MethodName =
                                    method.Name
                            });
                    }
                }
            }
        }

        return references
            .GroupBy(
                reference =>
                    $"{reference.Project}|" +
                    $"{reference.FilePath}|" +
                    $"{reference.ClassName}|" +
                    $"{reference.MethodName}|" +
                    $"{reference.Key}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.First())
            .OrderBy(
                reference =>
                    reference.Project,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                reference =>
                    reference.FilePath,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                reference =>
                    reference.Key,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetPropertyValue(
        XDocument document,
        string propertyName)
    {
        return document
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName.Equals(
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim()
            ?? string.Empty;
    }

    private static string DetermineProjectType(
        XDocument document)
    {
        var sdk =
            document
                .Root
                ?.Attribute("Sdk")
                ?.Value
                ?? string.Empty;

        if (sdk.Contains(
                "Microsoft.NET.Sdk.Web",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Web";
        }

        var outputType =
            GetPropertyValue(
                document,
                "OutputType");

        if (outputType.Equals(
                "Exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Executable";
        }

        return "Library";
    }

    private static bool ShouldIncludePath(
        string path,
        RepositoryScanRequest request)
    {
        var normalized =
            path.Replace(
                '\\',
                '/');

        if (normalized.Contains(
                "/bin/",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "/obj/",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "/.git/",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!request.IncludeGeneratedFiles &&
            (
                normalized.Contains(
                    "/generated/",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(
                    ".g.cs",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(
                    ".generated.cs",
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            return false;
        }

        if (!request.IncludeTests &&
            (
                normalized.Contains(
                    "/test/",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(
                    "/tests/",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(
                    ".Tests/",
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            return false;
        }

        return true;
    }

    private static string ResolveRepositoryName(
        RepositoryScanRequest request,
        string rootPath)
    {
        if (!string.IsNullOrWhiteSpace(
                request.RepositoryName))
        {
            return request.RepositoryName.Trim();
        }

        return new DirectoryInfo(
            rootPath)
            .Name;
    }

    private static string CreateRepositoryId(
        RepositoryScanRequest request,
        string rootPath)
    {
        var name =
            ResolveRepositoryName(
                request,
                rootPath);

        var provider =
            string.IsNullOrWhiteSpace(
                request.Provider)
                ? "Local"
                : request.Provider.Trim();

        return
            $"{provider}:{name}"
                .ToLowerInvariant()
                .Replace(
                    ' ',
                    '-');
    }
}