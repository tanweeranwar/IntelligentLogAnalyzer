using LogAnalyzer.Application.Interfaces;

namespace LogAnalyzer.Infrastructure.Parsers;

public sealed class LogParserResolver : ILogParserResolver
{
    private readonly IReadOnlyCollection<ILogParser> _parsers;

    public LogParserResolver(
        IEnumerable<ILogParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        _parsers = parsers.ToArray();
    }

    public IReadOnlyCollection<string> SupportedExtensions =>
        _parsers
            .SelectMany(parser =>
                parser.SupportedExtensions)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension)
            .ToArray();

    public async Task<ILogParser> ResolveAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(stream);

        var candidates = _parsers
            .Where(parser =>
                parser.CanParse(fileName))
            .OrderBy(parser =>
                parser is PlainTextLogParser ? 1 : 0)
            .ToArray();

        foreach (var parser in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResetStream(stream);

            var canParse =
                await parser.CanParseAsync(
                    fileName,
                    stream,
                    cancellationToken);

            if (!canParse)
            {
                continue;
            }

            ResetStream(stream);

            return parser;
        }

        throw new NotSupportedException(
            $"No parser is available for " +
            $"'{Path.GetExtension(fileName)}' files.");
    }

    private static void ResetStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }
}