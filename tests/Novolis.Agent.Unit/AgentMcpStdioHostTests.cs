using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentMcpStdioHostTests
{
    [Test]
    public async Task McpStdio_initialize_tools_and_call()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { HelloResponse = def.BuildHello(appId: "mcp-test") };
        var input = new StringReader(
            """
            {"jsonrpc":"2.0","method":"initialize","id":1}
            {"jsonrpc":"2.0","method":"tools/list","id":2}
            {"jsonrpc":"2.0","method":"tools/call","id":3,"params":{"name":"unit_hello","arguments":{}}}
            {"jsonrpc":"2.0","method":"ping","id":4}
            """ + "\n");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);

        await mcp.RunUntilEofAsync();

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsGreaterThanOrEqualTo(4);
        using var initDoc = JsonDocument.Parse(lines[0]);
        await Assert.That(initDoc.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString())
            .IsEqualTo("unit");

        using var toolsDoc = JsonDocument.Parse(lines[1]);
        await Assert.That(toolsDoc.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength()).IsGreaterThan(0);

        using var callDoc = JsonDocument.Parse(lines[2]);
        await Assert.That(callDoc.RootElement.GetProperty("result").GetProperty("content").GetArrayLength()).IsEqualTo(1);
        await Assert.That(mcp.Kind).IsEqualTo("mcp-stdio");
    }

    [Test]
    public async Task McpStdio_start_stop_lifecycle()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var input = new StringReader("");
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, input, output);
        await mcp.StartAsync();
        await mcp.StopAsync();
    }

    [Test]
    public async Task StdioHost_start_stop_lifecycle()
    {
        var host = new FakeAgentHost();
        var input = new StringReader("");
        var output = new StringWriter();
        await using var stdio = new AgentStdioHost(host, input, output);
        await stdio.StartAsync();
        await stdio.StopAsync();
    }
}


