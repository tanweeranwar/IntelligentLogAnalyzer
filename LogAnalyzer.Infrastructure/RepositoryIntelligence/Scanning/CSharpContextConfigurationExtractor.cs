using System.Text.RegularExpressions;

namespace LogAnalyzer.Infrastructure.RepositoryIntelligence.Scanning;

public sealed partial class CSharpContextConfigurationExtractor
{
    public SourceContextConfigurationResult Extract(
        string methodBody)
    {
        if (string.IsNullOrWhiteSpace(methodBody))
        {
            return new SourceContextConfigurationResult();
        }

        var dbContexts =
            ExtractDbContexts(
                methodBody);

        var configurationKeys =
            ExtractConfigurationKeys(
                methodBody);

        return new SourceContextConfigurationResult
        {
            DbContexts =
                dbContexts,

            ConfigurationKeys =
                configurationKeys
        };
    }

    private static IReadOnlyCollection<string>
        ExtractDbContexts(
            string content)
    {
        var contexts =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (Match match in
                 DbContextTypeRegex().Matches(content))
        {
            var value =
                match.Groups["name"]
                    .Value
                    .Trim();

            if (!string.IsNullOrWhiteSpace(value))
            {
                contexts.Add(
                    value);
            }
        }

        foreach (Match match in
                 GenericDbContextRegex().Matches(content))
        {
            var value =
                match.Groups["name"]
                    .Value
                    .Trim();

            if (!string.IsNullOrWhiteSpace(value))
            {
                contexts.Add(
                    value);
            }
        }

        return contexts.ToArray();
    }

    private static IReadOnlyCollection<string>
        ExtractConfigurationKeys(
            string content)
    {
        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (Match match in
                 ConfigurationIndexerRegex().Matches(content))
        {
            AddConfigurationKey(
                keys,
                match.Groups["key"].Value);
        }

        foreach (Match match in
                 GetValueRegex().Matches(content))
        {
            AddConfigurationKey(
                keys,
                match.Groups["key"].Value);
        }

        foreach (Match match in
                 GetSectionRegex().Matches(content))
        {
            AddConfigurationKey(
                keys,
                match.Groups["key"].Value);
        }

        foreach (Match match in
                 GetConnectionStringRegex().Matches(content))
        {
            var name =
                match.Groups["key"]
                    .Value
                    .Trim();

            if (!string.IsNullOrWhiteSpace(name))
            {
                keys.Add(
                    $"ConnectionStrings:{name}");
            }
        }

        return keys.ToArray();
    }

    private static void AddConfigurationKey(
        ICollection<string> keys,
        string value)
    {
        var normalized =
            value.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        keys.Add(
            normalized);
    }

    [GeneratedRegex(
        @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*DbContext)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DbContextTypeRegex();

    [GeneratedRegex(
        @"(?:GetRequiredService|GetService)\s*<\s*(?<name>[A-Za-z_][A-Za-z0-9_]*DbContext)\s*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex GenericDbContextRegex();

    [GeneratedRegex(
        @"(?:configuration|_configuration|config|_config)\s*\[\s*""(?<key>[^""]+)""\s*\]",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConfigurationIndexerRegex();

    [GeneratedRegex(
        @"\.GetValue(?:<[^>]+>)?\s*\(\s*""(?<key>[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex GetValueRegex();

    [GeneratedRegex(
        @"\.GetSection\s*\(\s*""(?<key>[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex GetSectionRegex();

    [GeneratedRegex(
        @"\.GetConnectionString\s*\(\s*""(?<key>[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex GetConnectionStringRegex();
}

public sealed class SourceContextConfigurationResult
{
    public IReadOnlyCollection<string> DbContexts
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> ConfigurationKeys
    {
        get;
        init;
    } = [];
}