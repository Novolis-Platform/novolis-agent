using System.Text.Json;

namespace Novolis.Agent.Core;

public static class AgentCommandJson
{
    public static JsonElement Instance(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrWhiteSpace(command.ArgumentsJson))
        {
            using var typed = JsonDocument.Parse(command.ArgumentsJson);
            return typed.RootElement.Clone();
        }

        var payload = JsonSerializer.Serialize(command.Params);
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.Clone();
    }
}
