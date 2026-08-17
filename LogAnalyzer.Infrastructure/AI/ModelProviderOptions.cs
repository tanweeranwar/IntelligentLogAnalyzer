namespace LogAnalyzer.Infrastructure.AI;

public sealed class ModelProviderOptions
{
    public const string SectionName =
        "AI";

    public string Provider { get; set; } =
        "Ollama";
}