using LogAnalyzer.Domain.AI;

namespace LogAnalyzer.Infrastructure.Investigation.Templates;

internal static class InvestigationModeTemplate
{
    public static string GetObjectives(
        InvestigationMode mode)
    {
        return mode switch
        {
            InvestigationMode.Quick =>
                BuildQuickObjectives(),

            InvestigationMode.RootCauseAnalysis =>
                BuildRootCauseAnalysisObjectives(),

            _ =>
                BuildDeepObjectives()
        };
    }

    private static string BuildQuickObjectives()
    {
        return """
               INVESTIGATION MODE: QUICK

               Focus only on the most useful immediate findings.

               Determine:
               1. What most likely failed.
               2. Where the failure most likely occurred.
               3. The top three root-cause hypotheses.
               4. The first five investigation actions.
               5. Any immediate recovery action supported by evidence.
               6. Confidence and missing information.

               Keep recommendations concise and operationally actionable.
               """;
    }

    private static string BuildDeepObjectives()
    {
        return """
               INVESTIGATION MODE: DEEP

               Perform a complete production-support investigation.

               Determine:
               1. What failed.
               2. The affected business workflow.
               3. The most likely failure point.
               4. Affected application components.
               5. Downstream and upstream dependencies.
               6. Root-cause hypotheses with supporting evidence.
               7. A prioritized investigation checklist.
               8. Suggested SQL queries when supported by context.
               9. Suggested source-code locations when supported by context.
               10. Immediate and permanent resolution recommendations.
               11. Business and operational impact.
               12. Prevention opportunities.
               13. Confidence, assumptions, and unknowns.
               """;
    }

    private static string BuildRootCauseAnalysisObjectives()
    {
        return """
               INVESTIGATION MODE: ROOT CAUSE ANALYSIS

               Produce a formal root-cause-oriented investigation.

               Determine:
               1. Incident timeline.
               2. Initial failure point.
               3. Failure propagation chain.
               4. Primary root cause.
               5. Contributing factors.
               6. Supporting and contradicting evidence.
               7. Detection gaps.
               8. Immediate corrective actions.
               9. Permanent corrective actions.
               10. Preventive actions.
               11. Monitoring and logging improvements.
               12. Assumptions, unknowns, and confidence.
               """;
    }
}