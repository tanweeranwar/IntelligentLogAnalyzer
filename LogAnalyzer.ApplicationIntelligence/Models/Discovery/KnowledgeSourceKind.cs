namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public enum KnowledgeSourceKind
{
    Unknown = 0,

    Log = 1,

    KnowledgePackage = 2,

    Runbook = 3,

    ArchitectureDocument = 4,

    OpenApi = 5,

    SourceCode = 6,

    DatabaseMetadata = 7,

    HistoricalIncident = 8,

    Configuration = 9,

    Manual = 10
}