namespace LogAnalyzer.Infrastructure.AI;

internal sealed class EvidenceAuthorityEvaluator
{
    public IReadOnlyCollection<EvidenceClaim> Evaluate(
        DistilledEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return evidence.Facts
            .Select(
                fact =>
                    new EvidenceClaim
                    {
                        Category =
                            fact.Category,

                        Label =
                            fact.Label,

                        Value =
                            fact.Value,

                        Authority =
                            fact.Authority,

                        ConfidenceScore =
                            GetConfidence(
                                fact.Authority),

                        Source =
                            fact.Source
                    })
            .ToArray();
    }

    private static int GetConfidence(
        EvidenceAuthority authority)
    {
        return authority switch
        {
            EvidenceAuthority.RawLog => 95,
            EvidenceAuthority.ExplicitIdentifier => 90,
            EvidenceAuthority.OpenApiMatch => 85,
            EvidenceAuthority.RepositoryMatch => 85,
            EvidenceAuthority.ExactKnowledgeMatch => 80,
            EvidenceAuthority.KnowledgeMatch => 65,
            EvidenceAuthority.FuzzyContext => 35,
            _ => 20
        };
    }
}