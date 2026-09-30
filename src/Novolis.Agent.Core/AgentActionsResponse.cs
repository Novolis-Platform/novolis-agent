using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentActionsResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public AgentAction[] Actions { get; set; } = [];
}
