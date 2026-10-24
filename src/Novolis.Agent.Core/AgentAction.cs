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
    [Key(7)] public string Capability { get; set; } = "";
    [Key(8)] public string SafetyClass { get; set; } = AgentSafetyClass.Read;
    [Key(9)] public bool ApprovalRequired { get; set; }
    [Key(10)] public string Idempotency { get; set; } = "";
    [Key(11)] public string EffectPreview { get; set; } = "";
    [Key(12)] public Dictionary<string, object?>? OutputSchema { get; set; }
    [Key(13)] public string MetadataVersion { get; set; } = "";
}
