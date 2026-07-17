using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Health;

public sealed class ApplicationHealthService : IApplicationHealthService
{
    public ApplicationHealth Calculate(
        IReadOnlyCollection<LogIncident> incidents)
    {
        if (incidents.Count == 0)
        {
            return new ApplicationHealth
            {
                HealthScore = 100,
                Status = "Healthy"
            };
        }

        var critical = incidents.Count(i =>
            i.Intelligence.Priority == "Critical");

        var high = incidents.Count(i =>
            i.Intelligence.Priority == "High");

        var medium = incidents.Count(i =>
            i.Intelligence.Priority == "Medium");

        var low = incidents.Count(i =>
            i.Intelligence.Priority == "Low");

        var score = 100;

        score -= critical * 25;
        score -= high * 10;
        score -= medium * 5;
        score -= low;

        score = Math.Clamp(score, 0, 100);

        var status = score switch
        {
            >= 90 => "Healthy",
            >= 70 => "Degraded",
            >= 40 => "Poor",
            _ => "Critical"
        };

        var mostImpactedServer = incidents
            .Where(i => !string.IsNullOrWhiteSpace(i.ServerName))
            .GroupBy(i => i.ServerName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "-";

        var mostImpactedApi = incidents
            .Where(i => !string.IsNullOrWhiteSpace(i.ApiPath))
            .GroupBy(i => i.ApiPath)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "-";

        var mostCommonException = incidents
            .Where(i => !string.IsNullOrWhiteSpace(i.ExceptionType))
            .GroupBy(i => i.ExceptionType)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "-";

        var durations = incidents
            .Where(i => i.Duration.HasValue)
            .Select(i => i.Duration!.Value)
            .ToArray();

        var averageDuration = TimeSpan.Zero;

        if (durations.Length > 0)
        {
            averageDuration = TimeSpan.FromTicks(
                Convert.ToInt64(
                    durations.Average(d => d.Ticks)));
        }

        return new ApplicationHealth
        {
            HealthScore = score,
            Status = status,

            ActiveIncidents = incidents.Count,

            CriticalIncidents = critical,
            HighIncidents = high,
            MediumIncidents = medium,
            LowIncidents = low,

            MostImpactedServer = mostImpactedServer,
            MostImpactedApi = mostImpactedApi,
            MostCommonException = mostCommonException,

            AverageIncidentDuration = averageDuration
        };
    }
}