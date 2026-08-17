namespace LogAnalyzer.ApplicationIntelligence.Loading;

public sealed class KnowledgePackageOptions
{
    public const string SectionName =
        "ApplicationIntelligence";

    public string RootPath { get; set; } =
        "ApplicationKnowledge";

    public bool FailOnInvalidPackage { get; set; } = true;

    public bool RequireAtLeastOnePackage { get; set; }

    public bool ReloadOnStartup { get; set; } = true;
}