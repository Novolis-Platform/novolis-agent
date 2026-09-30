using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentFault
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string Message { get; set; } = "";
}
