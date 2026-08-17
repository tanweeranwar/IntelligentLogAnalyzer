namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApplicationMetadata
{
    public string ApplicationId { get; init; } = string.Empty;

    public string ApplicationName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string OwnerTeam { get; init; } = string.Empty;

    public string BusinessOwner { get; init; } = string.Empty;

    public string TechnicalOwner { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0";

    public string PackageVersion { get; init; } = "1.0";

    public bool IsActive { get; init; } = true;

    public IReadOnlyCollection<string> Technologies { get; init; } = [];

    public IReadOnlyCollection<string> Environments { get; init; } = [];

    public IReadOnlyCollection<string> SupportTeams { get; init; } = [];

    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
}