using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentDiagnostic
{
    [Key(0)] public string Severity { get; set; } = AgentSeverity.Info;
    [Key(1)] public string Code { get; set; } = "";
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public string? ResourceId { get; set; }
}
