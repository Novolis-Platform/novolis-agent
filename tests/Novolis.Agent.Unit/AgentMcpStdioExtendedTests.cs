using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentMcpStdioExtendedTests
{
    [Test]
    public async Task McpStdio_all_tool_variants()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost
        {
            HelloResponse = def.BuildHello(appId: "mcp-tools"),
            SnapshotResponse = new AgentSnapshot { Day = 8, HubId = "mcp" },
        };
        var input = new StringReader(
            """
            {"jsonrpc":"2.0","method":"tools/call","id":1,"params":{"name":"unit_snapshot","arguments":{}}}
            {"jsonrpc":"2.0","method":"tools/call","id":2,"params":{"name":"unit_actions","arguments":{}}}
            {"jsonrpc":"2.0","method":"tools/call","id":3,"params":{"name":"unit_command","arguments":{"actionId":"ping","params":{"label":"mcp"}}}}
            {"jsonrpc":"2.0","method":"tools/call","id":4,"params":{"name":"unit_continue","arguments":{}}}
            {"jsonrpc":"2.0","method":"tools/call","id":5,"params":{"name":"unit_subscribe","arguments":{}}}
            """ + "\n");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);
        await mcp.RunUntilEofAsync();

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(5);

        using var snapDoc = JsonDocument.Parse(lines[0]);
        await Assert.That(snapDoc.RootElement.GetProperty("result").GetProperty("content").GetArrayLength()).IsEqualTo(1);
        await Assert.That(snapDoc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString())
            .Contains("\"hubId\":\"mcp\"");

        using var cmdDoc = JsonDocument.Parse(lines[2]);
        await Assert.That(cmdDoc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString())
            .Contains("\"ok\":true");
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("mcp");
        await Assert.That(host.ContinueCount).IsEqualTo(1);
        await Assert.That(host.SubscribeCount).IsEqualTo(1);
    }

    [Test]
    public async Task McpStdio_unknown_method_and_tool_return_errors()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var input = new StringReader(
            """
            {"jsonrpc":"2.0","method":"nope","id":1}
            {"jsonrpc":"2.0","method":"tools/call","id":2,"params":{"name":"unit_missing","arguments":{}}}
            {"jsonrpc":"2.0","method":"tools/call","id":3,"params":{"name":"","arguments":{}}}
            """ + "\n");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);
        await mcp.RunUntilEofAsync();

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(3);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            await Assert.That(doc.RootElement.TryGetProperty("error", out var err)).IsTrue();
            await Assert.That(err.GetProperty("code").GetInt32()).IsEqualTo(-32603);
        }
    }

    [Test]
    public async Task McpStdio_invalid_json_is_ignored_without_id()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var input = new StringReader("{not-json}\n");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);
        await mcp.RunUntilEofAsync();
        await Assert.That(output.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task McpStdio_notification_without_id_writes_nothing()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var input = new StringReader("""{"jsonrpc":"2.0","method":"ping"}""" + "\n");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);
        await mcp.RunUntilEofAsync();
        await Assert.That(output.ToString()).IsEqualTo("");
    }
}


