namespace Novolis.Agent.Surface;

/// <summary>Explicit transport selection and port/address overrides for <see cref="AgentSurface.AttachAll"/>.</summary>
public sealed class AgentAttachOptions
{
    public string? IpcAddress { get; set; }

    public int? HttpPort { get; set; }

    public int? TcpPort { get; set; }

    public int? RpcPort { get; set; }

    public bool EnableIpc { get; set; } = true;

    public bool EnableHttp { get; set; } = true;

    public bool EnableTcp { get; set; } = true;

    public bool EnableRpc { get; set; } = true;

    public bool EnableMcpStdio { get; set; }

    public bool EnableStdio { get; set; }
}
