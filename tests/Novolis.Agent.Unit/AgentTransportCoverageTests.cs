using System.Reflection;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Unit;

[AgentSurface("methods", HttpPort = 19887, TcpPort = 19888)]
public interface IMethodSurface : IAgentHost
{
    [AgentMethod("custom.hello")]
    void CustomHello();
}

[NotInParallel("agent-env")]
public sealed class AgentTransportCoverageTests
{
    [Test]
    public async Task SurfaceDefinition_reads_AgentMethodAttribute()
    {
        var def = AgentSurfaceDefinition.From<IMethodSurface>();
        await Assert.That(def.Methods).Contains("custom.hello");
        var attr = typeof(IMethodSurface).GetMethod(nameof(IMethodSurface.CustomHello))!
            .GetCustomAttribute<AgentMethodAttribute>();
        await Assert.That(attr!.Method).IsEqualTo("custom.hello");
    }

    [Test]
    public async Task HttpHost_StartAsync_is_noop()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        await using var http = AgentHttpHost.Attach(host, def, AgentTestPorts.GetFreePort());
        await http.StartAsync();
    }

    [Test]
    public async Task TcpJsonlHost_TryAttachFromEnvironment_and_action_result()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        Environment.SetEnvironmentVariable(def.TcpEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.TcpPortEnv, port.ToString());
        try
        {
            await using var attached = AgentTcpJsonlHost.TryAttachFromEnvironment(host, def);
            await Assert.That(attached).IsNotNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.TcpEnableEnv, null);
            Environment.SetEnvironmentVariable(def.TcpPortEnv, null);
        }
    }

    [Test]
    public Task LocalIpcHost_unknown_method_returns_fault() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost();
            var pipeName = $"novolis-agent-fault-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            var transport = LocalIpcTransport.CreateClient();
            await using var connection = await AgentIpcTestHelper.ConnectConnectionAsync(endpoint);
            await connection.SendMessageAsync(
                42,
                AgentFrameKinds.Request,
                "bad.method",
                new AgentHelloRequest { Sequence = 42 },
                CancellationToken.None);

            LocalIpcFrame? fault = null;
            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var frame in connection.ReadAllAsync(readCts.Token))
            {
                if (frame.Sequence == 42 && frame.Kind == AgentFrameKinds.Fault)
                {
                    fault = frame;
                    break;
                }
            }

            await Assert.That(fault).IsNotNull();
            var message = AgentProtocolCodec.Deserialize<AgentFault>(fault!.Payload).Message;
            await Assert.That(message).Contains("Unknown method");
        });

    [Test]
    public Task LocalIpcTransport_TryAttachFromEnvironment_respects_flag() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost();
            Environment.SetEnvironmentVariable(def.IpcEnableEnv, "1");
            try
            {
                await using var attached = AgentLocalIpcTransport.TryAttachFromEnvironment(host, def, $"novolis-test-{Guid.NewGuid():N}");
                await Assert.That(attached).IsNotNull();
                Environment.SetEnvironmentVariable(def.IpcEnableEnv, "0");
                await using var disabled = AgentLocalIpcTransport.TryAttachFromEnvironment(host, def);
                await Assert.That(disabled).IsNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(def.IpcEnableEnv, null);
            }
        });
}



