using System.Collections.Immutable;
using LogAnalyzer.ApplicationIntelligence.Interfaces;
using LogAnalyzer.ApplicationIntelligence.Models;

namespace LogAnalyzer.ApplicationIntelligence.Repository;

public sealed class InMemoryApplicationCatalog : IApplicationCatalog
{
    private ImmutableDictionary<string, ApplicationKnowledgePackage>
        _packages =
            ImmutableDictionary.Create<
                string,
                ApplicationKnowledgePackage>(
                    StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ApplicationKnowledgePackage> GetAll()
    {
        return _packages.Values
            .OrderBy(
                package =>
                    package.Application.Metadata.ApplicationName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ApplicationKnowledgePackage? GetByApplicationId(
        string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            applicationId);

        return _packages.TryGetValue(
            applicationId,
            out var package)
                ? package
                : null;
    }

    public bool TryGetByApplicationId(
        string applicationId,
        out ApplicationKnowledgePackage? package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            applicationId);

        return _packages.TryGetValue(
            applicationId,
            out package);
    }

    public void ReplaceAll(
        IEnumerable<ApplicationKnowledgePackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var builder =
            ImmutableDictionary.CreateBuilder<
                string,
                ApplicationKnowledgePackage>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (var package in packages)
        {
            ArgumentNullException.ThrowIfNull(package);

            var applicationId =
                package.Application.Metadata.ApplicationId;

            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new InvalidOperationException(
                    "An application package cannot be registered " +
                    "without an application ID.");
            }

            if (!builder.TryAdd(
                    applicationId,
                    package))
            {
                throw new InvalidOperationException(
                    $"Duplicate application ID '{applicationId}' " +
                    "was found while loading the application catalog.");
            }
        }

        Interlocked.Exchange(
            ref _packages,
            builder.ToImmutable());
    }
}