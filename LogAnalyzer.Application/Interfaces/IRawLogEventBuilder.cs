using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Application.Interfaces;

public interface IRawLogEventBuilder
{
    Task<IReadOnlyCollection<RawLogEvent>> BuildAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}