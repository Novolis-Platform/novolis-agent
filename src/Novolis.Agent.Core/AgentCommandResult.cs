using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentCommandResult
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; }
    [Key(2)] public string ActionId { get; set; } = "";
    [Key(3)] public string Message { get; set; } = "";
    [Key(4)] public string? ErrorCode { get; set; }
    [Key(5)] public AgentSnapshot? Snapshot { get; set; }
    [Key(6)] public string? NodeId { get; set; }
}
