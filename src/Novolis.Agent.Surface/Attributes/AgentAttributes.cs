namespace Novolis.Agent.Surface;

/// <summary>Marks an interface (or class) as an agent surface, with transport defaults.</summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public sealed class AgentSurfaceAttribute : Attribute
{
    public AgentSurfaceAttribute(string surfaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        SurfaceId = surfaceId;
    }

    public string SurfaceId { get; }

    public int HttpPort { get; set; } = 18765;

    public int TcpPort { get; set; } = 18766;

    public string EnableEnv { get; set; } = "NOVOLIS_AGENT";

    public string MarkerPrefix { get; set; } = "novolis-agent";

    public string ProtocolVersion { get; set; } = "1.0";

    public string? Description { get; set; }
}
