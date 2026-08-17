namespace LogAnalyzer.Infrastructure.AI;

public sealed class OllamaOptions
{
    public const string SectionName =
        "AI:Ollama";

    public string BaseUrl { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } =
        "llama3.2:3b";

    public int TimeoutSeconds { get; set; } =
        90;

    public double Temperature { get; set; } =
        0.1;

    public int MaxOutputTokens { get; set; } =
        1200;

    public int ContextWindow { get; set; } =
        4096;

    public bool Enabled { get; set; } =
        true;
}