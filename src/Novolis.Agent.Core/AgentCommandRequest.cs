using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentCommandRequest
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public AgentCommand Command { get; set; } = new();
}
