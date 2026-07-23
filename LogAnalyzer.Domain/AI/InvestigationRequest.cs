using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Domain.AI;

public sealed class InvestigationRequest
{
    public required LogIncident Incident
    {
        get;
        init;
    }

    public required ApplicationContextResult Context
    {
        get;
        init;
    }

    public required IReadOnlyCollection<NormalizedLogEntry> Evidence
    {
        get;
        init;
    }

    public required IReadOnlyCollection<ErrorSummary> ErrorPatterns
    {
        get;
        init;
    }

    public string Question
    {
        get;
        init;
    } = string.Empty;

    public string Application
    {
        get;
        init;
    } = string.Empty;

    public string Environment
    {
        get;
        init;
    } = string.Empty;

    public int MaxEvidenceEntries
    {
        get;
        init;
    } = 10;

    public InvestigationMode Mode
    {
        get;
        init;
    } = InvestigationMode.Deep;

    public bool IncludeStackTrace
    {
        get;
        init;
    } = true;

    public bool IncludeRawLogContent
    {
        get;
        init;
    }

    public bool IncludeKnownIssues
    {
        get;
        init;
    } = true;
}