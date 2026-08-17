namespace LogAnalyzer.ApplicationIntelligence.Models;

public sealed class ApplicationKnowledgePackage
{
    public string PackageDirectory { get; init; } = string.Empty;

    public required ApplicationKnowledge Application { get; init; }

    public DateTimeOffset LoadedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public IReadOnlyCollection<string> SourceFiles { get; init; } = [];

    public string ContentHash { get; init; } = string.Empty;
}