using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Unit;

public sealed class AgentTransportShutdownFaultTests
{
    [Test]
    public async Task HttpHost_StopAsync_completes_cleanly()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
    }

    [Test]
    public async Task HttpHost_sse_dead_client_pruned_on_broadcast()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 1, HubId = "sse-dead" } };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(10) };

        using var liveCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var liveReq = new HttpRequestMessage(HttpMethod.Get, "agent/events");
        using var liveResp = await client.SendAsync(liveReq, HttpCompletionOption.ResponseHeadersRead, liveCts.Token);
        await using var liveStream = await liveResp.Content.ReadAsStreamAsync(liveCts.Token);
        using var liveReader = new StreamReader(liveStream, Encoding.UTF8);
        await liveReader.ReadLineAsync(liveCts.Token);

        using var deadReq = new HttpRequestMessage(HttpMethod.Get, "agent/events");
        using var deadResp = await client.SendAsync(deadReq, HttpCompletionOption.ResponseHeadersRead, liveCts.Token);
        await using var deadStream = await deadResp.Content.ReadAsStreamAsync(liveCts.Token);
        using var deadReader = new StreamReader(deadStream, Encoding.UTF8);
        await deadReader.ReadLineAsync(liveCts.Token);
        deadStream.Close();

        host.RaiseChanged("after-dead-sse");
        host.RaiseActionResult("ping", ok: true);

        var buffer = new StringBuilder();
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            while (!readCts.Token.IsCancellationRequested)
            {
                var line = await liveReader.ReadLineAsync(readCts.Token);
                if (line is null)
                    break;
                buffer.AppendLine(line);
                if (buffer.ToString().Contains("after-dead-sse", StringComparison.Ordinal))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // timed out waiting for broadcast
        }

        await Assert.That(buffer.ToString()).Contains("after-dead-sse");
    }

    [Test]
    public async Task HttpHost_websocket_binary_skipped_close_and_dead_broadcast()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "ws-fault") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);

        var binary = new byte[] { 0x01, 0x02 };
        await ws.SendAsync(binary, WebSocketMessageType.Binary, true, cts.Token);

        var hello = Encoding.UTF8.GetBytes("""{"sequence":2,"method":"hello","payload":{}}""");
        await ws.SendAsync(hello, WebSocketMessageType.Text, true, cts.Token);
        var buffer = new byte[8192];
        var helloResult = await ws.ReceiveAsync(buffer, cts.Token);
        var helloText = Encoding.UTF8.GetString(buffer, 0, helloResult.Count);
        await Assert.That(helloText).Contains("ws-fault");

        host.RaiseDecision("ws-event");
        var eventResult = await ws.ReceiveAsync(buffer, cts.Token);
        var eventText = Encoding.UTF8.GetString(buffer, 0, eventResult.Count);
        await Assert.That(eventText).Contains("ws-event");

        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
    }

    [Test]
    public async Task HttpHost_websocket_closed_socket_skips_send()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

        host.RaiseChanged("after-ws-close");
        await Task.Delay(100);
    }

    [Test]
    public async Task JsonRpcHost_invalid_json_with_id_returns_error()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var rpcHost = AgentJsonRpcHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("{not-json");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.TryGetProperty("error", out _)).IsTrue();
        await Assert.That(doc.RootElement.GetProperty("id").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task JsonRpcHost_dead_client_removed_and_action_result_broadcast()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var rpcHost = AgentJsonRpcHost.Attach(host, def, port);

        using var dead = new TcpClient();
        await dead.ConnectAsync(IPAddress.Loopback, port);
        dead.Close();

        using var live = new TcpClient();
        await live.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = live.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"subscribe","id":1}""");
        await reader.ReadLineAsync();

        host.RaiseActionResult("rpc-action", ok: true);
        var note = await reader.ReadLineAsync();
        using var noteDoc = JsonDocument.Parse(note!);
        await Assert.That(noteDoc.RootElement.GetProperty("method").GetString()).IsEqualTo(AgentMethodNames.ActionResult);
    }

    [Test]
    public async Task JsonRpcHost_client_disconnect_mid_session()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var rpcHost = AgentJsonRpcHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        client.Close();
        await Task.Delay(100);
    }

    [Test]
    public async Task TcpJsonlHost_invalid_json_line_returns_error()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("{bad-json");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task TcpJsonlHost_dead_subscriber_and_action_result()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var dead = new TcpClient();
        await dead.ConnectAsync(IPAddress.Loopback, port);
        dead.Close();

        using var live = new TcpClient();
        await live.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = live.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"method":"subscribe","id":1}""");
        await reader.ReadLineAsync();

        host.RaiseActionResult("tcp-action", ok: false);
        var evtLine = await reader.ReadLineAsync();
        using var evtDoc = JsonDocument.Parse(evtLine!);
        await Assert.That(evtDoc.RootElement.GetProperty("eventName").GetString()).IsEqualTo(AgentMethodNames.ActionResult);
    }

    [Test]
    public async Task TcpJsonlHost_shutdown_after_client_disconnect()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        client.Close();
        await Task.Delay(100);
    }

    [Test]
    public Task LocalIpcTransport_shutdown_with_subscriber() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost();
            var pipeName = $"novolis-agent-shutdown-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);
            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            await using var client = await AgentIpcTestHelper.ConnectAsync(endpoint);
            await client.SubscribeAsync();
        });

    [Test]
    public Task LocalIpcTransport_ignores_non_request_frames() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var pipeName = $"novolis-agent-event-{Guid.NewGuid():N}";
        await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);

        var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
        var transport = LocalIpcTransport.CreateClient();
        await using var connection = await AgentIpcTestHelper.ConnectConnectionAsync(endpoint);
        await connection.SendAsync(
            new LocalIpcFrame(99, AgentFrameKinds.Event, AgentMethodNames.Changed, []),
            CancellationToken.None);

        await connection.SendMessageAsync(
            100,
            AgentFrameKinds.Request,
            AgentMethodNames.Hello,
            new AgentHelloRequest { Sequence = 100 },
            CancellationToken.None);

        LocalIpcFrame? response = null;
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var frame in connection.ReadAllAsync(readCts.Token))
        {
            if (frame.Sequence == 100)
            {
                response = frame;
                break;
            }
        }

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.Kind).IsEqualTo(AgentFrameKinds.Response);
        });

    [Test]
    public Task LocalIpcTransport_dead_subscriber_pruned_on_broadcast() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var pipeName = $"novolis-agent-prune-{Guid.NewGuid():N}";
        await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);

        var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
        {
            await using var dead = await AgentIpcTestHelper.ConnectAsync(endpoint);
            await dead.SubscribeAsync();
        }

        await using var live = await AgentIpcTestHelper.ConnectAsync(endpoint);
        AgentChangedEvent? received = null;
        using var done = new ManualResetEventSlim(false);
        live.EventReceived += (_, payload) =>
        {
            received = AgentProtocolCodec.Deserialize<AgentChangedEvent>(payload);
            done.Set();
        };

        await live.SubscribeAsync();
        host.RaiseChanged("ipc-prune");
        await Assert.That(done.Wait(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(received!.Reason).IsEqualTo("ipc-prune");
        });
}



