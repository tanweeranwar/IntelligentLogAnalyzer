namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public enum KnowledgeContributionType
{
    Unknown = 0,

    ApplicationIdentity = 1,

    Environment = 2,

    Server = 3,

    Namespace = 4,

    Assembly = 5,

    Exception = 6,

    ApiEndpoint = 7,

    Workflow = 8,

    Component = 9,

    Dependency = 10,

    DatabaseContext = 11,

    DatabaseObject = 12,

    Runbook = 13,

    KnownIssue = 14,

    Technology = 15,

    Repository = 16,

    Configuration = 17,

    Owner = 18,

    BusinessArea = 19,

    Version = 20,

    Other = 100
}