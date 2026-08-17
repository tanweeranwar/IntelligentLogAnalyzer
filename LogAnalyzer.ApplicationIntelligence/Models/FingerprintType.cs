namespace LogAnalyzer.ApplicationIntelligence.Models;

public enum FingerprintType
{
    Unknown = 0,
    Namespace = 1,
    Assembly = 2,
    Exception = 3,
    ApiPath = 4,
    Url = 5,
    Hostname = 6,
    ServerName = 7,
    Controller = 8,
    ClassName = 9,
    MethodName = 10,
    StoredProcedure = 11,
    DatabaseObject = 12,
    ConfigurationKey = 13,
    LogSource = 14,
    MessagePattern = 15,
    CorrelationPattern = 16
}