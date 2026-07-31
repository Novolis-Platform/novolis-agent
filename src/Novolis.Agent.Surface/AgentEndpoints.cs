using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Surface;

/// <summary>Default pipe name, ports, and <c>NOVOLIS_AGENT*</c> environment variable names.</summary>
public static class AgentEndpoints
{
    public const string DefaultPipeName = "novolis-agent";

    public const string EnableEnvVar = "NOVOLIS_AGENT";
    public const string EndpointEnvVar = "NOVOLIS_AGENT_ENDPOINT";
    public const string HttpEnableEnvVar = "NOVOLIS_AGENT_HTTP";
    public const string HttpPortEnvVar = "NOVOLIS_AGENT_HTTP_PORT";
    public const string TcpEnableEnvVar = "NOVOLIS_AGENT_TCP";
    public const string TcpPortEnvVar = "NOVOLIS_AGENT_TCP_PORT";
    public const string RpcEnableEnvVar = "NOVOLIS_AGENT_RPC";
    public const string RpcPortEnvVar = "NOVOLIS_AGENT_RPC_PORT";
    public const string McpEnableEnvVar = "NOVOLIS_AGENT_MCP";
    public const string StdioEnableEnvVar = "NOVOLIS_AGENT_STDIO";

    public const string HostMarkerFileName = "novolis-agent.host";
    public const string HttpMarkerFileName = "novolis-agent.http";
    public const string WsMarkerFileName = "novolis-agent.ws";
    public const string IpcMarkerFileName = "novolis-agent.ipc";
    public const string TcpMarkerFileName = "novolis-agent.tcp";
    public const string RpcMarkerFileName = "novolis-agent.rpc";
    public const string McpMarkerFileName = "novolis-agent.mcp";

    public const int DefaultHttpPort = 18765;
    public const int DefaultTcpPort = 18766;
    public const int DefaultRpcPort = 18767;

    public static string MarkerPath(string fileName) => Path.Combine(Path.GetTempPath(), fileName);

    /// <summary>Resolves a <see cref="LocalIpcEndpoint"/> for a surface, honoring its endpoint override env var.</summary>
    public static LocalIpcEndpoint CreateIpcEndpoint(AgentSurfaceDefinition definition, string? preferredAddress = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var overrideAddress = Environment.GetEnvironmentVariable(definition.IpcAddressEnv);
        if (string.IsNullOrWhiteSpace(overrideAddress))
            overrideAddress = Environment.GetEnvironmentVariable(EndpointEnvVar);

        var address = !string.IsNullOrWhiteSpace(overrideAddress)
            ? overrideAddress
            : preferredAddress ?? DefaultPipeName;

        if (OperatingSystem.IsWindows())
            return new LocalIpcEndpoint(address, LocalIpcTransportKind.NamedPipe);

        var socketPath = Path.IsPathRooted(address)
            ? address
            : Path.Combine(Path.GetTempPath(), address + ".sock");
        return new LocalIpcEndpoint(socketPath, LocalIpcTransportKind.UnixDomainSocket);
    }
}
