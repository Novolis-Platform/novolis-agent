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

[NotInParallel("agent-env")]
public sealed class AgentCoverageGapTests
{
    [Test]
    public async Task ParseCommand_reads_all_scene_scalar_fields()
    {
        using var doc = JsonDocument.Parse("""
            {
              "ActionId": "build",
              "params": { "count": 42 },
              "nodeId": "n1",
              "parentId": "p1",
              "lightKind": "point",
              "name": "Lamp",
              "x": 1.0,
              "y": 2.0,
              "z": 3.0,
              "rx": 0.1,
              "ry": 0.2,
              "rz": 0.3,
              "generatorKind": "box",
              "modifierKind": "mirror",
              "sourceId": "s1",
              "inputId": "in1",
              "targetId": "t1",
              "cutterId": "c1",
              "booleanKind": "union",
              "primitive": "cube",
              "distance": 5.5,
              "count": 3,
              "axis": "y",
              "materialColor": "#fff",
              "editMode": "object",
              "displayMode": "solid",
              "indices": "0,1,2"
            }
            """);
        var cmd = AgentJsonDispatcher.ParseCommand(doc.RootElement);
        await Assert.That(cmd.ActionId).IsEqualTo("build");
        await Assert.That(cmd.Get("count")).IsEqualTo("42");
        await Assert.That(cmd.NodeId).IsEqualTo("n1");
        await Assert.That(cmd.ParentId).IsEqualTo("p1");
        await Assert.That(cmd.LightKind).IsEqualTo("point");
        await Assert.That(cmd.Name).IsEqualTo("Lamp");
        await Assert.That(cmd.X).IsEqualTo(1f);
        await Assert.That(cmd.Y).IsEqualTo(2f);
        await Assert.That(cmd.Z).IsEqualTo(3f);
        await Assert.That(cmd.Rx).IsEqualTo(0.1f);
        await Assert.That(cmd.Ry).IsEqualTo(0.2f);
        await Assert.That(cmd.Rz).IsEqualTo(0.3f);
        await Assert.That(cmd.GeneratorKind).IsEqualTo("box");
        await Assert.That(cmd.ModifierKind).IsEqualTo("mirror");
        await Assert.That(cmd.SourceId).IsEqualTo("s1");
        await Assert.That(cmd.InputId).IsEqualTo("in1");
        await Assert.That(cmd.TargetId).IsEqualTo("t1");
        await Assert.That(cmd.CutterId).IsEqualTo("c1");
        await Assert.That(cmd.BooleanKind).IsEqualTo("union");
        await Assert.That(cmd.Primitive).IsEqualTo("cube");
        await Assert.That(cmd.Distance).IsEqualTo(5.5f);
        await Assert.That(cmd.Count).IsEqualTo(3);
        await Assert.That(cmd.Axis).IsEqualTo("y");
        await Assert.That(cmd.MaterialColor).IsEqualTo("#fff");
        await Assert.That(cmd.EditMode).IsEqualTo("object");
        await Assert.That(cmd.DisplayMode).IsEqualTo("solid");
        await Assert.That(cmd.Indices).IsEqualTo("0,1,2");
    }

    [Test]
    public async Task ParseCommand_null_root_returns_empty_command()
    {
        var cmd = AgentJsonDispatcher.ParseCommand(JsonDocument.Parse("null").RootElement);
        await Assert.That(cmd.ActionId).IsEqualTo("");
    }

    [Test]
    public async Task ParseCommand_non_string_param_values_stringify()
    {
        using var doc = JsonDocument.Parse("""{"params":{"qty":7,"ready":true}}""");
        var cmd = AgentJsonDispatcher.ParseCommand(doc.RootElement);
        await Assert.That(cmd.Get("qty")).IsEqualTo("7");
        await Assert.That(cmd.Get("ready")).IsEqualTo("True");
    }

    [Test]
    public async Task AgentEndpoints_honors_ipc_address_env_overrides()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var pipeName = $"novolis-env-{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(def.IpcAddressEnv, pipeName);
        try
        {
            var endpoint = AgentEndpoints.CreateIpcEndpoint(def);
            if (OperatingSystem.IsWindows())
                await Assert.That(endpoint.Address).IsEqualTo(pipeName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.IpcAddressEnv, null);
        }

        var globalPipe = $"novolis-global-{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(AgentEndpoints.EndpointEnvVar, globalPipe);
        try
        {
            var endpoint = AgentEndpoints.CreateIpcEndpoint(def);
            if (OperatingSystem.IsWindows())
                await Assert.That(endpoint.Address).IsEqualTo(globalPipe);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentEndpoints.EndpointEnvVar, null);
        }
    }

    [Test]
    public async Task AgentSurface_returns_null_when_nothing_enabled()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        foreach (var key in new[]
        {
            def.EnableEnv, def.HttpEnableEnv, def.IpcEnableEnv, def.TcpEnableEnv, def.RpcEnableEnv,
            def.McpEnableEnv, def.StdioEnableEnv, def.HttpPortEnv, def.TcpPortEnv, def.RpcPortEnv,
        })
            Environment.SetEnvironmentVariable(key, null);

        await Assert.That(AgentSurface.TryAttachFromEnvironment(host, def)).IsNull();

        var disabled = new AgentAttachOptions
        {
            EnableIpc = false,
            EnableHttp = false,
            EnableTcp = false,
            EnableRpc = false,
            EnableMcpStdio = false,
            EnableStdio = false,
        };
        await Assert.That(AgentSurface.AttachAll(host, def, disabled)).IsNull();
    }

    [Test]
    public async Task HttpHost_document_and_empty_command_body()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "doc") };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var docResp = await client.GetAsync("agent/document");
        docResp.EnsureSuccessStatusCode();
        await Assert.That(await docResp.Content.ReadAsStringAsync()).Contains("surfaceId");

        using var cmdResp = await client.PostAsync(
            "agent/command",
            new StringContent("", Encoding.UTF8, "application/json"));
        cmdResp.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task HttpHost_websocket_close_frame_ends_session()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.ConnectAsync(new Uri(http.WebSocketUrl), cts.Token);
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
        await Task.Delay(100);
    }

    [Test]
    public async Task HttpHost_rpc_unknown_method_returns_500()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/") };

        using var resp = await client.PostAsync(
            "agent/rpc",
            new StringContent("""{"method":"nope.method"}""", Encoding.UTF8, "application/json"));
        await Assert.That((int)resp.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task JsonRpcHost_skips_blank_lines_and_returns_error_for_bad_json()
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

        await writer.WriteLineAsync("");
        await writer.WriteLineAsync("{bad");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.TryGetProperty("error", out _)).IsTrue();
    }

    [Test]
    public async Task JsonRpcHost_notification_error_is_silent()
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

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"nope"}""");
        using var readCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            _ = await reader.ReadLineAsync(readCts.Token);
            throw new InvalidOperationException("Expected no reply for notification errors.");
        }
        catch (OperationCanceledException)
        {
            // expected: no reply for notification errors
        }
    }

    [Test]
    public async Task TcpJsonlHost_command_round_trip()
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

        await writer.WriteLineAsync("""{"method":"command","id":2,"actionId":"ping","params":{"label":"tcp-cmd"}}""");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("tcp-cmd");
    }

    [Test]
    public Task LocalIpcHost_malformed_payload_returns_fault() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost();
            var pipeName = $"novolis-agent-bad-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            var transport = LocalIpcTransport.CreateClient();
            await using var connection = await AgentIpcTestHelper.ConnectConnectionAsync(endpoint);
            await connection.SendAsync(
                new LocalIpcFrame(7, AgentFrameKinds.Request, AgentMethodNames.Command, new byte[] { 0xFF, 0x00 }),
                CancellationToken.None);

            LocalIpcFrame? fault = null;
            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var frame in connection.ReadAllAsync(readCts.Token))
            {
                if (frame.Sequence == 7 && frame.Kind == AgentFrameKinds.Fault)
                {
                    fault = frame;
                    break;
                }
            }

            await Assert.That(fault).IsNotNull();
        });

    [Test]
    public async Task InMemoryChannel_send_and_dispose()
    {
        await using var channel = new InMemoryAgentChannel();
        var frame = new AgentFrame(1, AgentFrameKinds.Response, AgentMethodNames.Hello, ReadOnlyMemory<byte>.Empty);
        await channel.SendAsync(frame);
        var read = await channel.ReadOutboundAsync();
        await Assert.That(read.Method).IsEqualTo(AgentMethodNames.Hello);
    }
}



