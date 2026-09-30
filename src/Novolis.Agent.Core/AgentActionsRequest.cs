using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentActionsRequest
{
    [Key(0)] public long Sequence { get; set; }
}
