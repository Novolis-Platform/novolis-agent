using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentWatchNextResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; } = true;
    [Key(2)] public string? Error { get; set; }
    [Key(3)] public string WatchId { get; set; } = "";
    [Key(4)] public AgentWatchEvent[] Events { get; set; } = [];
}
