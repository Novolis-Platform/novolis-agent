using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentWatchEvent
{
    [Key(0)] public string WatchId { get; set; } = "";
    [Key(1)] public string? CallbackId { get; set; }
    [Key(2)] public long EventSequence { get; set; }
    [Key(3)] public string EventName { get; set; } = "";
    [Key(4)] public DateTimeOffset OccurredUtc { get; set; }
    [Key(5)] public bool IsReplay { get; set; }
    [Key(6)] public string? CorrelationId { get; set; }
    [Key(7)] public string? ResourceId { get; set; }
    [Key(8)] public string? Severity { get; set; }
    [Key(9)] public string PayloadJson { get; set; } = "";
}
