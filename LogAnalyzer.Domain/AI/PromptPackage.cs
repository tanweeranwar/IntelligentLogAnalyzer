namespace LogAnalyzer.Domain.AI;

public sealed class PromptPackage
{
    /// <summary>
    /// System instructions.
    /// </summary>
    public string SystemPrompt
    {
        get;
        init;
    } = string.Empty;

    /// <summary>
    /// Investigation data.
    /// </summary>
    public string UserPrompt
    {
        get;
        init;
    } = string.Empty;

    /// <summary>
    /// JSON schema expected from AI.
    /// </summary>
    public string ExpectedOutputSchema
    {
        get;
        init;
    } = string.Empty;

    /// <summary>
    /// Prompt version.
    /// </summary>
    public string Version
    {
        get;
        init;
    } = "1.0";
}