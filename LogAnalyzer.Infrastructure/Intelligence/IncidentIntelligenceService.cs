using LogAnalyzer.Application.Interfaces;
using LogAnalyzer.Domain.Models;

namespace LogAnalyzer.Infrastructure.Intelligence;

public sealed class IncidentIntelligenceService
    : IIncidentIntelligenceService
{
    public IncidentIntelligence Analyze(
        string message,
        string exceptionType,
        int? httpStatusCode,
        int occurrenceCount)
    {
        var combinedText =
            $"{exceptionType} {message}".ToLowerInvariant();

        if (IsDatabaseTimeout(combinedText))
        {
            return CreateResult(
                baseScore: 80,
                occurrenceCount,
                likelyCause:
                    "A database command or connection exceeded its configured timeout.",
                impact:
                    "Application requests may be delayed or fail while waiting for the database.",
                recommendedChecks:
                [
                    "Check SQL blocking and long-running queries.",
                    "Review database CPU, memory and connection usage.",
                    "Identify the stored procedure or query involved.",
                    "Verify command timeout and connection timeout settings."
                ]);
        }

        if (IsTemplateExecutionTimeout(combinedText))
        {
            return CreateResult(
                baseScore: 75,
                occurrenceCount,
                likelyCause:
                    "Template processing exceeded its configured execution limit.",
                impact:
                    "Document, email or template-based processing may be delayed or fail.",
                recommendedChecks:
                [
                    "Review the template being processed.",
                    "Check downstream database and service calls.",
                    "Review processing duration before the timeout.",
                    "Verify whether the timeout threshold is appropriate."
                ]);
        }

        if (IsNullReference(combinedText))
        {
            return CreateResult(
                baseScore: 65,
                occurrenceCount,
                likelyCause:
                    "An application object or dependency was null when accessed.",
                impact:
                    "The affected operation may fail until the missing value or dependency is corrected.",
                recommendedChecks:
                [
                    "Review the top application stack frame.",
                    "Check database lookups that may return null.",
                    "Verify dependency injection registrations.",
                    "Validate request fields and configuration values."
                ]);
        }

        if (IsAuthenticationFailure(
                combinedText,
                httpStatusCode))
        {
            return CreateResult(
                baseScore: 70,
                occurrenceCount,
                likelyCause:
                    "Authentication or authorization validation failed.",
                impact:
                    "Users or systems may be unable to access the requested operation.",
                recommendedChecks:
                [
                    "Verify the token, cookie or Windows identity.",
                    "Check user roles and permissions.",
                    "Review authentication service availability.",
                    "Confirm clock synchronization and token expiry."
                ]);
        }

        if (httpStatusCode >= 500)
        {
            return CreateResult(
                baseScore: 85,
                occurrenceCount,
                likelyCause:
                    "The server failed while processing the request.",
                impact:
                    "The requested business operation may be unavailable.",
                recommendedChecks:
                [
                    "Review the related exception and stack trace.",
                    "Check downstream services and database connectivity.",
                    "Review recent deployments and configuration changes.",
                    "Correlate failures using request or correlation IDs."
                ]);
        }

        if (httpStatusCode >= 400)
        {
            return CreateResult(
                baseScore: 45,
                occurrenceCount,
                likelyCause:
                    "The request was rejected because of invalid input, missing data or client-side validation.",
                impact:
                    "The specific request cannot be completed until the request data is corrected.",
                recommendedChecks:
                [
                    "Capture and compare the request payload.",
                    "Review API model-validation errors.",
                    "Verify required headers and parameters.",
                    "Confirm the client is using the expected API contract."
                ]);
        }

        if (combinedText.Contains("signalr") &&
            combinedText.Contains("disconnected"))
        {
            return CreateResult(
                baseScore: 50,
                occurrenceCount,
                likelyCause:
                    "The real-time SignalR connection was interrupted.",
                impact:
                    "Live updates or notifications may be temporarily delayed.",
                recommendedChecks:
                [
                    "Check application pool recycling or server restarts.",
                    "Review network and proxy timeout settings.",
                    "Verify the SignalR endpoint is reachable.",
                    "Check whether the client automatically reconnected."
                ]);
        }

        if (combinedText.Contains("warning"))
        {
            return CreateResult(
                baseScore: 25,
                occurrenceCount,
                likelyCause:
                    "The application detected a condition that may require attention.",
                impact:
                    "No confirmed failure is visible, but the condition may contribute to later incidents.",
                recommendedChecks:
                [
                    "Review surrounding log entries.",
                    "Check whether the warning repeats frequently.",
                    "Confirm whether an error follows the warning.",
                    "Compare with normal application behavior."
                ]);
        }

        return CreateResult(
            baseScore: 35,
            occurrenceCount,
            likelyCause:
                "The available log evidence is not sufficient to identify a specific cause.",
            impact:
                "The impact should be validated using surrounding logs and application context.",
            recommendedChecks:
            [
                "Review surrounding log entries.",
                "Inspect the complete stack trace.",
                "Check related application and infrastructure logs.",
                "Correlate the event with recent changes."
            ]);
    }

    private static IncidentIntelligence CreateResult(
        int baseScore,
        int occurrenceCount,
        string likelyCause,
        string impact,
        IReadOnlyCollection<string> recommendedChecks)
    {
        var frequencyScore = occurrenceCount switch
        {
            >= 100 => 15,
            >= 50 => 12,
            >= 20 => 8,
            >= 5 => 4,
            _ => 0
        };

        var finalScore = Math.Min(
            baseScore + frequencyScore,
            100);

        return new IncidentIntelligence
        {
            PriorityScore = finalScore,
            Priority = GetPriority(finalScore),
            LikelyCause = likelyCause,
            Impact = impact,
            RecommendedChecks = recommendedChecks
        };
    }

    private static string GetPriority(int score)
    {
        return score switch
        {
            >= 85 => "Critical",
            >= 65 => "High",
            >= 40 => "Medium",
            _ => "Low"
        };
    }

    private static bool IsDatabaseTimeout(string text)
    {
        return
            text.Contains("sql timeout") ||
            text.Contains("command timeout") ||
            text.Contains("execution timeout expired") ||
            text.Contains("database timeout");
    }

    private static bool IsTemplateExecutionTimeout(string text)
    {
        return
            text.Contains("templateexecutionexception") ||
            text.Contains("template execution timed out");
    }

    private static bool IsNullReference(string text)
    {
        return text.Contains("nullreferenceexception");
    }

    private static bool IsAuthenticationFailure(
        string text,
        int? statusCode)
    {
        return
            statusCode is 401 or 403 ||
            text.Contains("unauthorized") ||
            text.Contains("authentication failed") ||
            text.Contains("access denied");
    }
}