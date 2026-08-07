using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

[NotInParallel("agent-env")]
public sealed class AgentSurfaceAttachTests
{
    [Test]
    public async Task TryAttachFromEnvironment_enables_http_tcp_rpc()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "attach-env") };

        Environment.SetEnvironmentVariable(def.EnableEnv, null);
        Environment.SetEnvironmentVariable(def.IpcEnableEnv, "0");
        Environment.SetEnvironmentVariable(def.HttpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.TcpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.RpcEnableEnv, "1");
        var httpPort = AgentTestPorts.GetFreePort();
        var tcpPort = AgentTestPorts.GetFreePort();
        var rpcPort = AgentTestPorts.GetFreePort();
        Environment.SetEnvironmentVariable(def.HttpPortEnv, httpPort.ToString());
        Environment.SetEnvironmentVariable(def.TcpPortEnv, tcpPort.ToString());
        Environment.SetEnvironmentVariable(def.RpcPortEnv, rpcPort.ToString());

        try
        {
            await Assert.That(def.IsTcpEnabledByEnvironment()).IsTrue();
            await Assert.That(def.IsRpcEnabledByEnvironment()).IsTrue();

            await using var surface = AgentSurface.TryAttachFromEnvironment(host, def);
            await Assert.That(surface).IsNotNull();
            await Assert.That(surface!.Http).IsNotNull();
            await Assert.That(surface.Tcp).IsNotNull();
            await Assert.That(surface.Rpc).IsNotNull();
            await Assert.That(surface.HttpBaseUrl).Contains(httpPort.ToString());
            await Assert.That(surface.TcpPort).IsEqualTo(def.ResolveTcpPort());
            await Assert.That(surface.RpcPort).IsEqualTo(def.ResolveRpcPort());
            await Assert.That(def.ResolveTcpPort()).IsEqualTo(tcpPort);
            await Assert.That(def.ResolveRpcPort()).IsEqualTo(rpcPort);
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.EnableEnv, null);
            Environment.SetEnvironmentVariable(def.IpcEnableEnv, null);
            Environment.SetEnvironmentVariable(def.HttpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.TcpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.RpcEnableEnv, null);
            Environment.SetEnvironmentVariable(def.HttpPortEnv, null);
            Environment.SetEnvironmentVariable(def.TcpPortEnv, null);
            Environment.SetEnvironmentVariable(def.RpcPortEnv, null);
        }
    }

    [Test]
    public async Task TryAttachFromEnvironment_enables_mcp_and_stdio()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        Environment.SetEnvironmentVariable(def.EnableEnv, "1");
        Environment.SetEnvironmentVariable(def.McpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.StdioEnableEnv, "1");

        try
        {
            // MCP/stdio attach uses Console.In by default; verify lifecycle with in-memory streams instead.
            await using var mcp = new AgentMcpStdioTransport(host, def, new StringReader(""), new StringWriter());
            await mcp.StartAsync();
            await using var stdio = new AgentStdioHost(host, new StringReader(""), new StringWriter());
            await stdio.StartAsync();
            await Assert.That(mcp.Kind).IsEqualTo("mcp-stdio");
            await Assert.That(stdio.Kind).IsEqualTo("stdio-jsonl");
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.EnableEnv, null);
            Environment.SetEnvironmentVariable(def.McpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.StdioEnableEnv, null);
        }
    }

    [Test]
    public async Task Transport_TryAttachFromEnvironment_individual_hosts()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        Environment.SetEnvironmentVariable(def.HttpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.HttpPortEnv, AgentTestPorts.GetFreePort().ToString());
        try
        {
            await using var http = AgentHttpHost.TryAttachFromEnvironment(host, def);
            await Assert.That(http).IsNotNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.HttpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.HttpPortEnv, null);
        }
    }

    [Test]
    public async Task AttachAll_binds_loopback_transports()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "attach-all") };
        var options = new AgentAttachOptions
        {
            EnableHttp = true,
            EnableTcp = true,
            EnableRpc = true,
            HttpPort = AgentTestPorts.GetFreePort(),
            TcpPort = AgentTestPorts.GetFreePort(),
            RpcPort = AgentTestPorts.GetFreePort(),
        };

        await using var surface = AgentSurface.AttachAll(host, def, options);
        await Assert.That(surface).IsNotNull();
        await Assert.That(surface!.Http).IsNotNull();
        await Assert.That(surface.Tcp).IsNotNull();
        await Assert.That(surface.Rpc).IsNotNull();

        await surface.Http!.StopAsync();
        await surface.Tcp!.StopAsync();
        await surface.Rpc!.StopAsync();
    }

    [Test]
    public async Task AttachAll_includes_ipc_on_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var pipe = $"novolis-agent-all-{Guid.NewGuid():N}";
        var options = new AgentAttachOptions
        {
            EnableIpc = true,
            IpcAddress = pipe,
            EnableHttp = true,
            HttpPort = AgentTestPorts.GetFreePort(),
        };

        await using var surface = AgentSurface.AttachAll(host, def, options);
        await Assert.That(surface).IsNotNull();
        await Assert.That(surface!.LocalIpc).IsNotNull();
        await Assert.That(surface.Http).IsNotNull();
    }

    [Test]
    public async Task AttachAll_enables_mcp_and_stdio_transports()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        await using var mcp = new AgentMcpStdioTransport(host, def, new StringReader(""), new StringWriter());
        await mcp.StartAsync();
        await using var stdio = new AgentStdioHost(host, new StringReader(""), new StringWriter());
        await stdio.StartAsync();
        await Assert.That(mcp.Kind).IsEqualTo("mcp-stdio");
        await Assert.That(stdio.Kind).IsEqualTo("stdio-jsonl");
    }

    [Test]
    public async Task Transport_TryAttachFromEnvironment_respects_flags()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        Environment.SetEnvironmentVariable(def.TcpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.TcpPortEnv, AgentTestPorts.GetFreePort().ToString());

        try
        {
            var tcp = AgentTcpJsonlHost.TryAttachFromEnvironment(host, def);
            await Assert.That(tcp).IsNotNull();
            await tcp!.DisposeAsync();

            Environment.SetEnvironmentVariable(def.TcpEnableEnv, "0");
            await Assert.That(AgentTcpJsonlHost.TryAttachFromEnvironment(host, def)).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.TcpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.TcpPortEnv, null);
        }
    }
}



