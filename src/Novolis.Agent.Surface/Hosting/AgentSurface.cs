using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// One-shot attach for all agent transports so every host process shares the same surface.
/// Local-IPC (MessagePack) + HTTP REST/SSE/WebSocket (default) + optional TCP JSONL / JSON-RPC / stdio / MCP-stdio.
/// </summary>
public sealed class AgentSurface : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _hosts = new();

    private AgentSurface()
    {
    }

    public AgentSurfaceDefinition Definition { get; private set; } = null!;

    public AgentLocalIpcTransport? LocalIpc { get; private set; }

    public AgentHttpHost? Http { get; private set; }

    public AgentTcpJsonlHost? Tcp { get; private set; }

    public AgentJsonRpcHost? Rpc { get; private set; }

    public AgentMcpStdioTransport? Mcp { get; private set; }

    public AgentStdioHost? Stdio { get; private set; }

    public string? HttpBaseUrl => Http?.BaseUrl;

    public string? WebSocketUrl => Http?.WebSocketUrl;

    public int? TcpPort => Tcp?.Port;

    public int? RpcPort => Rpc?.Port;

    /// <summary>
    /// Attach transports gated by environment variables (<c>NOVOLIS_AGENT*</c> by default, or the surface's own
    /// <see cref="AgentSurfaceDefinition.EnableEnv"/> prefix). Returns <c>null</c> when nothing is enabled.
    /// </summary>
    public static AgentSurface? TryAttachFromEnvironment(
        IAgentHost host,
        AgentSurfaceDefinition definition,
        string? preferredIpcAddress = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);

        var surface = new AgentSurface { Definition = definition };
        var any = false;

        if (definition.IsIpcEnabledByEnvironment())
        {
            var ipc = AgentLocalIpcTransport.Attach(host, definition, preferredIpcAddress);
            surface.LocalIpc = ipc;
            surface._hosts.Add(ipc);
            any = true;
        }

        if (definition.IsHttpEnabledByEnvironment())
        {
            var http = AgentHttpHost.Attach(host, definition);
            surface.Http = http;
            surface._hosts.Add(http);
            any = true;
        }

        if (definition.IsTcpEnabledByEnvironment())
        {
            var tcp = AgentTcpJsonlHost.Attach(host, definition);
            surface.Tcp = tcp;
            surface._hosts.Add(tcp);
            any = true;
        }

        if (definition.IsRpcEnabledByEnvironment())
        {
            var rpc = AgentJsonRpcHost.Attach(host, definition);
            surface.Rpc = rpc;
            surface._hosts.Add(rpc);
            any = true;
        }

        if (definition.IsMcpEnabledByEnvironment())
        {
            var mcp = new AgentMcpStdioTransport(host, definition);
            _ = mcp.StartAsync();
            surface.Mcp = mcp;
            surface._hosts.Add(mcp);
            any = true;
        }

        if (definition.IsStdioEnabledByEnvironment())
        {
            var stdio = new AgentStdioHost(host);
            _ = stdio.StartAsync();
            surface.Stdio = stdio;
            surface._hosts.Add(stdio);
            any = true;
        }

        return any ? surface : null;
    }

    /// <summary>
    /// Attach transports unconditionally per <paramref name="options"/> (no env gating), so the same EXE can be
    /// controlled mid-run by any agent transport. Per-transport bind failures are swallowed.
    /// </summary>
    public static AgentSurface? AttachAll(IAgentHost host, AgentSurfaceDefinition definition, AgentAttachOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new AgentAttachOptions();

        var surface = new AgentSurface { Definition = definition };
        var any = false;

        if (options.EnableIpc)
        {
            try
            {
                var ipc = AgentLocalIpcTransport.Attach(host, definition, options.IpcAddress);
                surface.LocalIpc = ipc;
                surface._hosts.Add(ipc);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        if (options.EnableHttp)
        {
            try
            {
                var http = AgentHttpHost.Attach(host, definition, options.HttpPort);
                surface.Http = http;
                surface._hosts.Add(http);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        if (options.EnableTcp)
        {
            try
            {
                var tcp = AgentTcpJsonlHost.Attach(host, definition, options.TcpPort);
                surface.Tcp = tcp;
                surface._hosts.Add(tcp);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        if (options.EnableRpc)
        {
            try
            {
                var rpc = AgentJsonRpcHost.Attach(host, definition, options.RpcPort);
                surface.Rpc = rpc;
                surface._hosts.Add(rpc);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        if (options.EnableMcpStdio)
        {
            try
            {
                var mcp = new AgentMcpStdioTransport(host, definition);
                _ = mcp.StartAsync();
                surface.Mcp = mcp;
                surface._hosts.Add(mcp);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        if (options.EnableStdio)
        {
            try
            {
                var stdio = new AgentStdioHost(host);
                _ = stdio.StartAsync();
                surface.Stdio = stdio;
                surface._hosts.Add(stdio);
                any = true;
            }
            catch
            {
                // ignore per-transport bind failures
            }
        }

        return any ? surface : null;
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = _hosts.Count - 1; i >= 0; i--)
        {
            try { await _hosts[i].DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        _hosts.Clear();
    }
}
