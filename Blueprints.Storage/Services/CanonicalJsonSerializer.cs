using System.Text.Json;
using System.Text.Json.Nodes;
using Blueprints.Storage.Abstractions;

namespace Blueprints.Storage.Services;

public sealed class CanonicalJsonSerializer : ICanonicalJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public T Deserialize<T>(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var result = JsonSerializer.Deserialize<T>(json, SerializerOptions);
        return result ?? throw new InvalidOperationException("Failed to deserialize JSON.");
    }

    public string Serialize<T>(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Failed to parse serialized JSON.");

        var canonicalNode = Canonicalize(node);
        return canonicalNode.ToJsonString(SerializerOptions);
    }

    private static JsonNode Canonicalize(JsonNode node) =>
        node switch
        {
            JsonObject jsonObject => CanonicalizeObject(jsonObject),
            JsonArray jsonArray => CanonicalizeArray(jsonArray),
            _ => node.DeepClone(),
        };

    private static JsonArray CanonicalizeArray(JsonArray jsonArray)
    {
        var result = new JsonArray();
        foreach (var item in jsonArray)
        {
            result.Add(item is null ? null : Canonicalize(item));
        }

        return result;
    }

    private static JsonObject CanonicalizeObject(JsonObject jsonObject)
    {
        var result = new JsonObject();

        foreach (var property in jsonObject.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            result[property.Key] = property.Value is null ? null : Canonicalize(property.Value);
        }

        return result;
    }
}
