using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentDecisionEvent
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public int Day { get; set; }
    [Key(2)] public string HubId { get; set; } = "";
    [Key(3)] public string DecisionLine { get; set; } = "";
    [Key(4)] public AgentSnapshot? Snapshot { get; set; }
    [Key(5)] public string? CorrelationId { get; set; }
    [Key(6)] public string? ResourceId { get; set; }
    [Key(7)] public string? Severity { get; set; }
}
