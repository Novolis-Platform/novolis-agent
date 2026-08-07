using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Unit;

[NotInParallel("agent-ipc")]
public sealed class AgentLocalIpcHostTests
{
    [Test]
    public Task LocalIpcHost_hello_snapshot_and_command() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost
            {
                HelloResponse = def.BuildHello(appId: "ipc-test"),
                SnapshotResponse = new AgentSnapshot { Day = 4, HubId = "pipe" },
            };
            var pipeName = $"novolis-agent-test-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ipcHost.StartAsync(startCts.Token);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            var expectedKind = OperatingSystem.IsWindows()
                ? LocalIpcTransportKind.NamedPipe
                : LocalIpcTransportKind.UnixDomainSocket;
            await Assert.That(endpoint.Kind).IsEqualTo(expectedKind);

            await using var client = await AgentIpcTestHelper.ConnectAsync(endpoint);
            var hello = await client.HelloAsync();
            await Assert.That(hello.AppId).IsEqualTo("ipc-test");

            var snapshot = await client.SnapshotAsync();
            await Assert.That(snapshot.Day).IsEqualTo(4);
            await Assert.That(snapshot.HubId).IsEqualTo("pipe");

            var actions = await client.ActionsAsync();
            await Assert.That(actions.Actions.Length).IsGreaterThan(0);

            var cmd = new AgentCommand { ActionId = "ping" };
            cmd.With("label", "ipc");
            var result = await client.CommandAsync(cmd);
            await Assert.That(result.Ok).IsTrue();
            await Assert.That(host.Executed[0].Get("label")).IsEqualTo("ipc");

            var continued = await client.ContinueAsync();
            await Assert.That(continued.Ok).IsTrue();
            await Assert.That(host.ContinueCount).IsEqualTo(1);

            await Assert.That(ipcHost.Kind).IsEqualTo("local-ipc");
        });

    [Test]
    public Task LocalIpcHost_subscribe_accepts_connection() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost();
            var pipeName = $"novolis-agent-test-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ipcHost.StartAsync(startCts.Token);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            await using var client = await AgentIpcTestHelper.ConnectAsync(endpoint);

            var subscribed = await client.SubscribeAsync();
            await Assert.That(subscribed.Ok).IsTrue();
            await Assert.That(host.SubscribeCount).IsEqualTo(1);
        });

    [Test]
    public Task LocalIpcHost_broadcasts_changed_event_to_subscriber() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 2, HubId = "evt" } };
            var pipeName = $"novolis-agent-test-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ipcHost.StartAsync(startCts.Token);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            await using var client = await AgentIpcTestHelper.ConnectAsync(endpoint);

            AgentChangedEvent? received = null;
            using var done = new ManualResetEventSlim(false);
            client.EventReceived += (_, payload) =>
            {
                received = AgentProtocolCodec.Deserialize<AgentChangedEvent>(payload);
                done.Set();
            };

            await client.SubscribeAsync();
            host.RaiseChanged("ipc-broadcast");
            await Assert.That(done.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            await Assert.That(received!.Reason).IsEqualTo("ipc-broadcast");
        });

    [Test]
    public Task LocalIpcHost_broadcasts_decision_and_action_result() =>
        AgentIpcTestHelper.RunAsync(async () =>
        {
            var def = AgentSurfaceDefinition.From<IUnitSurface>();
            var host = new FakeAgentHost { SnapshotResponse = new AgentSnapshot { Day = 3, HubId = "evt2" } };
            var pipeName = $"novolis-agent-test-{Guid.NewGuid():N}";
            await using var ipcHost = AgentLocalIpcTransport.Attach(host, def, pipeName);
            using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ipcHost.StartAsync(startCts.Token);

            var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
            await using var client = await AgentIpcTestHelper.ConnectAsync(endpoint);

            AgentDecisionEvent? decision = null;
            AgentActionResultEvent? action = null;
            using var decisionDone = new ManualResetEventSlim(false);
            using var actionDone = new ManualResetEventSlim(false);
            client.EventReceived += (name, payload) =>
            {
                if (name == AgentMethodNames.Decision)
                {
                    decision = AgentProtocolCodec.Deserialize<AgentDecisionEvent>(payload);
                    decisionDone.Set();
                }
                else if (name == AgentMethodNames.ActionResult)
                {
                    action = AgentProtocolCodec.Deserialize<AgentActionResultEvent>(payload);
                    actionDone.Set();
                }
            };

            await client.SubscribeAsync();
            host.RaiseDecision("ipc-decide");
            host.RaiseActionResult("ping", ok: true);
            await Assert.That(decisionDone.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            await Assert.That(actionDone.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            await Assert.That(decision!.DecisionLine).IsEqualTo("ipc-decide");
            await Assert.That(action!.ActionId).IsEqualTo("ping");
        });

    [Test]
    public async Task AgentEndpoints_marker_path_and_ipc_endpoint_override()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var marker = AgentEndpoints.MarkerPath(AgentEndpoints.HttpMarkerFileName);
        await Assert.That(marker).Contains(AgentEndpoints.HttpMarkerFileName);

        var pipeName = $"novolis-agent-endpoint-{Guid.NewGuid():N}";
        var endpoint = AgentEndpoints.CreateIpcEndpoint(def, pipeName);
        if (OperatingSystem.IsWindows())
            await Assert.That(endpoint.Address).IsEqualTo(pipeName);
        else
            await Assert.That(endpoint.Address).Contains(pipeName);
    }
}

