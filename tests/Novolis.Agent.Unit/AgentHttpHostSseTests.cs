using System.Net.Http;
using System.Text;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentHttpHostSseTests
{
    [Test]
    public async Task HttpHost_sse_connects_and_broadcasts_events()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 3, HubId = "sse" } };
        var port = AgentTestPorts.GetFreePort();
        await using var http = AgentHttpHost.Attach(host, def, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(10) };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, "agent/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var connected = await reader.ReadLineAsync(cts.Token);
        await Assert.That(connected).IsEqualTo(": connected");
        await Assert.That(host.SubscribeCount).IsGreaterThanOrEqualTo(1);

        host.RaiseChanged("sse-broadcast");
        host.RaiseDecision("pick-a");
        host.RaiseActionResult("ping", ok: true);

        var buffer = new StringBuilder();
        for (var i = 0; i < 30; i++)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line is null)
                break;
            buffer.AppendLine(line);
            var text = buffer.ToString();
            if (text.Contains("sse-broadcast", StringComparison.Ordinal)
                && text.Contains("event: agent.changed", StringComparison.Ordinal))
                break;
        }

        await Assert.That(buffer.ToString()).Contains("event: agent.changed");
        await Assert.That(buffer.ToString()).Contains("sse-broadcast");
    }
}



