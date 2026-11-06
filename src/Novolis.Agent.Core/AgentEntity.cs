using MessagePack;

namespace Novolis.Agent.Core;

/// <summary>
/// One thing a host puts on the map for an agent to read: a threat to defeat, an objective, or a terrain feature.
/// <see cref="X"/> and <see cref="Z"/> are host map meters. <see cref="Path"/> is flat x,z pairs when the entity is a line.
/// </summary>
[MessagePackObject]
public sealed class AgentEntity
{
    public const string Threat = "threat";
    public const string Objective = "objective";
    public const string River = "river";
    public const string Road = "road";

    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public string Kind { get; set; } = "";
    [Key(2)] public string Name { get; set; } = "";
    [Key(3)] public string State { get; set; } = "";
    [Key(4)] public double X { get; set; }
    [Key(5)] public double Z { get; set; }
    [Key(6)] public string Summary { get; set; } = "";
    [Key(7)] public double[] Path { get; set; } = [];
}
