using MessagePack;

namespace Novolis.Agent.Core;

/// <summary>Command envelope: action id plus string parameter bag and optional typed fields.</summary>
[MessagePackObject]
public sealed class AgentCommand
{
    [Key(0)] public string ActionId { get; set; } = "";

    [Key(1)] public Dictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);

    // Scene / CAD typed optional fields (JSON HTTP); ignored by MessagePack key layout beyond 1 when unset.
    [IgnoreMember] public string? Path { get; set; }
    [IgnoreMember] public string? NodeId { get; set; }
    [IgnoreMember] public string? ParentId { get; set; }
    [IgnoreMember] public string? LightKind { get; set; }
    [IgnoreMember] public string? Name { get; set; }
    [IgnoreMember] public float? Intensity { get; set; }
    [IgnoreMember] public float? X { get; set; }
    [IgnoreMember] public float? Y { get; set; }
    [IgnoreMember] public float? Z { get; set; }
    [IgnoreMember] public float? Rx { get; set; }
    [IgnoreMember] public float? Ry { get; set; }
    [IgnoreMember] public float? Rz { get; set; }
    [IgnoreMember] public string? GeneratorKind { get; set; }
    [IgnoreMember] public string? ModifierKind { get; set; }
    [IgnoreMember] public string? SourceId { get; set; }
    [IgnoreMember] public string? InputId { get; set; }
    [IgnoreMember] public string? TargetId { get; set; }
    [IgnoreMember] public string? CutterId { get; set; }
    [IgnoreMember] public string? BooleanKind { get; set; }
    [IgnoreMember] public string? Primitive { get; set; }
    [IgnoreMember] public int? Segments { get; set; }
    [IgnoreMember] public float? Distance { get; set; }
    [IgnoreMember] public int? Count { get; set; }
    [IgnoreMember] public string? Axis { get; set; }
    [IgnoreMember] public string? MaterialColor { get; set; }
    [IgnoreMember] public string? EditMode { get; set; }
    [IgnoreMember] public string? DisplayMode { get; set; }
    [IgnoreMember] public string? Indices { get; set; }
    [IgnoreMember] public string? Vertices { get; set; }
    [IgnoreMember] public string? Phrase { get; set; }
    [IgnoreMember] public string? Key { get; set; }
    [IgnoreMember] public string? Value { get; set; }
    /// <summary>When set, used by groundphrase (default true if unset).</summary>
    [IgnoreMember] public bool? Select { get; set; }
    [IgnoreMember] public bool? Additive { get; set; }
    [IgnoreMember] public Dictionary<string, object?>? Extra { get; set; }

    public string? Get(string key) =>
        Params.TryGetValue(key, out var value) ? value : null;

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        var raw = Get(key);
        return raw is not null && int.TryParse(raw, out value);
    }

    public bool TryGetDouble(string key, out double value)
    {
        value = 0;
        var raw = Get(key);
        return raw is not null && double.TryParse(raw, out value);
    }

    public bool TryGetBool(string key, out bool value)
    {
        value = false;
        var raw = Get(key);
        if (raw is null)
            return false;
        if (bool.TryParse(raw, out value))
            return true;
        if (raw is "1" or "yes")
        {
            value = true;
            return true;
        }

        if (raw is "0" or "no")
        {
            value = false;
            return true;
        }

        return false;
    }

    public AgentCommand With(string key, string? value)
    {
        if (value is not null)
            Params[key] = value;
        return this;
    }

    public AgentCommand With(string key, int value) => With(key, value.ToString());

    public AgentCommand With(string key, double value) => With(key, value.ToString("R"));

    public AgentCommand With(string key, bool value) => With(key, value ? "true" : "false");
}
