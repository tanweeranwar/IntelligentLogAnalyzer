using System.Text.RegularExpressions;

namespace LogAnalyzer.Infrastructure.RepositoryIntelligence.Scanning;

public sealed partial class CSharpDatabaseOperationExtractor
{
    public IReadOnlyCollection<string> Extract(
        string methodBody)
    {
        if (string.IsNullOrWhiteSpace(methodBody))
        {
            return [];
        }

        var operations =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (Match match in
                 StoredProcedureRegex().Matches(methodBody))
        {
            var value =
                match.Groups["name"]
                    .Value
                    .Trim();

            if (!string.IsNullOrWhiteSpace(value))
            {
                operations.Add(value);
            }
        }

        foreach (Match match in
                 CommandTextRegex().Matches(methodBody))
        {
            var value =
                NormalizeSqlOperation(
                    match.Groups["value"].Value);

            if (!string.IsNullOrWhiteSpace(value))
            {
                operations.Add(value);
            }
        }

        foreach (Match match in
                 ExecuteSqlRegex().Matches(methodBody))
        {
            var value =
                NormalizeSqlOperation(
                    match.Groups["value"].Value);

            if (!string.IsNullOrWhiteSpace(value))
            {
                operations.Add(value);
            }
        }

        return operations.ToArray();
    }

    private static string NormalizeSqlOperation(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized =
            value
                .Replace(
                    "\r",
                    " ",
                    StringComparison.Ordinal)
                .Replace(
                    "\n",
                    " ",
                    StringComparison.Ordinal)
                .Trim();

        var execMatch =
            ExecNameRegex().Match(normalized);

        if (execMatch.Success)
        {
            return execMatch.Groups["name"]
                .Value
                .Trim();
        }

        var directName =
            DirectOperationRegex().Match(normalized);

        return directName.Success
            ? directName.Groups["name"]
                .Value
                .Trim()
            : string.Empty;
    }

    [GeneratedRegex(
        @"\b(?:EXEC|EXECUTE)\s+(?:dbo\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex StoredProcedureRegex();

    [GeneratedRegex(
        @"CommandText\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex CommandTextRegex();

    [GeneratedRegex(
        @"(?:ExecuteSqlRaw|ExecuteSqlRawAsync|ExecuteSqlInterpolated|ExecuteSqlInterpolatedAsync)\s*\(\s*""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExecuteSqlRegex();

    [GeneratedRegex(
        @"\b(?:EXEC|EXECUTE)\s+(?:dbo\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExecNameRegex();

    [GeneratedRegex(
        @"^(?:dbo\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DirectOperationRegex();
}