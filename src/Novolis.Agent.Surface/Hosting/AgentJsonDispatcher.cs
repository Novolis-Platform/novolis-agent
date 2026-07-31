using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>Shared JSON method dispatch for stdio / TCP / JSON-RPC / HTTP / WebSocket hosts.</summary>
public static class AgentJsonDispatcher
{
    public static readonly JsonSerializerOptions JsonOptions = AgentJson.Options;

    /// <summary>Dispatches a wire method (accepting <c>agent.*</c>, legacy <c>session.*</c>, and bare aliases).</summary>
    public static object Dispatch(IAgentHost host, string? method, JsonElement root)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (AgentMethodNames.IsHello(method))
            return host.Hello();
        if (AgentMethodNames.IsSnapshot(method))
            return host.Snapshot();
        if (AgentMethodNames.IsActions(method))
            return host.Actions();
        if (AgentMethodNames.IsContinue(method))
            return host.Continue();
        if (AgentMethodNames.IsSubscribe(method))
            return Subscribe(host);
        if (AgentMethodNames.IsCommand(method))
        {
            // Prefer full command object (actionId + params); don't peel nested "params" alone.
            var source = TryGetProperty(root, "command", out var c) && c.ValueKind == JsonValueKind.Object
                ? c
                : root;
            return host.Execute(ParseCommand(source));
        }

        throw new InvalidOperationException($"Unknown method '{method}'.");
    }

    /// <summary>
    /// Parses an <see cref="AgentCommand"/> from a params bag plus typed optional fields. Accepts the element
    /// directly, or wrapped once under a <c>command</c> property.
    /// </summary>
    public static AgentCommand ParseCommand(JsonElement root)
    {
        var cmd = new AgentCommand();
        if (root.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return cmd;

        var source = root;
        if (root.ValueKind == JsonValueKind.Object
            && TryGetProperty(root, "command", out var c)
            && c.ValueKind == JsonValueKind.Object)
        {
            source = c;
        }

        if (source.ValueKind != JsonValueKind.Object)
            return cmd;

        if (TryGetProperty(source, "actionId", out var actionId) && actionId.ValueKind == JsonValueKind.String)
            cmd.ActionId = actionId.GetString() ?? "";

        if (TryGetProperty(source, "params", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in nested.EnumerateObject())
            {
                cmd.Params[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.ToString();
            }
        }

        SetString(source, "path", v => cmd.Path = v);
        SetString(source, "nodeId", v => cmd.NodeId = v);
        SetString(source, "parentId", v => cmd.ParentId = v);
        SetString(source, "lightKind", v => cmd.LightKind = v);
        SetString(source, "name", v => cmd.Name = v);
        SetFloat(source, "intensity", v => cmd.Intensity = v);
        SetFloat(source, "x", v => cmd.X = v);
        SetFloat(source, "y", v => cmd.Y = v);
        SetFloat(source, "z", v => cmd.Z = v);
        SetFloat(source, "rx", v => cmd.Rx = v);
        SetFloat(source, "ry", v => cmd.Ry = v);
        SetFloat(source, "rz", v => cmd.Rz = v);
        SetString(source, "generatorKind", v => cmd.GeneratorKind = v);
        SetString(source, "modifierKind", v => cmd.ModifierKind = v);
        SetString(source, "sourceId", v => cmd.SourceId = v);
        SetString(source, "inputId", v => cmd.InputId = v);
        SetString(source, "targetId", v => cmd.TargetId = v);
        SetString(source, "cutterId", v => cmd.CutterId = v);
        SetString(source, "booleanKind", v => cmd.BooleanKind = v);
        SetString(source, "primitive", v => cmd.Primitive = v);
        SetInt(source, "segments", v => cmd.Segments = v);
        SetFloat(source, "distance", v => cmd.Distance = v);
        SetInt(source, "count", v => cmd.Count = v);
        SetString(source, "axis", v => cmd.Axis = v);
        SetString(source, "materialColor", v => cmd.MaterialColor = v);
        SetString(source, "editMode", v => cmd.EditMode = v);
        SetString(source, "displayMode", v => cmd.DisplayMode = v);
        SetString(source, "indices", v => cmd.Indices = v);
        SetBool(source, "additive", v => cmd.Additive = v);

        return cmd;
    }

    private static AgentSubscribeResponse Subscribe(IAgentHost host)
    {
        host.Subscribe();
        return new AgentSubscribeResponse { Ok = true };
    }

    private static bool TryGetProperty(JsonElement element, string camelCaseName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (element.TryGetProperty(camelCaseName, out value))
            return true;

        var pascalCaseName = char.ToUpperInvariant(camelCaseName[0]) + camelCaseName[1..];
        return element.TryGetProperty(pascalCaseName, out value);
    }

    private static void SetString(JsonElement element, string name, Action<string?> set)
    {
        if (TryGetProperty(element, name, out var p) && p.ValueKind == JsonValueKind.String)
            set(p.GetString());
    }

    private static void SetFloat(JsonElement element, string name, Action<float?> set)
    {
        if (TryGetProperty(element, name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetSingle(out var v))
            set(v);
    }

    private static void SetInt(JsonElement element, string name, Action<int?> set)
    {
        if (TryGetProperty(element, name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
            set(v);
    }

    private static void SetBool(JsonElement element, string name, Action<bool?> set)
    {
        if (TryGetProperty(element, name, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False)
            set(p.GetBoolean());
    }
}
