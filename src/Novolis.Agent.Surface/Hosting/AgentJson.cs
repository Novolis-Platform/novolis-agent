using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Agent.Surface;

/// <summary>Shared <see cref="JsonSerializerOptions"/> for every JSON-speaking transport in this package.</summary>
public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
