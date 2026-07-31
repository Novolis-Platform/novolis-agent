using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>Lightweight, serializable presence/discovery record for a running agent surface.</summary>
public sealed record AgentAnnouncement(
    string SurfaceId,
    string ProtocolVersion,
    string AppId,
    string AppTitle,
    int ProcessId,
    string[] Capabilities,
    string? Description,
    int? HttpPort,
    int? TcpPort,
    string? DocumentUrl,
    string? WebSocketUrl)
{
    public static AgentAnnouncement From(AgentHello hello)
    {
        ArgumentNullException.ThrowIfNull(hello);
        return new AgentAnnouncement(
            hello.SurfaceId,
            hello.ProtocolVersion,
            hello.AppId,
            hello.AppTitle,
            hello.ProcessId,
            hello.Capabilities,
            hello.Description,
            hello.HttpPort,
            hello.TcpPort,
            hello.DocumentUrl,
            hello.WebSocketUrl);
    }
}
