namespace LogAnalyzer.Infrastructure.AI;

internal sealed class AuthorityAwareEvidence
{
    public IReadOnlyCollection<EvidenceClaim> ConfirmedFacts
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<EvidenceClaim> CandidateContext
    {
        get;
        init;
    } = [];

    public IReadOnlyCollection<string> Unknowns
    {
        get;
        init;
    } = [];

    public string ToPromptText()
    {
        var confirmed =
            ConfirmedFacts.Count == 0
                ? "- None"
                : string.Join(
                    Environment.NewLine,
                    ConfirmedFacts.Select(
                        claim =>
                            $"- [{claim.Authority}] {claim.Label}: {claim.Value}"));

        var candidates =
            CandidateContext.Count == 0
                ? "- None"
                : string.Join(
                    Environment.NewLine,
                    CandidateContext.Select(
                        claim =>
                            $"- [{claim.ConfidenceScore}% / {claim.Authority}] " +
                            $"{claim.Label}: {claim.Value}"));

        var unknowns =
            Unknowns.Count == 0
                ? "- None"
                : string.Join(
                    Environment.NewLine,
                    Unknowns.Select(
                        value =>
                            $"- {value}"));

        return
            $$"""
            CONFIRMED EVIDENCE
            {{confirmed}}

            CANDIDATE CONTEXT
            These items are NOT confirmed facts.
            Do not use them to establish application, workflow,
            database, dependency, or root cause unless independently
            supported by confirmed evidence.

            {{candidates}}

            UNKNOWNS
            {{unknowns}}
            """;
    }
}