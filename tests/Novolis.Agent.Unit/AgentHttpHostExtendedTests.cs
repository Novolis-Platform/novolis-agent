using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentHttpHostExtendedTests
{
    [Test]
    public async Task HttpHost_snapshot_actions_and_command()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost
        {
            SnapshotResponse = new AgentSnapshot { Day = 9, HubId = "alpha" },
            HelloResponse = def.BuildHello(appId: "unit-test"),
        };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var snapResp = await client.GetAsync("agent/snapshot");
        snapResp.EnsureSuccessStatusCode();
        var snapJson = await snapResp.Content.ReadAsStringAsync();
        await Assert.That(snapJson).Contains("\"day\":9");

        using var actionsResp = await client.GetAsync("agent/actions");
        actionsResp.EnsureSuccessStatusCode();

        using var cmdResp = await client.PostAsync(
            "agent/command",
            new StringContent("""{"actionId":"ping","params":{"label":"hi"}}""", Encoding.UTF8, "application/json"));
        cmdResp.EnsureSuccessStatusCode();
        var cmdBody = await cmdResp.Content.ReadAsStringAsync();
        await Assert.That(cmdBody).Contains("\"ok\":true");
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("hi");
    }

    [Test]
    public async Task SurfaceDocument_rpc_methods_and_openapi()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var doc = AgentSurfaceDocument.From(def, httpPort: 8080, tcpPort: 8081, rpcPort: 8082);
        var rpc = doc.ToRpcMethods();
        await Assert.That(rpc.Count).IsGreaterThanOrEqualTo(6);
        await Assert.That(rpc.Any(m => m.Method == AgentMethodNames.Hello)).IsTrue();

        var openapi = doc.ToOpenApiObject();
        await Assert.That(openapi.ContainsKey("paths")).IsTrue();
    }

    [Test]
    public async Task InMemoryChannel_read_frames_async()
    {
        await using var channel = new InMemoryAgentChannel();
        channel.EnqueueInbound(new AgentFrame(1, AgentFrameKinds.Request, AgentMethodNames.Hello, ReadOnlyMemory<byte>.Empty));
        channel.EnqueueInbound(new AgentFrame(2, AgentFrameKinds.Request, AgentMethodNames.Snapshot, ReadOnlyMemory<byte>.Empty));

        var frames = new List<AgentFrame>();
        await foreach (var frame in channel.ReadFramesAsync())
        {
            frames.Add(frame);
            if (frames.Count == 2)
                break;
        }

        await Assert.That(frames.Count).IsEqualTo(2);
        await Assert.That(frames[0].Method).IsEqualTo(AgentMethodNames.Hello);
        await Assert.That(channel.TransportKind).IsEqualTo("in-memory");
    }
}



