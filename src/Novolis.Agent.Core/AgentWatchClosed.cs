using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentWatchClosed
{
    [Key(0)] public string WatchId { get; set; } = "";
    [Key(1)] public string Reason { get; set; } = "";
}
