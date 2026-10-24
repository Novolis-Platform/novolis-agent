using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentEffect
{
    [Key(0)] public string ResourceId { get; set; } = "";
    [Key(1)] public string Kind { get; set; } = "";
    [Key(2)] public string? Before { get; set; }
    [Key(3)] public string? After { get; set; }
}
