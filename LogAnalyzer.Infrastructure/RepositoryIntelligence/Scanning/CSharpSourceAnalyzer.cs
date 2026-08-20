using System.Text.RegularExpressions;
using LogAnalyzer.Domain.RepositoryIntelligence;

namespace LogAnalyzer.Infrastructure.RepositoryIntelligence.Scanning;

public sealed partial class CSharpSourceAnalyzer
{
    private readonly CSharpDatabaseOperationExtractor
        _databaseOperationExtractor;

    private readonly CSharpContextConfigurationExtractor
        _contextConfigurationExtractor;

    public CSharpSourceAnalyzer(
        CSharpDatabaseOperationExtractor databaseOperationExtractor,
        CSharpContextConfigurationExtractor contextConfigurationExtractor)
    {
        _databaseOperationExtractor =
            databaseOperationExtractor;

        _contextConfigurationExtractor =
            contextConfigurationExtractor;
    }

    public async Task<SourceFileKnowledge> AnalyzeAsync(
        string repositoryRoot,
        string projectName,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            repositoryRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        var content =
            await File.ReadAllTextAsync(
                filePath,
                cancellationToken);

        var repositoryRelativePath =
            Path.GetRelativePath(
                repositoryRoot,
                filePath);

        var namespaceName =
            FindNamespace(
                content);

        var types =
            FindTypes(
                content,
                namespaceName);

        return new SourceFileKnowledge
        {
            FilePath =
                repositoryRelativePath,

            ProjectName =
                projectName,

            Language =
                "C#",

            Namespace =
                namespaceName,

            Types =
                types
        };
    }

    private static string FindNamespace(
        string content)
    {
        var match =
            NamespaceRegex()
                .Match(content);

        return match.Success
            ? match.Groups["name"]
                .Value
                .Trim()
            : string.Empty;
    }

    private IReadOnlyCollection<SourceTypeKnowledge>
        FindTypes(
            string content,
            string namespaceName)
    {
        var types =
            new List<SourceTypeKnowledge>();

        foreach (Match match in
                 TypeRegex().Matches(content))
        {
            var name =
                match.Groups["name"]
                    .Value
                    .Trim();

            var kind =
                match.Groups["kind"]
                    .Value
                    .Trim();

            var inheritance =
                match.Groups["bases"]
                    .Value
                    .Trim();

            var baseTypes =
                ParseBaseTypes(
                    inheritance);

            var typeBody =
                TryExtractBlock(
                    content,
                    match.Index);

            var methods =
                FindMethods(
                    content,
                    typeBody.Content,
                    typeBody.StartOffset);

            types.Add(
                new SourceTypeKnowledge
                {
                    Name =
                        name,

                    FullName =
                        string.IsNullOrWhiteSpace(
                            namespaceName)
                            ? name
                            : $"{namespaceName}.{name}",

                    Kind =
                        NormalizeTypeKind(
                            kind),

                    BaseTypes =
                        baseTypes,

                    Methods =
                        methods
                });
        }

        return types
            .GroupBy(
                type =>
                    type.FullName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.First())
            .ToArray();
    }

    private static IReadOnlyCollection<string>
        ParseBaseTypes(
            string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .TrimStart(':')
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(item =>
                !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private IReadOnlyCollection<SourceMethodKnowledge>
        FindMethods(
            string fullSource,
            string typeContent,
            int typeSourceOffset)
    {
        if (string.IsNullOrWhiteSpace(
                typeContent))
        {
            return [];
        }

        var methods =
            new List<SourceMethodKnowledge>();

        foreach (Match match in
                 MethodRegex().Matches(typeContent))
        {
            var methodName =
                match.Groups["name"]
                    .Value
                    .Trim();

            if (IsControlKeyword(
                    methodName))
            {
                continue;
            }

            var signature =
                NormalizeWhitespace(
                    match.Value);

            var absoluteDeclarationIndex =
                typeSourceOffset +
                match.Index;

            var methodBlock =
                TryExtractBlock(
                    fullSource,
                    absoluteDeclarationIndex);

            var databaseOperations =
                _databaseOperationExtractor.Extract(
                    methodBlock.Content);

            var contextConfiguration =
                _contextConfigurationExtractor.Extract(
                    methodBlock.Content);

            var lineNumber =
                GetLineNumber(
                    fullSource,
                    absoluteDeclarationIndex);

            methods.Add(
                new SourceMethodKnowledge
                {
                    Name =
                        methodName,

                    Signature =
                        signature,

                    LineNumber =
                        lineNumber,

                    CalledMethods =
                        [],

                    DatabaseOperations =
                        databaseOperations,

                    DbContexts =
                        contextConfiguration
                            .DbContexts,

                    ConfigurationKeys =
                        contextConfiguration
                            .ConfigurationKeys,

                    Routes =
                        []
                });
        }

        return methods
            .GroupBy(
                method =>
                    $"{method.Name}|{method.Signature}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.First())
            .ToArray();
    }

    private static ExtractedBlock TryExtractBlock(
        string content,
        int declarationStart)
    {
        if (declarationStart < 0 ||
            declarationStart >= content.Length)
        {
            return new ExtractedBlock(
                string.Empty,
                declarationStart);
        }

        var openingBrace =
            content.IndexOf(
                '{',
                declarationStart);

        if (openingBrace < 0)
        {
            return new ExtractedBlock(
                content[declarationStart..],
                declarationStart);
        }

        var depth =
            0;

        for (var index = openingBrace;
             index < content.Length;
             index++)
        {
            switch (content[index])
            {
                case '{':
                    depth++;
                    break;

                case '}':
                    depth--;

                    if (depth == 0)
                    {
                        return new ExtractedBlock(
                            content[
                                openingBrace..(index + 1)],
                            openingBrace);
                    }

                    break;
            }
        }

        return new ExtractedBlock(
            content[openingBrace..],
            openingBrace);
    }

    private static int GetLineNumber(
        string content,
        int characterIndex)
    {
        if (characterIndex <= 0)
        {
            return 1;
        }

        var lineNumber =
            1;

        var maximum =
            Math.Min(
                characterIndex,
                content.Length);

        for (var index = 0;
             index < maximum;
             index++)
        {
            if (content[index] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static bool IsControlKeyword(
        string value)
    {
        return value.Equals(
                   "if",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "for",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "foreach",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "while",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "switch",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "catch",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "using",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "lock",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTypeKind(
        string value)
    {
        if (value.Equals(
                "interface",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Interface";
        }

        if (value.Equals(
                "record",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Record";
        }

        if (value.Equals(
                "struct",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Struct";
        }

        return "Class";
    }

    private static string NormalizeWhitespace(
        string value)
    {
        return Regex.Replace(
                value,
                @"\s+",
                " ")
            .Trim();
    }

    private sealed record ExtractedBlock(
        string Content,
        int StartOffset);

    [GeneratedRegex(
        @"\bnamespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
        RegexOptions.Multiline)]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(
        @"(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly)\s+)*" +
        @"(?<kind>class|interface|record|struct)\s+" +
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)" +
        @"(?<bases>\s*:\s*[^{\r\n]+)?",
        RegexOptions.Multiline)]
    private static partial Regex TypeRegex();

    [GeneratedRegex(
        @"(?:(?:public|internal|private|protected|static|virtual|override|abstract|async|sealed|new|extern|partial)\s+)+" +
        @"(?:[A-Za-z_][A-Za-z0-9_<>,?.\[\]\s]*\s+)" +
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*" +
        @"\([^;{}]*\)",
        RegexOptions.Multiline)]
    private static partial Regex MethodRegex();
}