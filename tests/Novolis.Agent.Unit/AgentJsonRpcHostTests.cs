using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentJsonRpcHostTests
{
    [Test]
    public async Task JsonRpcHost_hello_and_command()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "rpc-test") };
        var port = AgentTestPorts.GetFreePort();
        await using var rpcHost = AgentJsonRpcHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"hello","id":1}""");
        var helloLine = await reader.ReadLineAsync();
        using (var helloDoc = JsonDocument.Parse(helloLine!))
        {
            await Assert.That(helloDoc.RootElement.GetProperty("result").GetProperty("appId").GetString()).IsEqualTo("rpc-test");
        }

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"command","id":2,"params":{"actionId":"ping","params":{"label":"rpc"}}}""");
        var cmdLine = await reader.ReadLineAsync();
        using (var cmdDoc = JsonDocument.Parse(cmdLine!))
        {
            await Assert.That(cmdDoc.RootElement.GetProperty("result").GetProperty("ok").GetBoolean()).IsTrue();
        }
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("rpc");
        await Assert.That(rpcHost.Kind).IsEqualTo("json-rpc");
    }

    [Test]
    public async Task JsonRpcHost_subscribe_receives_notification()
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

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"subscribe","id":1}""");
        var ack = await reader.ReadLineAsync();
        using (JsonDocument.Parse(ack!)) { }

        host.RaiseDecision("pick-a");
        var note = await reader.ReadLineAsync();
        using var noteDoc = JsonDocument.Parse(note!);
        await Assert.That(noteDoc.RootElement.GetProperty("method").GetString()).IsEqualTo(AgentMethodNames.Decision);
    }

    [Test]
    public async Task JsonRpcHost_unknown_method_returns_error()
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

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"nope","id":9}""");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.TryGetProperty("error", out _)).IsTrue();
    }

    [Test]
    public async Task JsonRpcHost_notification_without_id_still_broadcasts()
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

        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"subscribe"}""");
        host.RaiseChanged("no-id");
        var note = await reader.ReadLineAsync();
        using var noteDoc = JsonDocument.Parse(note!);
        await Assert.That(noteDoc.RootElement.GetProperty("method").GetString()).IsEqualTo(AgentMethodNames.Changed);
    }

    [Test]
    public async Task JsonRpcHost_TryAttachFromEnvironment_respects_flag()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        Environment.SetEnvironmentVariable(def.RpcEnableEnv, "1");
        Environment.SetEnvironmentVariable(def.RpcPortEnv, AgentTestPorts.GetFreePort().ToString());
        try
        {
            await using var attached = AgentJsonRpcHost.TryAttachFromEnvironment(host, def);
            await Assert.That(attached).IsNotNull();
            Environment.SetEnvironmentVariable(def.RpcEnableEnv, null);
            await using var disabled = AgentJsonRpcHost.TryAttachFromEnvironment(host, def);
            await Assert.That(disabled).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(def.RpcEnableEnv, null);
            Environment.SetEnvironmentVariable(def.RpcPortEnv, null);
        }
    }
}



