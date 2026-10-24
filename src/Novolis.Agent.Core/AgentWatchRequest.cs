using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentWatchRequest
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string[] EventNames { get; set; } = [];
    [Key(2)] public string? CallbackId { get; set; }
    [Key(3)] public long? AfterEventSequence { get; set; }
    [Key(4)] public int? ReplayLimit { get; set; }
    [Key(5)] public string? CorrelationId { get; set; }
    [Key(6)] public string? ResourceId { get; set; }
    [Key(7)] public string? MinSeverity { get; set; }
}
