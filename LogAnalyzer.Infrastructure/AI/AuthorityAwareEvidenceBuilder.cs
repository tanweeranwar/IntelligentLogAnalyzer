namespace LogAnalyzer.Infrastructure.AI;

internal sealed class AuthorityAwareEvidenceBuilder
{
    public AuthorityAwareEvidence Build(
        IReadOnlyCollection<EvidenceClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var confirmed =
            claims
                .Where(claim =>
                    claim.IsConfirmed)
                .OrderByDescending(claim =>
                    claim.ConfidenceScore)
                .Take(18)
                .ToArray();

        var candidates =
            claims
                .Where(claim =>
                    !claim.IsConfirmed)
                .Where(claim =>
                    !claim.IsWeak ||
                    claim.Authority ==
                    EvidenceAuthority.FuzzyContext)
                .OrderByDescending(claim =>
                    claim.ConfidenceScore)
                .Take(8)
                .ToArray();

        return new AuthorityAwareEvidence
        {
            ConfirmedFacts =
                confirmed,

            CandidateContext =
                candidates,

            Unknowns =
                BuildUnknowns(
                    confirmed,
                    candidates)
        };
    }

    private static IReadOnlyCollection<string> BuildUnknowns(
        IReadOnlyCollection<EvidenceClaim> confirmed,
        IReadOnlyCollection<EvidenceClaim> candidates)
    {
        var unknowns =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var applicationConfirmed =
            confirmed.Any(claim =>
                claim.Category.Equals(
                    "Application",
                    StringComparison.OrdinalIgnoreCase) &&
                claim.Label.Contains(
                    "Application",
                    StringComparison.OrdinalIgnoreCase));

        var workflowConfirmed =
            confirmed.Any(claim =>
                claim.Label.Contains(
                    "Workflow",
                    StringComparison.OrdinalIgnoreCase));

        var databaseOperationConfirmed =
            confirmed.Any(claim =>
                claim.Label.Equals(
                    "Database operation",
                    StringComparison.OrdinalIgnoreCase));

        if (!applicationConfirmed)
        {
            unknowns.Add(
                "Application identity is not confirmed.");
        }

        if (!workflowConfirmed)
        {
            unknowns.Add(
                "Affected workflow is not confirmed.");
        }

        if (databaseOperationConfirmed)
        {
            unknowns.Add(
                "The physical database containing the observed database operation is not confirmed unless explicitly present in confirmed evidence.");
        }

        if (candidates.Count > 0)
        {
            unknowns.Add(
                "Candidate application knowledge exists but is not authoritative.");
        }

        return unknowns.ToArray();
    }
}