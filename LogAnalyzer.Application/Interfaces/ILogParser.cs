using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface ILogParser
{
    Task<LogAnalysisResult> AnalyzeAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}