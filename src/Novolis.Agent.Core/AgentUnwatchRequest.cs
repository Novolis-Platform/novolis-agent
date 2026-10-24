using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentUnwatchRequest
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string WatchId { get; set; } = "";
}
