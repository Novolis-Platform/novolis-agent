using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentCoreTests
{
    [Test]
    public async Task ProtocolCodec_round_trips_snapshot()
    {
        var snap = new AgentSnapshot
        {
            Day = 3,
            HubId = "sol",
            DocumentName = "scene",
            NodeCount = 2,
            StatusLines = new Dictionary<string, string>(StringComparer.Ordinal) { ["cash"] = "100" },
        };
        var bytes = AgentProtocolCodec.Serialize(snap);
        var back = AgentProtocolCodec.Deserialize<AgentSnapshot>(bytes);
        await Assert.That(back.Day).IsEqualTo(3);
        await Assert.That(back.HubId).IsEqualTo("sol");
        await Assert.That(back.DocumentName).IsEqualTo("scene");
        await Assert.That(back.Line("cash")).IsEqualTo("100");
    }

    [Test]
    public async Task MethodNames_accept_legacy_session_aliases()
    {
        await Assert.That(AgentMethodNames.IsHello("session.hello")).IsTrue();
        await Assert.That(AgentMethodNames.IsCommand("agent.command")).IsTrue();
        await Assert.That(AgentMethodNames.IsContinue("continue")).IsTrue();
    }

    [Test]
    public async Task InMemoryChannel_duplex()
    {
        await using var channel = new InMemoryAgentChannel();
        channel.EnqueueInbound(new AgentFrame(1, AgentFrameKinds.Request, AgentMethodNames.Hello, ReadOnlyMemory<byte>.Empty));
        await channel.SendAsync(new AgentFrame(1, AgentFrameKinds.Response, AgentMethodNames.Hello, new byte[] { 1, 2, 3 }));
        var outbound = await channel.ReadOutboundAsync();
        await Assert.That(outbound.Kind).IsEqualTo(AgentFrameKinds.Response);
        await Assert.That(outbound.Payload.Length).IsEqualTo(3);
    }
}

[AgentSurface("unit", HttpPort = 19885, TcpPort = 19886, Description = "unit surface")]
[AgentAction("ping", Summary = "Ping", Params = "label?")]
public interface IUnitSurface : IAgentHost;

public sealed class AgentSurfaceDefinitionTests
{
    [Test]
    public async Task From_builds_actions_and_document()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        await Assert.That(def.SurfaceId).IsEqualTo("unit");
        await Assert.That(def.Actions.Count).IsEqualTo(1);
        await Assert.That(def.Actions[0].Id).IsEqualTo("ping");

        var doc = AgentSurfaceDocument.From(def, httpPort: 19885);
        var openApi = doc.ToOpenApiJson();
        await Assert.That(openApi).Contains("/agent/hello");
        await Assert.That(openApi).Contains("/agent/ws");

        var mcp = doc.ToMcpTools();
        await Assert.That(mcp.Count).IsGreaterThanOrEqualTo(1);

        var json = doc.ToJson();
        using var parsed = JsonDocument.Parse(json);
        await Assert.That(parsed.RootElement.GetProperty("surfaceId").GetString()).IsEqualTo("unit");
    }

    [Test]
    public async Task JsonDispatcher_hello_and_command()
    {
        var host = new FakeAgentHost();
        var hello = AgentJsonDispatcher.Dispatch(host, AgentMethodNames.Hello, JsonDocument.Parse("{}").RootElement);
        await Assert.That(hello).IsTypeOf<AgentHello>();

        using var cmdDoc = JsonDocument.Parse("""{"actionId":"ping","params":{"label":"x"}}""");
        var result = AgentJsonDispatcher.Dispatch(host, "command", cmdDoc.RootElement);
        await Assert.That(result).IsTypeOf<AgentCommandResult>();
        await Assert.That(host.Executed[0].ActionId).IsEqualTo("ping");
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("x");
    }

    [Test]
    public async Task HttpHost_document_and_agent_hello()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "unit-test") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var docResp = await client.GetAsync("agent/document");
        docResp.EnsureSuccessStatusCode();
        var docJson = await docResp.Content.ReadAsStringAsync();
        await Assert.That(docJson).Contains("unit");

        using var helloResp = await client.GetAsync("agent/hello");
        helloResp.EnsureSuccessStatusCode();
        var helloBody = await helloResp.Content.ReadAsStringAsync();
        await Assert.That(helloBody).Contains("\"ok\":true");
    }
}


