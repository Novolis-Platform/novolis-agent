using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentWatchTransportTests
{
    [Test]
    public async Task Http_watch_poll_and_sse_do_not_open_cross_origin_access()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(10) };

        using var hello = await client.GetAsync("agent/hello");
        hello.EnsureSuccessStatusCode();
        await Assert.That(hello.Headers.Contains("Access-Control-Allow-Origin")).IsTrue();

        using var watchResponse = await client.PostAsync(
            "agent/watch",
            new StringContent("""{"eventNames":["agent.changed"],"callbackId":"http"}""", Encoding.UTF8, "application/json"));
        watchResponse.EnsureSuccessStatusCode();
        await Assert.That(watchResponse.Headers.Contains("Access-Control-Allow-Origin")).IsFalse();
        using var watchDoc = JsonDocument.Parse(await watchResponse.Content.ReadAsStringAsync());
        var watchId = watchDoc.RootElement.GetProperty("result").GetProperty("watchId").GetString();
        host.RaiseChanged("from-http");

        using var next = await client.PostAsync(
            "agent/watch/next",
            new StringContent("{\"watchId\":\"" + watchId + "\",\"maxEvents\":4}", Encoding.UTF8, "application/json"));
        var body = await next.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("from-http");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "agent/watch?eventNames=agent.changed");
        using var sse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        sse.EnsureSuccessStatusCode();
        await Assert.That(sse.Headers.Contains("Access-Control-Allow-Origin")).IsFalse();
        await using var stream = await sse.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var opened = await reader.ReadLineAsync(cts.Token);
        await Assert.That(opened).StartsWith("event: agent.watch");
        host.RaiseChanged("sse-watch");
        var buffer = new StringBuilder();
        for (var i = 0; i < 20; i++)
        {
            buffer.AppendLine(await reader.ReadLineAsync(cts.Token));
            if (buffer.ToString().Contains("event: agent.event", StringComparison.Ordinal)
                && buffer.ToString().Contains("sse-watch", StringComparison.Ordinal))
            {
                break;
            }
        }

        await Assert.That(buffer.ToString()).Contains("event: agent.event");
        await Assert.That(buffer.ToString()).Contains("sse-watch");
    }

    [Test]
    public async Task Mcp_lists_the_command_escape_hatch_and_watch_next()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost();
        var reader = new LineReader();
        var output = new StringWriter();
        await using var mcp = new AgentMcpStdioTransport(host, def, reader, output);
        var running = mcp.RunUntilEofAsync();

        reader.Add("""{"jsonrpc":"2.0","method":"initialize","id":1}""");
        reader.Add("""{"jsonrpc":"2.0","method":"tools/list","id":2}""");
        await WaitForAsync(output, "\"id\":2");
        await Assert.That(output.ToString()).Contains("unit_command");
        await Assert.That(output.ToString()).Contains("unit_watch");

        reader.Add("""{"jsonrpc":"2.0","method":"tools/call","id":3,"params":{"name":"unit_watch","arguments":{"callbackId":"mcp","eventNames":["agent.changed"]}}}""");
        await WaitForAsync(output, "\"id\":3");
        var watchLine = output.ToString().Split('\n').Last(line => line.Contains("\"id\":3", StringComparison.Ordinal));
        using var watchDoc = JsonDocument.Parse(watchLine);
        var text = watchDoc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        using var payload = JsonDocument.Parse(text);
        var watchId = payload.RootElement.GetProperty("watchId").GetString();
        host.RaiseChanged("from-mcp");
        reader.Add("{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"id\":4,\"params\":{\"name\":\"unit_watch_next\",\"arguments\":{\"watchId\":\"" + watchId + "\",\"maxEvents\":4}}}");
        await WaitForAsync(output, "from-mcp");
        reader.Complete();
        await running.WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static async Task WaitForAsync(StringWriter output, string text)
    {
        for (var i = 0; i < 50 && !output.ToString().Contains(text, StringComparison.Ordinal); i++)
        {
            await Task.Delay(20);
        }
    }

    private sealed class LineReader : TextReader
    {
        private readonly System.Threading.Channels.Channel<string> _lines =
            System.Threading.Channels.Channel.CreateUnbounded<string>();

        public void Add(string line) => _lines.Writer.TryWrite(line);

        public void Complete() => _lines.Writer.TryComplete();

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (await _lines.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_lines.Reader.TryRead(out var line))
                {
                    return line;
                }
            }

            return null;
        }
    }
}
