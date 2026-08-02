using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentJsonDispatcherExtendedTests
{
    [Test]
    public async Task ParseCommand_reads_optional_scene_fields()
    {
        using var doc = JsonDocument.Parse("""
            {
              "actionId": "spawn",
              "params": { "label": "x" },
              "name": "Widget",
              "path": "/root",
              "intensity": 1.5,
              "segments": 8,
              "additive": true
            }
            """);
        var cmd = AgentJsonDispatcher.ParseCommand(doc.RootElement);
        await Assert.That(cmd.ActionId).IsEqualTo("spawn");
        await Assert.That(cmd.Name).IsEqualTo("Widget");
        await Assert.That(cmd.Path).IsEqualTo("/root");
        await Assert.That(cmd.Intensity).IsEqualTo(1.5f);
        await Assert.That(cmd.Segments).IsEqualTo(8);
        await Assert.That(cmd.Additive).IsTrue();
    }

    [Test]
    public async Task ParseCommand_handles_non_object_root()
    {
        var cmd = AgentJsonDispatcher.ParseCommand(JsonDocument.Parse("\"text\"").RootElement);
        await Assert.That(cmd.ActionId).IsEqualTo("");
    }

    [Test]
    public async Task Dispatch_wraps_nested_command_property()
    {
        var host = new FakeAgentHost();
        using var doc = JsonDocument.Parse("""
            {"command":{"actionId":"ping","params":{"label":"nested"}}}
            """);
        var result = AgentJsonDispatcher.Dispatch(host, AgentMethodNames.Command, doc.RootElement);
        await Assert.That(result).IsTypeOf<AgentCommandResult>();
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("nested");
    }

    [Test]
    public async Task AgentSurface_exposes_transport_urls()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var surface = AgentSurface.AttachAll(host, def, new AgentAttachOptions
        {
            EnableHttp = true,
            EnableTcp = true,
            EnableRpc = true,
            HttpPort = port,
            TcpPort = AgentTestPorts.GetFreePort(),
            RpcPort = AgentTestPorts.GetFreePort(),
        });

        await Assert.That(surface).IsNotNull();
        await Assert.That(surface!.HttpBaseUrl).Contains(port.ToString());
        await Assert.That(surface.WebSocketUrl).Contains("/agent/ws");
        await Assert.That(surface.TcpPort).IsNotNull();
        await Assert.That(surface.RpcPort).IsNotNull();
    }
}



