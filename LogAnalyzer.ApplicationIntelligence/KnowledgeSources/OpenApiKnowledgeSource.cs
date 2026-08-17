using System.Diagnostics;
using System.Text.Json;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models.Discovery;

namespace LogAnalyzer.ApplicationIntelligence.KnowledgeSources;

public sealed class OpenApiKnowledgeSource
    : IOpenApiKnowledgeSource
{
    private const string SourcePrefix =
        "Source:";

    private static readonly HashSet<string>
        SupportedHttpMethods =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            "get",
            "post",
            "put",
            "delete",
            "patch",
            "head",
            "options",
            "trace"
        };

    public string SourceName =>
        "OpenAPI Intelligence";

    public KnowledgeSourceKind SourceKind =>
        KnowledgeSourceKind.OpenApi;

    public async Task<KnowledgeSourceResult> DiscoverAsync(
        ApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var documentPaths =
            ExtractOpenApiPaths(
                request.Metadata);

        if (documentPaths.Count == 0)
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

        foreach (var documentPath in documentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result =
                    await DiscoverOpenApiAsync(
                        documentPath,
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
                    $"OpenAPI document '{documentPath}' " +
                    $"could not be analyzed: {ex.Message}");
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
        DiscoverOpenApiAsync(
            string documentPath,
            string applicationHint = "",
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            documentPath);

        var stopwatch =
            Stopwatch.StartNew();

        var resolvedPath =
            Path.GetFullPath(
                documentPath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"OpenAPI document '{resolvedPath}' " +
                "does not exist.",
                resolvedPath);
        }

        var extension =
            Path.GetExtension(
                resolvedPath);

        if (extension.Equals(
                ".yaml",
                StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".yml",
                StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();

            return new KnowledgeSourceResult
            {
                SourceName =
                    SourceName,

                SourceKind =
                    SourceKind,

                Warnings =
                [
                    $"OpenAPI YAML document '{resolvedPath}' " +
                    "was detected. JSON OpenAPI documents are " +
                    "supported in the current implementation."
                ],

                Duration =
                    stopwatch.Elapsed
            };
        }

        await using var stream =
            File.OpenRead(
                resolvedPath);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken:
                    cancellationToken);

        var contributions =
            BuildContributions(
                document.RootElement,
                resolvedPath,
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

            Duration =
                stopwatch.Elapsed
        };
    }

    private IReadOnlyCollection<KnowledgeContribution>
        BuildContributions(
            JsonElement root,
            string documentPath,
            string applicationHint)
    {
        var contributions =
            new List<KnowledgeContribution>();

        AddDocumentMetadata(
            contributions,
            root,
            documentPath,
            applicationHint);

        AddServers(
            contributions,
            root,
            documentPath,
            applicationHint);

        AddPaths(
            contributions,
            root,
            documentPath,
            applicationHint);

        AddSchemas(
            contributions,
            root,
            documentPath,
            applicationHint);

        contributions.Add(
            CreateContribution(
                applicationHint,
                KnowledgeContributionType.Technology,
                "Specification",
                "OpenAPI",
                100,
                $"OpenAPI specification discovered in " +
                $"'{documentPath}'.",
                [
                    "openapi",
                    "api",
                    "specification"
                ]));

        return Deduplicate(
            contributions);
    }

    private void AddDocumentMetadata(
        ICollection<KnowledgeContribution> contributions,
        JsonElement root,
        string documentPath,
        string applicationHint)
    {
        if (!root.TryGetProperty(
                "info",
                out var info) ||
            info.ValueKind !=
                JsonValueKind.Object)
        {
            return;
        }

        var title =
            GetStringProperty(
                info,
                "title");

        if (!string.IsNullOrWhiteSpace(
                title))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Other,
                    "OpenApiTitle",
                    title,
                    100,
                    $"OpenAPI title '{title}' declared in " +
                    $"'{documentPath}'.",
                    [
                        "openapi",
                        "metadata",
                        "title"
                    ]));
        }

        var version =
            GetStringProperty(
                info,
                "version");

        if (!string.IsNullOrWhiteSpace(
                version))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Version,
                    "ApiVersion",
                    version,
                    100,
                    $"OpenAPI version '{version}' declared in " +
                    $"'{documentPath}'.",
                    [
                        "openapi",
                        "version"
                    ]));
        }

        var description =
            GetStringProperty(
                info,
                "description");

        if (!string.IsNullOrWhiteSpace(
                description))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Other,
                    "ApiDescription",
                    TrimLength(
                        description,
                        1000),
                    100,
                    $"API description declared in " +
                    $"'{documentPath}'.",
                    [
                        "openapi",
                        "description"
                    ]));
        }
    }

    private void AddServers(
        ICollection<KnowledgeContribution> contributions,
        JsonElement root,
        string documentPath,
        string applicationHint)
    {
        if (!root.TryGetProperty(
                "servers",
                out var servers) ||
            servers.ValueKind !=
                JsonValueKind.Array)
        {
            return;
        }

        foreach (var server in
                 servers.EnumerateArray())
        {
            if (server.ValueKind !=
                JsonValueKind.Object)
            {
                continue;
            }

            var url =
                GetStringProperty(
                    server,
                    "url");

            if (string.IsNullOrWhiteSpace(
                    url))
            {
                continue;
            }

            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Dependency,
                    "ApiServer",
                    url,
                    95,
                    $"OpenAPI server URL '{url}' declared in " +
                    $"'{documentPath}'.",
                    [
                        "openapi",
                        "server",
                        "url"
                    ]));
        }
    }

    private void AddPaths(
        ICollection<KnowledgeContribution> contributions,
        JsonElement root,
        string documentPath,
        string applicationHint)
    {
        if (!root.TryGetProperty(
                "paths",
                out var paths) ||
            paths.ValueKind !=
                JsonValueKind.Object)
        {
            return;
        }

        foreach (var pathProperty in
                 paths.EnumerateObject())
        {
            var route =
                pathProperty.Name;

            if (string.IsNullOrWhiteSpace(
                    route))
            {
                continue;
            }

            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.ApiEndpoint,
                    "Route",
                    route,
                    100,
                    $"API route '{route}' discovered in " +
                    $"'{documentPath}'.",
                    [
                        "openapi",
                        "route",
                        "api"
                    ]));

            if (pathProperty.Value.ValueKind !=
                JsonValueKind.Object)
            {
                continue;
            }

            foreach (var operationProperty in
                     pathProperty.Value
                         .EnumerateObject())
            {
                if (!SupportedHttpMethods.Contains(
                        operationProperty.Name))
                {
                    continue;
                }

                AddOperation(
                    contributions,
                    route,
                    operationProperty.Name,
                    operationProperty.Value,
                    documentPath,
                    applicationHint);
            }
        }
    }

    private void AddOperation(
        ICollection<KnowledgeContribution> contributions,
        string route,
        string httpMethod,
        JsonElement operation,
        string documentPath,
        string applicationHint)
    {
        var normalizedMethod =
            httpMethod.ToUpperInvariant();

        var operationKey =
            $"{normalizedMethod} {route}";

        contributions.Add(
            CreateContribution(
                applicationHint,
                KnowledgeContributionType.ApiEndpoint,
                "Operation",
                operationKey,
                100,
                $"{normalizedMethod} operation discovered " +
                $"for route '{route}' in '{documentPath}'.",
                [
                    "openapi",
                    "operation",
                    normalizedMethod
                ]));

        if (operation.ValueKind !=
            JsonValueKind.Object)
        {
            return;
        }

        var operationId =
            GetStringProperty(
                operation,
                "operationId");

        if (!string.IsNullOrWhiteSpace(
                operationId))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Component,
                    "OperationId",
                    operationId,
                    95,
                    $"Operation '{operationKey}' maps to " +
                    $"operationId '{operationId}'.",
                    [
                        "openapi",
                        "operation-id"
                    ]));
        }

        AddOperationTags(
            contributions,
            operation,
            operationKey,
            applicationHint);

        var summary =
            GetStringProperty(
                operation,
                "summary");

        if (!string.IsNullOrWhiteSpace(
                summary))
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Other,
                    $"Summary:{operationKey}",
                    summary,
                    100,
                    $"OpenAPI summary for " +
                    $"'{operationKey}'.",
                    [
                        "openapi",
                        "summary"
                    ]));
        }
    }

    private void AddOperationTags(
        ICollection<KnowledgeContribution> contributions,
        JsonElement operation,
        string operationKey,
        string applicationHint)
    {
        if (!operation.TryGetProperty(
                "tags",
                out var tags) ||
            tags.ValueKind !=
                JsonValueKind.Array)
        {
            return;
        }

        foreach (var tagElement in
                 tags.EnumerateArray())
        {
            if (tagElement.ValueKind !=
                JsonValueKind.String)
            {
                continue;
            }

            var tag =
                tagElement.GetString();

            if (string.IsNullOrWhiteSpace(
                    tag))
            {
                continue;
            }

            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Component,
                    $"ApiTag:{operationKey}",
                    tag,
                    85,
                    $"Operation '{operationKey}' is grouped " +
                    $"under OpenAPI tag '{tag}'.",
                    [
                        "openapi",
                        "tag"
                    ]));
        }
    }

    private void AddSchemas(
        ICollection<KnowledgeContribution> contributions,
        JsonElement root,
        string documentPath,
        string applicationHint)
    {
        if (!root.TryGetProperty(
                "components",
                out var components) ||
            components.ValueKind !=
                JsonValueKind.Object)
        {
            return;
        }

        if (!components.TryGetProperty(
                "schemas",
                out var schemas) ||
            schemas.ValueKind !=
                JsonValueKind.Object)
        {
            return;
        }

        foreach (var schema in
                 schemas.EnumerateObject())
        {
            contributions.Add(
                CreateContribution(
                    applicationHint,
                    KnowledgeContributionType.Other,
                    "ApiSchema",
                    schema.Name,
                    90,
                    $"OpenAPI schema '{schema.Name}' " +
                    $"discovered in '{documentPath}'.",
                    [
                        "openapi",
                        "schema"
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
        ExtractOpenApiPaths(
            IReadOnlyDictionary<string, string> metadata)
    {
        var results =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in metadata)
        {
            if (!item.Key.StartsWith(
                    SourcePrefix,
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
                    "OpenApi",
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

    private static string GetStringProperty(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return string.Empty;
        }

        return property.ValueKind ==
               JsonValueKind.String
            ? property.GetString()
              ?? string.Empty
            : string.Empty;
    }

    private static string TrimLength(
        string value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        if (value.Length <= maximumLength)
        {
            return value;
        }

        return value[..maximumLength];
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