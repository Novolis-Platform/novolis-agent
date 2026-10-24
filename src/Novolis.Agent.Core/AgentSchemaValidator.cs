using System.Globalization;
using System.Text.Json;

namespace Novolis.Agent.Core;

public static class AgentSchemaValidator
{
    public static bool TryValidate(IReadOnlyDictionary<string, object?>? schema, JsonElement instance, out string error)
    {
        error = "";
        if (schema is null || schema.Count == 0)
        {
            return true;
        }

        var json = JsonSerializer.Serialize(schema);
        using var doc = JsonDocument.Parse(json);
        return TryValidateElement(doc.RootElement, instance, out error);
    }

    public static bool TryValidateElement(JsonElement schema, JsonElement instance, out string error)
    {
        error = "";
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (instance.ValueKind != JsonValueKind.Object)
        {
            error = "Arguments must be an object.";
            return false;
        }

        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var name in required.EnumerateArray())
            {
                var key = name.GetString();
                if (string.IsNullOrEmpty(key) || !TryGet(instance, key, out _))
                {
                    error = $"Missing required argument '{key}'.";
                    return false;
                }
            }
        }

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            if (!TryGet(instance, prop.Name, out var value))
            {
                continue;
            }

            var expected = prop.Value.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : "string";
            if (!Matches(expected, value))
            {
                error = $"Argument '{prop.Name}' must be {expected}.";
                return false;
            }
        }

        return true;
    }

    private static bool TryGet(JsonElement instance, string name, out JsonElement value)
    {
        if (instance.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in instance.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool Matches(string? expected, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return expected switch
        {
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)
                || value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "number" => value.ValueKind == JsonValueKind.Number
                || value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                || value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out _),
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => value.ValueKind == JsonValueKind.String || value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
        };
    }
}
