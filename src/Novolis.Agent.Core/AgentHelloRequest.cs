using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentHelloRequest
{
    [Key(0)] public long Sequence { get; set; }
}
