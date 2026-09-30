using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentSubscribeResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; } = true;
}
