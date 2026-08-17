namespace LogAnalyzer.Infrastructure.AI;

internal enum EvidenceAuthority
{
    Unknown = 0,
    FuzzyContext = 10,
    KnowledgeMatch = 20,
    ExactKnowledgeMatch = 30,
    RepositoryMatch = 40,
    OpenApiMatch = 50,
    ExplicitIdentifier = 60,
    RawLog = 70
}