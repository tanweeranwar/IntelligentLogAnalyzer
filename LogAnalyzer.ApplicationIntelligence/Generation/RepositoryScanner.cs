using System.Xml.Linq;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models.Generation;

namespace LogAnalyzer.ApplicationIntelligence.Generation;

public sealed class RepositoryScanner : IRepositoryScanner
{
    private static readonly HashSet<string>
        IgnoredDirectories =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".vs",
            ".idea",
            "bin",
            "obj",
            "node_modules",
            "packages",
            "TestResults"
        };

    private static readonly HashSet<string>
        SupportedExtensions =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".csproj",
            ".sln",
            ".sql",
            ".json",
            ".config",
            ".xml",
            ".yml",
            ".yaml",
            ".razor",
            ".cshtml",
            ".js",
            ".ts",
            ".tsx",
            ".jsx",
            ".html",
            ".css",
            ".ps1",
            ".cmd",
            ".bat",
            ".md"
        };

    public Task<RepositoryScanResult> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootPath);

        var resolvedRoot =
            Path.GetFullPath(rootPath);

        if (!Directory.Exists(resolvedRoot))
        {
            throw new DirectoryNotFoundException(
                $"Repository directory '{resolvedRoot}' does not exist.");
        }

        return Task.Run(
            () => ScanRepository(
                resolvedRoot,
                cancellationToken),
            cancellationToken);
    }

    private static RepositoryScanResult ScanRepository(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var warnings =
            new List<string>();

        var files =
            EnumerateRepositoryFiles(
                rootPath,
                warnings,
                cancellationToken)
            .ToArray();

        var projectFiles =
            files
                .Where(file =>
                    file.Extension.Equals(
                        ".csproj",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var projects =
            new List<RepositoryProject>();

        foreach (var projectFile in projectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                projects.Add(
                    ReadProject(
                        rootPath,
                        projectFile));
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Unable to inspect project " +
                    $"'{projectFile.RelativePath}': " +
                    ex.Message);
            }
        }

        var enrichedFiles =
            files
                .Select(file =>
                    AttachProjectName(
                        file,
                        projects))
                .ToArray();

        var technologies =
            DetectRepositoryTechnologies(
                enrichedFiles,
                projects);

        var solutions =
            enrichedFiles
                .Where(file =>
                    file.Extension.Equals(
                        ".sln",
                        StringComparison.OrdinalIgnoreCase))
                .Select(file =>
                    file.RelativePath)
                .OrderBy(
                    value => value,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var filesByCategory =
            enrichedFiles
                .GroupBy(
                    file => file.Category,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.OrdinalIgnoreCase);

        return new RepositoryScanResult
        {
            RootPath =
                rootPath,

            ScannedAtUtc =
                DateTimeOffset.UtcNow,

            TotalFiles =
                enrichedFiles.Length,

            TotalSizeBytes =
                enrichedFiles.Sum(
                    file => file.SizeBytes),

            Projects =
                projects
                    .OrderBy(
                        project => project.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            Files =
                enrichedFiles,

            SolutionFiles =
                solutions,

            Technologies =
                technologies,

            FilesByCategory =
                filesByCategory,

            Warnings =
                warnings
        };
    }

    private static IEnumerable<RepositoryFile>
        EnumerateRepositoryFiles(
            string rootPath,
            ICollection<string> warnings,
            CancellationToken cancellationToken)
    {
        var directories =
            new Stack<string>();

        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDirectory =
                directories.Pop();

            IEnumerable<string> childDirectories;

            try
            {
                childDirectories =
                    Directory.EnumerateDirectories(
                        currentDirectory);
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Unable to inspect directory " +
                    $"'{currentDirectory}': {ex.Message}");

                continue;
            }

            foreach (var directory in childDirectories)
            {
                var directoryName =
                    Path.GetFileName(directory);

                if (IgnoredDirectories.Contains(
                        directoryName))
                {
                    continue;
                }

                directories.Push(directory);
            }

            IEnumerable<string> files;

            try
            {
                files =
                    Directory.EnumerateFiles(
                        currentDirectory);
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Unable to enumerate files in " +
                    $"'{currentDirectory}': {ex.Message}");

                continue;
            }

            foreach (var path in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension =
                    Path.GetExtension(path);

                if (!SupportedExtensions.Contains(
                        extension))
                {
                    continue;
                }

                FileInfo fileInfo;

                try
                {
                    fileInfo =
                        new FileInfo(path);
                }
                catch (Exception ex)
                {
                    warnings.Add(
                        $"Unable to inspect file " +
                        $"'{path}': {ex.Message}");

                    continue;
                }

                yield return new RepositoryFile
                {
                    RelativePath =
                        Path.GetRelativePath(
                            rootPath,
                            path),

                    FileName =
                        fileInfo.Name,

                    Extension =
                        extension,

                    Category =
                        GetFileCategory(extension),

                    SizeBytes =
                        fileInfo.Length,

                    LastModifiedUtc =
                        fileInfo.LastWriteTimeUtc
                };
            }
        }
    }

    private static RepositoryProject ReadProject(
        string rootPath,
        RepositoryFile projectFile)
    {
        var absolutePath =
            Path.Combine(
                rootPath,
                projectFile.RelativePath);

        var document =
            XDocument.Load(
                absolutePath);

        var root =
            document.Root ??
            throw new InvalidOperationException(
                "Project XML does not contain a root element.");

        var targetFramework =
            GetFirstProperty(
                root,
                "TargetFramework");

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            targetFramework =
                GetFirstProperty(
                    root,
                    "TargetFrameworks");
        }

        var projectReferences =
            root
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals(
                        "ProjectReference",
                        StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                    element.Attribute("Include")?.Value)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var packageReferences =
            root
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals(
                        "PackageReference",
                        StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                    element.Attribute("Include")?.Value)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var technologies =
            DetectProjectTechnologies(
                root,
                targetFramework,
                packageReferences);

        return new RepositoryProject
        {
            Name =
                Path.GetFileNameWithoutExtension(
                    projectFile.FileName),

            RelativePath =
                projectFile.RelativePath,

            ProjectType =
                DetermineProjectType(root),

            TargetFramework =
                targetFramework,

            ProjectReferences =
                projectReferences,

            PackageReferences =
                packageReferences,

            Technologies =
                technologies
        };
    }

    private static RepositoryFile AttachProjectName(
        RepositoryFile file,
        IReadOnlyCollection<RepositoryProject> projects)
    {
        var matchingProject =
            projects
                .Select(project =>
                    new
                    {
                        Project = project,
                        Directory =
                            Path.GetDirectoryName(
                                project.RelativePath)
                            ?? string.Empty
                    })
                .Where(item =>
                    string.IsNullOrWhiteSpace(
                        item.Directory) ||
                    file.RelativePath.StartsWith(
                        item.Directory +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) ||
                    file.RelativePath.Equals(
                        item.Project.RelativePath,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item =>
                    item.Directory.Length)
                .FirstOrDefault();

        if (matchingProject is null)
        {
            return file;
        }

        return new RepositoryFile
        {
            RelativePath =
                file.RelativePath,

            FileName =
                file.FileName,

            Extension =
                file.Extension,

            Category =
                file.Category,

            SizeBytes =
                file.SizeBytes,

            LastModifiedUtc =
                file.LastModifiedUtc,

            ProjectName =
                matchingProject.Project.Name
        };
    }

    private static IReadOnlyCollection<string>
        DetectRepositoryTechnologies(
            IReadOnlyCollection<RepositoryFile> files,
            IReadOnlyCollection<RepositoryProject> projects)
    {
        var technologies =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            technologies.UnionWith(
                project.Technologies);
        }

        if (files.Any(file =>
                file.Extension.Equals(
                    ".sql",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "SQL");
        }

        if (files.Any(file =>
                file.Extension.Equals(
                    ".razor",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "Blazor");
        }

        if (files.Any(file =>
                file.Extension.Equals(
                    ".ts",
                    StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(
                    ".tsx",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "TypeScript");
        }

        if (files.Any(file =>
                file.FileName.Equals(
                    "package.json",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "Node.js");
        }

        return technologies
            .OrderBy(
                technology => technology,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string>
        DetectProjectTechnologies(
            XElement projectRoot,
            string targetFramework,
            IReadOnlyCollection<string> packages)
    {
        var technologies =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            technologies.Add(
                targetFramework);
        }

        var sdk =
            projectRoot.Attribute("Sdk")?.Value;

        if (!string.IsNullOrWhiteSpace(sdk))
        {
            if (sdk.Contains(
                    "Web",
                    StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add(
                    "ASP.NET Core");
            }
            else
            {
                technologies.Add(
                    ".NET");
            }
        }

        if (packages.Any(package =>
                package.Contains(
                    "EntityFrameworkCore",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "Entity Framework Core");
        }

        if (packages.Any(package =>
                package.Contains(
                    "EntityFramework",
                    StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add(
                "Entity Framework");
        }

        return technologies
            .OrderBy(
                technology => technology,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string DetermineProjectType(
        XElement projectRoot)
    {
        var sdk =
            projectRoot.Attribute("Sdk")?.Value
            ?? string.Empty;

        if (sdk.Contains(
                "Web",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Web";
        }

        var outputType =
            GetFirstProperty(
                projectRoot,
                "OutputType");

        if (outputType.Equals(
                "Exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Executable";
        }

        return "Library";
    }

    private static string GetFirstProperty(
        XElement root,
        string propertyName)
    {
        return root
                   .Descendants()
                   .FirstOrDefault(element =>
                       element.Name.LocalName.Equals(
                           propertyName,
                           StringComparison.OrdinalIgnoreCase))
                   ?.Value
                   .Trim()
               ?? string.Empty;
    }

    private static string GetFileCategory(
        string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" =>
                "CSharp",

            ".csproj" or ".sln" =>
                "Project",

            ".sql" =>
                "Database",

            ".json" or ".config" or
            ".xml" or ".yml" or ".yaml" =>
                "Configuration",

            ".razor" or ".cshtml" or
            ".html" =>
                "Presentation",

            ".js" or ".jsx" or
            ".ts" or ".tsx" =>
                "Frontend",

            ".css" =>
                "Styles",

            ".ps1" or ".cmd" or ".bat" =>
                "Automation",

            ".md" =>
                "Documentation",

            _ =>
                "Other"
        };
    }
}