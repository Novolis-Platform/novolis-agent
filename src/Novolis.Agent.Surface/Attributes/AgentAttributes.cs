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

/// <summary>Maps a method used on the wire (<c>agent.hello</c>, ...) to a member for capability discovery.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentMethodAttribute : Attribute
{
    public AgentMethodAttribute(string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Method = method;
    }

    public string Method { get; }
}

/// <summary>Declares a command action available via <c>agent.actions</c> / <c>agent.command</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AgentActionAttribute : Attribute
{
    public AgentActionAttribute(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ActionId = actionId;
    }

    public string ActionId { get; }

    public string Summary { get; set; } = "";

    /// <summary>Compact param hint, e.g. <c>lightKind|omni,spot; intensity?</c>.</summary>
    public string Params { get; set; } = "";

    public bool EnabledByDefault { get; set; } = true;
}
