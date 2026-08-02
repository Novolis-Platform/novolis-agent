using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentHttpClientTests
{
    [Test]
    public async Task HttpClient_round_trips_against_live_host()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var host = new FakeAgentHost
        {
            HelloResponse = def.BuildHello(appId: "client-test"),
            SnapshotResponse = new AgentSnapshot { Day = 3, HubId = "client" },
        };
        var port = 19930 + Random.Shared.Next(0, 100);
        await using var httpHost = AgentHttpHost.Attach(host, def, port);

        await using var client = new AgentHttpClient(httpHost.BaseUrl);
        var hello = await client.HelloAsync();
        await Assert.That(hello.AppId).IsEqualTo("client-test");

        var snapshot = await client.SnapshotAsync();
        await Assert.That(snapshot.Day).IsEqualTo(3);

        var actions = await client.ActionsAsync();
        await Assert.That(actions.Actions.Length).IsGreaterThan(0);

        var doc = await client.DocumentAsync();
        await Assert.That(doc.GetProperty("surfaceId").GetString()).IsEqualTo("unit");

        var announce = await client.AnnounceAsync();
        await Assert.That(announce.AppId).IsEqualTo("client-test");

        await client.SubscribeAsync();
        await Assert.That(host.SubscribeCount).IsEqualTo(1);

        var continued = await client.ContinueAsync();
        await Assert.That(continued.Ok).IsTrue();

        var cmd = new AgentCommand { ActionId = "ping" };
        cmd.With("label", "http-client");
        var result = await client.CommandAsync(cmd);
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(host.Executed[0].Get("label")).IsEqualTo("http-client");
    }

    [Test]
    [NotInParallel("agent-env")]
    public async Task HttpClient_TryFromDefinition_uses_default_port()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        Environment.SetEnvironmentVariable(def.HttpEnableEnv + "_URL", null);
        Environment.SetEnvironmentVariable(def.HttpPortEnv, null);
        try { File.Delete(def.HttpMarkerPath); } catch { /* ignore */ }

        var client = AgentHttpClient.TryFromDefinition(def);
        await Assert.That(client).IsNotNull();
        await Assert.That(client!.BaseUrl).IsEqualTo($"http://127.0.0.1:{def.DefaultHttpPort}");
        await client.DisposeAsync();
    }
}



