namespace LogAnalyzer.Application.Interfaces;

public interface ILogParserResolver
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    Task<ILogParser> ResolveAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default);
}