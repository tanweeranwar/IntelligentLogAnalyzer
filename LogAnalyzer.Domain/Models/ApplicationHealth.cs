namespace LogAnalyzer.Domain.Models;

public sealed class ApplicationHealth
{
    public int HealthScore { get; init; }

    public string Status { get; init; } = string.Empty;

    public int ActiveIncidents { get; init; }

    public int CriticalIncidents { get; init; }

    public int HighIncidents { get; init; }

    public int MediumIncidents { get; init; }

    public int LowIncidents { get; init; }

    public string MostImpactedServer { get; init; } = string.Empty;

    public string MostImpactedApi { get; init; } = string.Empty;

    public string MostCommonException { get; init; } = string.Empty;

    public TimeSpan AverageIncidentDuration { get; init; }
}