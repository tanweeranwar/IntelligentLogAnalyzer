using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface ILogAnalysisPipeline
{
    LogAnalysisResult Build(
        int totalLines,
        IReadOnlyCollection<NormalizedLogEntry> entries);
}