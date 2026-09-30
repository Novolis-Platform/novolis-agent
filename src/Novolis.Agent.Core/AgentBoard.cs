using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentBoard
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public AgentBoardItem[] Items { get; set; } = [];
}
