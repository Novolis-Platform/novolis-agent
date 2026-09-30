using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentSubscribeRequest
{
    [Key(0)] public long Sequence { get; set; }
}
