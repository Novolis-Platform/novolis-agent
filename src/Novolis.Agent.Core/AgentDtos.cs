using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentHello
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string ProtocolVersion { get; set; } = "1.0";
    [Key(2)] public string AppId { get; set; } = "";
    [Key(3)] public string AppTitle { get; set; } = "";
    [Key(4)] public int ProcessId { get; set; }
    [Key(5)] public string[] Capabilities { get; set; } = [];
    [Key(6)] public string SurfaceId { get; set; } = "";
    [Key(7)] public string? Description { get; set; }
    [Key(8)] public int? HttpPort { get; set; }
    [Key(9)] public int? TcpPort { get; set; }
    [Key(10)] public string? DocumentUrl { get; set; }
    [Key(11)] public string? WebSocketUrl { get; set; }
}
