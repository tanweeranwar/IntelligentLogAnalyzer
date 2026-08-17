using LogAnalyzer.Application.AI;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.AI;

public sealed class ModelProviderResolver
{
    private readonly OllamaModelProvider
        _ollamaProvider;

    private readonly MockModelProvider
        _mockProvider;

    private readonly ModelProviderOptions
        _options;

    public ModelProviderResolver(
        OllamaModelProvider ollamaProvider,
        MockModelProvider mockProvider,
        IOptions<ModelProviderOptions> options)
    {
        _ollamaProvider =
            ollamaProvider;

        _mockProvider =
            mockProvider;

        _options =
            options.Value;
    }

    public IModelProvider Resolve()
    {
        return _options.Provider
            .Trim()
            .ToLowerInvariant() switch
        {
            "ollama" =>
                _ollamaProvider,

            "mock" =>
                _mockProvider,

            _ =>
                _mockProvider
        };
    }
}