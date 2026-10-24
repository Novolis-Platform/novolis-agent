namespace Novolis.Agent.Core;

public static class AgentActionSchema
{
    public static Dictionary<string, object?> Object(
        IEnumerable<string>? required,
        params (string Name, string Type)[] properties)
    {
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, type) in properties)
        {
            props[name] = new Dictionary<string, object?> { ["type"] = type };
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = props,
        };
        if (required is not null)
        {
            schema["required"] = required.ToArray();
        }

        return schema;
    }
}
