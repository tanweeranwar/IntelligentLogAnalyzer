using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface ILogParser
{
    string Name { get; }

    IReadOnlyCollection<string> SupportedExtensions { get; }

    bool CanParse(string fileName);

    Task<bool> CanParseAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}