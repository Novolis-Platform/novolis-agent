using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentStdioHostTests
{
    [Test]
    public async Task StdioHost_jsonl_hello_round_trip()
    {
        var host = new FakeAgentHost { HelloResponse = new AgentHello { AppId = "stdio-test", SurfaceId = "unit" } };
        var input = new StringReader("""{"method":"hello","id":7}""" + "\n");
        var output = new StringWriter();
        var stdio = new AgentStdioHost(host, input, output);

        await stdio.RunUntilEofAsync();

        using var doc = JsonDocument.Parse(output.ToString().Trim());
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(doc.RootElement.GetProperty("id").GetInt64()).IsEqualTo(7);
        await Assert.That(doc.RootElement.GetProperty("result").GetProperty("appId").GetString()).IsEqualTo("stdio-test");
    }

    [Test]
    public async Task StdioHost_skips_blank_and_comment_lines()
    {
        var host = new FakeAgentHost();
        var input = new StringReader("# comment\n\n   \n{\"method\":\"continue\",\"id\":1}\n");
        var output = new StringWriter();
        var stdio = new AgentStdioHost(host, input, output);

        await stdio.RunUntilEofAsync();

        using var doc = JsonDocument.Parse(output.ToString().Trim());
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(host.ContinueCount).IsEqualTo(1);
    }

    [Test]
    public async Task StdioHost_reports_errors_as_json()
    {
        var host = new FakeAgentHost();
        var input = new StringReader("""{"method":"bad.method","id":1}""" + "\n");
        var output = new StringWriter();
        var stdio = new AgentStdioHost(host, input, output);

        await stdio.RunUntilEofAsync();

        using var doc = JsonDocument.Parse(output.ToString().Trim());
        await Assert.That(doc.RootElement.GetProperty("ok").GetBoolean()).IsFalse();
        await Assert.That(doc.RootElement.GetProperty("error").GetString()).Contains("Unknown method");
    }
}


