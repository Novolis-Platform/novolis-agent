using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentChangedEvent
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string Reason { get; set; } = "";
    [Key(2)] public AgentSnapshot? Snapshot { get; set; }
    [Key(3)] public string? DocumentName { get; set; }
    [Key(4)] public int NodeCount { get; set; }
}
