using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentArtifactRef
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public string Kind { get; set; } = "";
    [Key(2)] public string? MediaType { get; set; }
    [Key(3)] public string? Uri { get; set; }
    [Key(4)] public string? Summary { get; set; }
}
