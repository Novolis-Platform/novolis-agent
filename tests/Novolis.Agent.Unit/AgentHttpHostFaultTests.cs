using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentHttpHostFaultTests
{
    [Test]
    public async Task HttpHost_websocket_invalid_json_returns_fault()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);

        var bad = Encoding.UTF8.GetBytes("{not-json");
        await ws.SendAsync(bad, WebSocketMessageType.Text, true, cts.Token);

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        await Assert.That(text).Contains("\"kind\":\"fault\"");
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
    }

    [Test]
    public async Task HttpHost_command_invalid_body_returns_500()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var resp = await client.PostAsync(
            "agent/command",
            new StringContent("{bad-json", Encoding.UTF8, "application/json"));
        await Assert.That((int)resp.StatusCode).IsEqualTo(500);
        var body = await resp.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"ok\":false");
    }

    [Test]
    public async Task HttpHost_session_path_aliases_map_to_agent()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "alias") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var hello = await client.GetAsync("session/hello");
        hello.EnsureSuccessStatusCode();
        await Assert.That(await hello.Content.ReadAsStringAsync()).Contains("alias");
    }
}



