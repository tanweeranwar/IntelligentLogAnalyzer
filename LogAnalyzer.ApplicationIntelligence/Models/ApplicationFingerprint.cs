namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApplicationFingerprint
{
    public string Id { get; init; } = string.Empty;

    public FingerprintType Type { get; init; } =
        FingerprintType.Unknown;

    public string Value { get; init; } = string.Empty;

    public string MatchMode { get; init; } = "Contains";

    public int Weight { get; init; } = 50;

    public bool IsCaseSensitive { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}