using LogAnalyzer.Application.AI;

namespace LogAnalyzer.Infrastructure.AI;

public sealed class MockModelProvider
    : IModelProvider
{
    public string ProviderName =>
        "Mock";

    public Task<ModelResponse> GenerateAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new ModelResponse
            {
                IsSuccessful =
                    true,

                ProviderName =
                    ProviderName,

                ModelName =
                    "Mock",

                Content =
                    """
                    {
                      "executiveSummary": "Mock model response.",
                      "rootCauses": [],
                      "investigationSteps": [],
                      "recommendations": [],
                      "unknowns": []
                    }
                    """,

                Duration =
                    TimeSpan.Zero
            });
    }
}