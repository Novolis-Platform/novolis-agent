using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentUnwatchResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; }
    [Key(2)] public string WatchId { get; set; } = "";
}
