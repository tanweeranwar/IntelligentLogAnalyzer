using System.Text.Json;

namespace LogAnalyzer.Infrastructure.AI;

internal sealed class InvestigationModelResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions =
    CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive =
                    true
            };

        options.Converters.Add(
            new StringOrArrayJsonConverter());

        return options;
    }

    public bool TryParse(
        string content,
        out ModelInvestigationResult result,
        out string error)
    {
        result =
            new ModelInvestigationResult();

        error =
            string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            error =
                "The model returned an empty response.";

            return false;
        }

        try
        {
            var normalized =
                NormalizeJson(content);

            var parsed =
                JsonSerializer.Deserialize<
                    ModelInvestigationResult>(
                        normalized,
                        JsonOptions);

            if (parsed is null)
            {
                error =
                    "The model response could not be deserialized.";

                return false;
            }

            result =
                parsed;

            return true;
        }
        catch (JsonException ex)
        {
            error =
                $"Invalid model JSON: {ex.Message}";

            return false;
        }
    }

    private static string NormalizeJson(
        string content)
    {
        var value =
            content.Trim();

        if (value.StartsWith(
                "```json",
                StringComparison.OrdinalIgnoreCase))
        {
            value =
                value["```json".Length..];
        }
        else if (value.StartsWith(
                     "```",
                     StringComparison.Ordinal))
        {
            value =
                value[3..];
        }

        if (value.EndsWith(
                "```",
                StringComparison.Ordinal))
        {
            value =
                value[..^3];
        }

        return value.Trim();
    }
}