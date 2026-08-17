namespace LogAnalyzer.Infrastructure.AI;

public sealed class OllamaOptions
{
    public const string SectionName =
        "AI:Ollama";

    public string BaseUrl { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } =
        "llama3.1:8b";

    public int TimeoutSeconds { get; set; } =
        180;

    public double Temperature { get; set; } =
        0.1;

    public bool Enabled { get; set; } =
        true;
}