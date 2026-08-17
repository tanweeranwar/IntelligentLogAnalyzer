using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace LogAnalyzer.ApplicationIntelligence.Loading;

public sealed class JsonApplicationPackageLoader
    : IApplicationPackageLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
    new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly IHostEnvironment _hostEnvironment;
    private readonly KnowledgePackageOptions _options;
    private readonly IApplicationPackageValidator _validator;

    public JsonApplicationPackageLoader(
        IHostEnvironment hostEnvironment,
        IOptions<KnowledgePackageOptions> options,
        IApplicationPackageValidator validator)
    {
        _hostEnvironment =
            hostEnvironment ??
            throw new ArgumentNullException(
                nameof(hostEnvironment));

        _options =
            options?.Value ??
            throw new ArgumentNullException(
                nameof(options));

        _validator =
            validator ??
            throw new ArgumentNullException(
                nameof(validator));
    }

    public async Task<
        IReadOnlyCollection<ApplicationKnowledgePackage>>
        LoadAllAsync(
            CancellationToken cancellationToken = default)
    {
        var rootPath =
            ResolveRootPath();

        if (!Directory.Exists(rootPath))
        {
            if (_options.RequireAtLeastOnePackage)
            {
                throw new DirectoryNotFoundException(
                    $"Application knowledge root directory " +
                    $"'{rootPath}' does not exist.");
            }

            return Array.Empty<ApplicationKnowledgePackage>();
        }

        var packageDirectories =
            Directory
                .EnumerateDirectories(
                    rootPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var packages =
            new List<ApplicationKnowledgePackage>();

        foreach (var packageDirectory in packageDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var package =
                    await LoadAsync(
                        packageDirectory,
                        cancellationToken);

                packages.Add(package);
            }
            catch when (!_options.FailOnInvalidPackage)
            {
                // Invalid packages are ignored only when explicitly
                // configured. Logging will be added in the next increment.
            }
        }

        if (_options.RequireAtLeastOnePackage &&
            packages.Count == 0)
        {
            throw new InvalidOperationException(
                $"No valid application knowledge packages " +
                $"were found under '{rootPath}'.");
        }

        return packages;
    }

    public async Task<ApplicationKnowledgePackage> LoadAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            packageDirectory);

        var resolvedDirectory =
            Path.GetFullPath(packageDirectory);

        if (!Directory.Exists(resolvedDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Application package directory " +
                $"'{resolvedDirectory}' does not exist.");
        }

        var sourceFiles =
            Directory
                .EnumerateFiles(
                    resolvedDirectory,
                    "*.json",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var metadata =
            await ReadRequiredAsync<ApplicationMetadata>(
                resolvedDirectory,
                "application.json",
                cancellationToken);

        var application =
            new ApplicationKnowledge
            {
                Metadata = metadata,

                Repositories =
                    await ReadOptionalCollectionAsync<
                        ApplicationRepositoryKnowledge>(
                            resolvedDirectory,
                            "repositories.json",
                            cancellationToken),

                Components =
                    await ReadOptionalCollectionAsync<
                        ArchitectureComponentKnowledge>(
                            resolvedDirectory,
                            "architecture.json",
                            cancellationToken),

                Workflows =
                    await ReadOptionalCollectionAsync<
                        WorkflowKnowledge>(
                            resolvedDirectory,
                            "workflows.json",
                            cancellationToken),

                Dependencies =
                    await ReadOptionalCollectionAsync<
                        DependencyKnowledge>(
                            resolvedDirectory,
                            "dependencies.json",
                            cancellationToken),

                ApiEndpoints =
                    await ReadOptionalCollectionAsync<
                        ApiEndpointKnowledge>(
                            resolvedDirectory,
                            "apis.json",
                            cancellationToken),

                DatabaseObjects =
                    await ReadOptionalCollectionAsync<
                        DatabaseObjectKnowledge>(
                            resolvedDirectory,
                            "database.json",
                            cancellationToken),

                Runbooks =
                    await ReadOptionalCollectionAsync<
                        RunbookKnowledge>(
                            resolvedDirectory,
                            "runbooks.json",
                            cancellationToken),

                KnownIssues =
                    await ReadOptionalCollectionAsync<
                        KnownIssueKnowledge>(
                            resolvedDirectory,
                            "knownissues.json",
                            cancellationToken),

                Configurations =
                    await ReadOptionalCollectionAsync<
                        ConfigurationKnowledge>(
                            resolvedDirectory,
                            "configuration.json",
                            cancellationToken),

                Fingerprints =
                    await ReadOptionalCollectionAsync<
                        ApplicationFingerprint>(
                            resolvedDirectory,
                            "fingerprints.json",
                            cancellationToken)
            };

        var package =
            new ApplicationKnowledgePackage
            {
                PackageDirectory =
                    resolvedDirectory,

                Application =
                    application,

                LoadedAtUtc =
                    DateTimeOffset.UtcNow,

                SourceFiles =
                    sourceFiles
                        .Select(Path.GetFileName)
                        .Where(fileName =>
                            !string.IsNullOrWhiteSpace(fileName))
                        .Select(fileName => fileName!)
                        .ToArray(),

                ContentHash =
                    await CalculateContentHashAsync(
                        sourceFiles,
                        cancellationToken)
            };

        _validator.Validate(package);

        return package;
    }

    private string ResolveRootPath()
    {
        if (Path.IsPathRooted(_options.RootPath))
        {
            return Path.GetFullPath(
                _options.RootPath);
        }

        return Path.GetFullPath(
            Path.Combine(
                _hostEnvironment.ContentRootPath,
                _options.RootPath));
    }

    private static async Task<T> ReadRequiredAsync<T>(
        string directory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var path =
            Path.Combine(
                directory,
                fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Required application knowledge file " +
                $"'{fileName}' was not found in '{directory}'.",
                path);
        }

        return await DeserializeAsync<T>(
            path,
            cancellationToken);
    }

    private static async Task<IReadOnlyCollection<T>>
        ReadOptionalCollectionAsync<T>(
            string directory,
            string fileName,
            CancellationToken cancellationToken)
    {
        var path =
            Path.Combine(
                directory,
                fileName);

        if (!File.Exists(path))
        {
            return Array.Empty<T>();
        }

        var values =
            await DeserializeAsync<List<T>>(
                path,
                cancellationToken);

        return values;
    }

    private static async Task<T> DeserializeAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream =
            File.OpenRead(path);

        var value =
            await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonOptions,
                cancellationToken);

        return value ??
               throw new JsonException(
                   $"The file '{path}' did not contain a valid " +
                   $"{typeof(T).Name} value.");
    }

    private static async Task<string>
        CalculateContentHashAsync(
            IReadOnlyCollection<string> sourceFiles,
            CancellationToken cancellationToken)
    {
        using var incrementalHash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        foreach (var path in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileNameBytes =
                Encoding.UTF8.GetBytes(
                    Path.GetFileName(path));

            incrementalHash.AppendData(
                fileNameBytes);

            await using var stream =
                File.OpenRead(path);

            var buffer =
                new byte[81920];

            int bytesRead;

            while ((bytesRead =
                       await stream.ReadAsync(
                           buffer,
                           cancellationToken)) > 0)
            {
                incrementalHash.AppendData(
                    buffer,
                    0,
                    bytesRead);
            }
        }

        return Convert.ToHexString(
            incrementalHash.GetHashAndReset());
    }
}