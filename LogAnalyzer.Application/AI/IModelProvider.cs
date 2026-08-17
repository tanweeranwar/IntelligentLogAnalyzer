namespace LogAnalyzer.Application.AI;

public interface IModelProvider
{
    string ProviderName { get; }

    Task<ModelResponse> GenerateAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default);
}