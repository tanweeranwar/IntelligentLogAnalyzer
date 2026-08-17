namespace LogAnalyzer.Application.AI;

public sealed class ModelRequest
{
    public string SystemPrompt { get; init; } =
        string.Empty;

    public string UserPrompt { get; init; } =
        string.Empty;

    public string ResponseSchema { get; init; } =
        string.Empty;

    public IReadOnlyDictionary<string, string> Metadata
    {
        get;
        init;
    } = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase);
}