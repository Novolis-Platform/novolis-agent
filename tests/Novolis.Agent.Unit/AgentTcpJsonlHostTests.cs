using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentTcpJsonlHostTests
{
    [Test]
    public async Task TcpJsonlHost_hello_round_trip()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "tcp-test") };
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"method":"hello","id":3}""");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(doc.RootElement.GetProperty("id").GetInt64()).IsEqualTo(3);
        await Assert.That(doc.RootElement.GetProperty("result").GetProperty("appId").GetString()).IsEqualTo("tcp-test");
        await Assert.That(tcpHost.Kind).IsEqualTo("tcp-jsonl");
        await Assert.That(tcpHost.Port).IsEqualTo(port);
    }

    [Test]
    public async Task TcpJsonlHost_skips_blank_and_comment_lines()
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

        await writer.WriteLineAsync("# comment");
        await writer.WriteLineAsync("");
        await writer.WriteLineAsync("""{"method":"continue","id":1}""");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(host.ContinueCount).IsEqualTo(1);
    }

    [Test]
    public async Task TcpJsonlHost_reports_unknown_method_as_error()
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

        await writer.WriteLineAsync("""{"method":"bad.method","id":9}""");
        var line = await reader.ReadLineAsync();
        using var doc = JsonDocument.Parse(line!);
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsFalse();
        await Assert.That(doc.RootElement.GetProperty("error").GetString()).Contains("Unknown method");
    }

    [Test]
    public async Task TcpJsonlHost_subscribe_receives_changed_event()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 1, HubId = "tcp" } };
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"method":"subscribe","id":1}""");
        var ack = await reader.ReadLineAsync();
        using (JsonDocument.Parse(ack!)) { }

        host.RaiseChanged("tcp-event");
        var evtLine = await reader.ReadLineAsync();
        using var evtDoc = JsonDocument.Parse(evtLine!);
        await Assert.That(evtDoc.RootElement.GetProperty("eventName").GetString()).IsEqualTo(AgentMethodNames.Changed);
    }

    [Test]
    public async Task TcpJsonlHost_subscribe_receives_decision_event()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 2, HubId = "tcp" } };
        var port = AgentTestPorts.GetFreePort();
        await using var tcpHost = AgentTcpJsonlHost.Attach(host, def, port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync("""{"method":"subscribe","id":1}""");
        await reader.ReadLineAsync();

        host.RaiseDecision("tcp-decide");
        var evtLine = await reader.ReadLineAsync();
        using var evtDoc = JsonDocument.Parse(evtLine!);
        await Assert.That(evtDoc.RootElement.GetProperty("eventName").GetString()).IsEqualTo(AgentMethodNames.Decision);
    }
}



