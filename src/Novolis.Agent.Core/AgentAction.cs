using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentAction
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public string Label { get; set; } = "";
    [Key(2)] public bool Enabled { get; set; }
    [Key(3)] public string? DisabledReason { get; set; }
    [Key(4)] public string Summary { get; set; } = "";
    [Key(5)] public string Params { get; set; } = "";
    [Key(6)] public Dictionary<string, object?>? Schema { get; set; }
}
