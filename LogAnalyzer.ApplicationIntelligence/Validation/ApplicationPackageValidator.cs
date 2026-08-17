using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models;

namespace LogAnalyzer.ApplicationIntelligence.Validation;

public sealed class ApplicationPackageValidator
    : IApplicationPackageValidator
{
    public void Validate(
        ApplicationKnowledgePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(package.Application);
        ArgumentNullException.ThrowIfNull(
            package.Application.Metadata);

        var errors =
            new List<string>();

        var metadata =
            package.Application.Metadata;

        Require(
            metadata.ApplicationId,
            "ApplicationId",
            errors);

        Require(
            metadata.ApplicationName,
            "ApplicationName",
            errors);

        Require(
            metadata.PackageVersion,
            "PackageVersion",
            errors);

        ValidateUniqueIds(
            package.Application.Repositories,
            repository => repository.Id,
            "repository",
            errors);

        ValidateUniqueIds(
            package.Application.Components,
            component => component.Id,
            "architecture component",
            errors);

        ValidateUniqueIds(
            package.Application.Workflows,
            workflow => workflow.Id,
            "workflow",
            errors);

        ValidateUniqueIds(
            package.Application.Dependencies,
            dependency => dependency.Id,
            "dependency",
            errors);

        ValidateUniqueIds(
            package.Application.ApiEndpoints,
            endpoint => endpoint.Id,
            "API endpoint",
            errors);

        ValidateUniqueIds(
            package.Application.DatabaseObjects,
            databaseObject => databaseObject.Id,
            "database object",
            errors);

        ValidateUniqueIds(
            package.Application.Runbooks,
            runbook => runbook.Id,
            "runbook",
            errors);

        ValidateUniqueIds(
            package.Application.KnownIssues,
            knownIssue => knownIssue.Id,
            "known issue",
            errors);

        ValidateUniqueIds(
            package.Application.Configurations,
            configuration => configuration.Id,
            "configuration item",
            errors);

        ValidateUniqueIds(
            package.Application.Fingerprints,
            fingerprint => fingerprint.Id,
            "fingerprint",
            errors);

        foreach (var fingerprint in
                 package.Application.Fingerprints)
        {
            if (fingerprint.Weight is < 1 or > 100)
            {
                errors.Add(
                    $"Fingerprint '{fingerprint.Id}' has weight " +
                    $"{fingerprint.Weight}. Weight must be between " +
                    "1 and 100.");
            }

            Require(
                fingerprint.Value,
                $"Fingerprint '{fingerprint.Id}' Value",
                errors);
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new ApplicationPackageValidationException(
            $"Application package '{metadata.ApplicationId}' " +
            $"is invalid:{Environment.NewLine}- " +
            string.Join(
                $"{Environment.NewLine}- ",
                errors));
    }

    private static void Require(
        string? value,
        string fieldName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"{fieldName} is required.");
        }
    }

    private static void ValidateUniqueIds<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        string itemType,
        ICollection<string> errors)
    {
        var ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var id =
                idSelector(item);

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(
                    $"A {itemType} is missing its ID.");

                continue;
            }

            if (!ids.Add(id))
            {
                errors.Add(
                    $"Duplicate {itemType} ID '{id}' was found.");
            }
        }
    }
}