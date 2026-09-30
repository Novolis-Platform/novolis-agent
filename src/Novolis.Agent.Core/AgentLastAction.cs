using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentLastAction
{
    [Key(0)] public string ActionId { get; set; } = "";
    [Key(1)] public bool Ok { get; set; }
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public string? ErrorCode { get; set; }
}
