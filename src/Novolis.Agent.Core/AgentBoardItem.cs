using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentBoardItem
{
    [Key(0)] public int Index { get; set; }
    [Key(1)] public string Id { get; set; } = "";
    [Key(2)] public string Label { get; set; } = "";
    [Key(3)] public string Detail { get; set; } = "";
    [Key(4)] public bool CanAct { get; set; }
}
