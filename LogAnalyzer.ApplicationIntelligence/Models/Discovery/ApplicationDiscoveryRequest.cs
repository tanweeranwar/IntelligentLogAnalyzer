namespace LogAnalyzer.ApplicationIntelligence.Models.Discovery;

public sealed class ApplicationDiscoveryRequest
{
    public string ApplicationHint { get; init; } =
        string.Empty;

    public string EnvironmentHint { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> Evidence { get; init; } =
        [];

    public IReadOnlyDictionary<string, string> Metadata
    {
        get;
        init;
    } = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase);

    public static ApplicationDiscoveryRequest FromText(
        string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new ApplicationDiscoveryRequest
        {
            Evidence =
            [
                text
            ]
        };
    }
}