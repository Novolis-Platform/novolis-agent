using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentWatchResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string WatchId { get; set; } = "";
    [Key(2)] public string? CallbackId { get; set; }
    [Key(3)] public long EstablishedThroughSequence { get; set; }
    [Key(4)] public long? OldestAvailableSequence { get; set; }
    [Key(5)] public bool ReplayTruncated { get; set; }
    [Key(6)] public bool Ok { get; set; } = true;
    [Key(7)] public string? Error { get; set; }
}
