using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using LogAnalyzer.Application.AI;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.AI;

public sealed class OllamaModelProvider
    : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaModelProvider(
        HttpClient httpClient,
        IOptions<OllamaOptions> options)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _options =
            options?.Value ??
            throw new ArgumentNullException(
                nameof(options));
    }

    public string ProviderName =>
        "Ollama";

    public async Task<ModelResponse> GenerateAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            var payload =
                BuildRequestPayload(request);

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "/api/chat",
                    payload,
                    cancellationToken);

            var responseText =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();

                return new ModelResponse
                {
                    IsSuccessful =
                        false,

                    ProviderName =
                        ProviderName,

                    ModelName =
                        _options.Model,

                    ErrorMessage =
                        $"Ollama returned HTTP " +
                        $"{(int)response.StatusCode}: " +
                        responseText,

                    Duration =
                        stopwatch.Elapsed
                };
            }

            using var document =
                JsonDocument.Parse(
                    responseText);

            var root =
                document.RootElement;

            var content =
                ExtractMessageContent(root);

            var inputTokens =
                GetIntegerProperty(
                    root,
                    "prompt_eval_count");

            var outputTokens =
                GetIntegerProperty(
                    root,
                    "eval_count");

            stopwatch.Stop();

            return new ModelResponse
            {
                IsSuccessful =
                    true,

                ProviderName =
                    ProviderName,

                ModelName =
                    GetStringProperty(
                        root,
                        "model",
                        _options.Model),

                Content =
                    content,

                InputTokenCount =
                    inputTokens,

                OutputTokenCount =
                    outputTokens,

                Duration =
                    stopwatch.Elapsed,

                GeneratedAtUtc =
                    DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ModelResponse
            {
                IsSuccessful =
                    false,

                ProviderName =
                    ProviderName,

                ModelName =
                    _options.Model,

                ErrorMessage =
                    ex.Message,

                Duration =
                    stopwatch.Elapsed
            };
        }
    }

    private object BuildRequestPayload(
        ModelRequest request)
    {
        object format =
            string.IsNullOrWhiteSpace(
                request.ResponseSchema)
                ? "json"
                : ParseSchema(
                    request.ResponseSchema);

        return new
        {
            model =
                _options.Model,

            stream =
                false,

            messages =
                new object[]
                {
                    new
                    {
                        role =
                            "system",

                        content =
                            request.SystemPrompt
                    },

                    new
                    {
                        role =
                            "user",

                        content =
                            request.UserPrompt
                    }
                },

            format,

            options =
                new
                {
                    temperature =
                        _options.Temperature
                }
        };
    }

    private static JsonElement ParseSchema(
        string schema)
    {
        using var document =
            JsonDocument.Parse(schema);

        return document.RootElement.Clone();
    }

    private static string ExtractMessageContent(
        JsonElement root)
    {
        if (!root.TryGetProperty(
                "message",
                out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty(
                "content",
                out var content))
        {
            return string.Empty;
        }

        return content.ValueKind ==
               JsonValueKind.String
            ? content.GetString()
              ?? string.Empty
            : string.Empty;
    }

    private static int GetIntegerProperty(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var property))
        {
            return 0;
        }

        return property.TryGetInt32(
            out var value)
                ? value
                : 0;
    }

    private static string GetStringProperty(
        JsonElement root,
        string propertyName,
        string fallback)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var property))
        {
            return fallback;
        }

        return property.ValueKind ==
               JsonValueKind.String
            ? property.GetString()
              ?? fallback
            : fallback;
    }
}