using MessagePack;

namespace Novolis.Agent.Core;

/// <summary>Host-neutral view of one resource: identity, revision, diagnostics, and recovery.</summary>
[MessagePackObject]
public sealed class AgentObservation
{
    [Key(0)] public string ResourceId { get; set; } = "";
    [Key(1)] public long StateRevision { get; set; }
    [Key(2)] public string? Summary { get; set; }
    [Key(3)] public string? BlockedReason { get; set; }
    [Key(4)] public string? Recovery { get; set; }
    [Key(5)] public AgentDiagnostic[] Diagnostics { get; set; } = [];
    [Key(6)] public AgentArtifactRef[] Artifacts { get; set; } = [];
    [Key(7)] public string[] AvailableActionIds { get; set; } = [];
}
