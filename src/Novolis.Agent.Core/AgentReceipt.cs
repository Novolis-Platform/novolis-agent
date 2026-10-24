using MessagePack;

namespace Novolis.Agent.Core;

/// <summary>Structured outcome for one correlated action. Transport success alone is not enough.</summary>
[MessagePackObject]
public sealed class AgentReceipt
{
    [Key(0)] public string CorrelationId { get; set; } = "";
    [Key(1)] public string ActionId { get; set; } = "";
    [Key(2)] public string Lifecycle { get; set; } = "";
    [Key(3)] public bool Ok { get; set; }
    [Key(4)] public string? ReasonCode { get; set; }
    [Key(5)] public string Message { get; set; } = "";
    [Key(6)] public long? StateRevision { get; set; }
    [Key(7)] public string? Preview { get; set; }
    [Key(8)] public string? Recovery { get; set; }
    [Key(9)] public AgentEffect[] Effects { get; set; } = [];
    [Key(10)] public AgentArtifactRef[] Artifacts { get; set; } = [];
    [Key(11)] public string MetadataVersion { get; set; } = AgentMetadata.Version;
}
