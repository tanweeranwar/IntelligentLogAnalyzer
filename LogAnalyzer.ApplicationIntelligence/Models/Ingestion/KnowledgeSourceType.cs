namespace LogAnalyzer.ApplicationIntelligence.Models.Ingestion;

public enum KnowledgeSourceType
{
    Unknown = 0,
    Log = 1,
    KnowledgePackage = 2,
    Repository = 3,
    OpenApi = 4,
    Document = 5,
    Runbook = 6,
    ArchitectureDocument = 7,
    HistoricalIncident = 8,
    DatabaseMetadata = 9
}