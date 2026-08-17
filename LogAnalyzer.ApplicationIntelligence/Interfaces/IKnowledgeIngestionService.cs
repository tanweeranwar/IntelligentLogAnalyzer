using LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

namespace LogAnalyzer.ApplicationIntelligence.Interfaces;

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> IngestAsync(
        KnowledgeIngestionRequest request,
        CancellationToken cancellationToken = default);
}