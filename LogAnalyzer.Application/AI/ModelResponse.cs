namespace LogAnalyzer.Application.AI;

public sealed class ModelResponse
{
    public bool IsSuccessful { get; init; }

    public string ProviderName { get; init; } =
        string.Empty;

    public string ModelName { get; init; } =
        string.Empty;

    public string Content { get; init; } =
        string.Empty;

    public string ErrorMessage { get; init; } =
        string.Empty;

    public int InputTokenCount { get; init; }

    public int OutputTokenCount { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;
}