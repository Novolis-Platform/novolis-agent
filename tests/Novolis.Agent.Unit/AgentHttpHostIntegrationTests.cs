using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentHttpHostIntegrationTests
{
    [Test]
    public async Task HttpHost_health_continue_subscribe_and_announce()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost
        {
            HelloResponse = def.BuildHello(appId: "http-int"),
            SnapshotResponse = new AgentSnapshot { Day = 2, HubId = "hub" },
        };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var health = await client.GetAsync("health");
        health.EnsureSuccessStatusCode();
        var healthBody = await health.Content.ReadAsStringAsync();
        await Assert.That(healthBody).Contains("\"transport\":\"http\"");

        using var sessionHealth = await client.GetAsync("session/health");
        sessionHealth.EnsureSuccessStatusCode();

        using var continueResp = await client.PostAsync("agent/continue", new StringContent("{}", Encoding.UTF8, "application/json"));
        continueResp.EnsureSuccessStatusCode();
        await Assert.That(host.ContinueCount).IsEqualTo(1);

        using var subscribeResp = await client.PostAsync("agent/subscribe", new StringContent("{}", Encoding.UTF8, "application/json"));
        subscribeResp.EnsureSuccessStatusCode();
        await Assert.That(host.SubscribeCount).IsEqualTo(1);

        using var announceResp = await client.GetAsync("agent/announce");
        announceResp.EnsureSuccessStatusCode();
        var announceBody = await announceResp.Content.ReadAsStringAsync();
        await Assert.That(announceBody).Contains("http-int");
        await Assert.That(http.WebSocketUrl).Contains("/agent/ws");
    }

    [Test]
    public async Task HttpHost_openapi_mcp_rpc_and_options()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "http-docs") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var openapi = await client.GetAsync("agent/openapi");
        openapi.EnsureSuccessStatusCode();
        await Assert.That(await openapi.Content.ReadAsStringAsync()).Contains("paths");

        using var openapiJson = await client.GetAsync("agent/openapi.json");
        openapiJson.EnsureSuccessStatusCode();
        await Assert.That(await openapiJson.Content.ReadAsStringAsync()).Contains("paths");

        using var mcp = await client.GetAsync("agent/mcp/tools");
        mcp.EnsureSuccessStatusCode();

        using var rpcMethods = await client.GetAsync("agent/rpc/methods");
        rpcMethods.EnsureSuccessStatusCode();

        using var rpc = await client.PostAsJsonAsync(
            "agent/rpc",
            new { method = AgentMethodNames.Snapshot, id = 1 });
        rpc.EnsureSuccessStatusCode();
        var rpcBody = await rpc.Content.ReadAsStringAsync();
        await Assert.That(rpcBody).Contains("\"ok\":true");

        using var options = new HttpRequestMessage(HttpMethod.Options, "agent/hello");
        using var optionsResp = await client.SendAsync(options);
        await Assert.That((int)optionsResp.StatusCode).IsEqualTo(204);
        await Assert.That(optionsResp.Headers.Contains("Access-Control-Allow-Origin")).IsTrue();
    }

    [Test]
    public async Task HttpHost_unknown_route_returns_404()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var missing = await client.GetAsync("agent/missing-endpoint");
        await Assert.That((int)missing.StatusCode).IsEqualTo(404);
        await Assert.That(await missing.Content.ReadAsStringAsync()).Contains("not found");
    }

    [Test]
    public async Task HttpHost_websocket_hello_round_trip()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "ws-hello") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);

        var request = """{"sequence":1,"method":"hello","payload":{}}""";
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await ws.SendAsync(requestBytes, WebSocketMessageType.Text, true, cts.Token);

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        await Assert.That(text).Contains("ws-hello");
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
    }

    [Test]
    public async Task HttpHost_websocket_receives_changed_event()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 7, HubId = "ws" } };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);

        host.RaiseChanged("ws-broadcast");
        var buffer = new byte[8192];
        string? payload = null;
        using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 10; i++)
        {
            var result = await ws.ReceiveAsync(buffer, receiveCts.Token);
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (text.Contains("ws-broadcast", StringComparison.Ordinal))
            {
                payload = text;
                break;
            }
        }

        await Assert.That(payload).IsNotNull();
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
    }
}



