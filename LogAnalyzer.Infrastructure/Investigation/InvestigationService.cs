using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.AI;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Investigation;

public sealed class InvestigationService
    : IInvestigationService
{
    private readonly IApplicationContextResolver
        _applicationContextResolver;

    private readonly IInvestigationEvidenceBuilder
        _evidenceBuilder;

    private readonly IInvestigationPreparationEngine
        _preparationEngine;

    private readonly IDecisionEngine
        _decisionEngine;

    public InvestigationService(
        IApplicationContextResolver applicationContextResolver,
        IInvestigationEvidenceBuilder evidenceBuilder,
        IInvestigationPreparationEngine preparationEngine,
        IDecisionEngine decisionEngine)
    {
        _applicationContextResolver =
            applicationContextResolver;

        _evidenceBuilder =
            evidenceBuilder;

        _preparationEngine =
            preparationEngine;

        _decisionEngine =
            decisionEngine;
    }

    public async Task<InvestigationReport> InvestigateAsync(
        LogIncident incident,
        IReadOnlyCollection<ErrorSummary> errorPatterns,
        InvestigationMode mode = InvestigationMode.Deep,
        string question = "",
        int maxEvidenceEntries = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(errorPatterns);

        cancellationToken.ThrowIfCancellationRequested();

        var applicationContext =
            await _applicationContextResolver.ResolveAsync(
                incident,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var investigationRequest =
            _evidenceBuilder.Build(
                incident,
                applicationContext,
                errorPatterns,
                question,
                maxEvidenceEntries,
                mode);

        var reasoningPackage =
            _preparationEngine.Prepare(
                investigationRequest);

        cancellationToken.ThrowIfCancellationRequested();

        return await _decisionEngine.AnalyzeAsync(
            reasoningPackage,
            cancellationToken);
    }
}