using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogAnalyzer.Infrastructure.AI;

internal sealed class StringOrArrayJsonConverter
    : JsonConverter<IReadOnlyCollection<string>>
{
    public override IReadOnlyCollection<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType ==
            JsonTokenType.String)
        {
            var value =
                reader.GetString();

            return string.IsNullOrWhiteSpace(value)
                ? []
                : [value];
        }

        if (reader.TokenType ==
            JsonTokenType.StartArray)
        {
            var values =
                new List<string>();

            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonTokenType.EndArray)
                {
                    return values;
                }

                if (reader.TokenType ==
                    JsonTokenType.String)
                {
                    var value =
                        reader.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }

            throw new JsonException(
                "Unexpected end of JSON while reading string array.");
        }

        if (reader.TokenType ==
            JsonTokenType.Null)
        {
            return [];
        }

        throw new JsonException(
            $"Expected string or string array but received {reader.TokenType}.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyCollection<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}