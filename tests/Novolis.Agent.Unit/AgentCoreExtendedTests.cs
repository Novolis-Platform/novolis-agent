using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentCoreExtendedTests
{
    [Test]
    public async Task ProtocolCodec_round_trips_all_core_dtos()
    {
        var hello = new AgentHello
        {
            Sequence = 1,
            AppId = "app",
            SurfaceId = "surface",
            Capabilities = [AgentMethodNames.Hello],
            HttpPort = 8080,
        };
        await RoundTrip(hello);

        var command = new AgentCommand { ActionId = "ping" };
        command.With("label", "x").With("count", 3).With("ready", true);
        await RoundTrip(command);

        var snapshot = new AgentSnapshot
        {
            Day = 5,
            HubId = "sol",
            StatusLines = new Dictionary<string, string>(StringComparer.Ordinal) { ["cash"] = "100" },
            Boards =
            [
                new AgentBoard
                {
                    Id = AgentBoardIds.SpotFreight,
                    Items = [new AgentBoardItem { Index = 0, Id = "lot-1", Label = "Cargo", CanAct = true }],
                },
            ],
            LastAction = new AgentLastAction { ActionId = "travel", Ok = true, Message = "done" },
        };
        await RoundTrip(snapshot);
        await RoundTrip(new AgentActionsResponse { Sequence = 2, Actions = [new AgentAction { Id = "ping", Enabled = true }] });
        await RoundTrip(new AgentCommandResult { Ok = true, ActionId = "ping", Snapshot = snapshot });
        await RoundTrip(new AgentDecisionEvent { Day = 5, HubId = "sol", DecisionLine = "choose", Snapshot = snapshot });
        await RoundTrip(new AgentChangedEvent { Reason = "tick", Snapshot = snapshot, DocumentName = "scene", NodeCount = 3 });
        await RoundTrip(new AgentActionResultEvent { ActionId = "ping", Ok = true, Message = "ok", Snapshot = snapshot });
        await RoundTrip(new AgentSubscribeResponse { Ok = true });
        await RoundTrip(new AgentFault { Message = "boom" });
    }

    [Test]
    public async Task AgentCommand_parses_typed_params()
    {
        var cmd = new AgentCommand().With("qty", 42).With("speed", 1.5).With("ready", true).With("yes", "yes").With("no", "no");
        await Assert.That(cmd.TryGetInt("qty", out var qty)).IsTrue();
        await Assert.That(qty).IsEqualTo(42);
        await Assert.That(cmd.TryGetDouble("speed", out var speed)).IsTrue();
        await Assert.That(speed).IsEqualTo(1.5);
        await Assert.That(cmd.TryGetBool("ready", out var ready)).IsTrue();
        await Assert.That(ready).IsTrue();
        await Assert.That(cmd.TryGetBool("yes", out var yes)).IsTrue();
        await Assert.That(yes).IsTrue();
        await Assert.That(cmd.TryGetBool("no", out var no)).IsTrue();
        await Assert.That(no).IsFalse();
        await Assert.That(cmd.Get("missing")).IsNull();
    }

    [Test]
    public async Task AgentSnapshot_line_and_board_items()
    {
        var snap = new AgentSnapshot
        {
            StatusLines = new Dictionary<string, string>(StringComparer.Ordinal) { [AgentLineKeys.Cash] = "500" },
            Boards =
            [
                new AgentBoard
                {
                    Id = AgentBoardIds.MarketLots,
                    Items = [new AgentBoardItem { Id = "a", Label = "A" }],
                },
            ],
        };
        await Assert.That(snap.Line(AgentLineKeys.Cash)).IsEqualTo("500");
        await Assert.That(snap.Line("missing")).IsEqualTo("");
        await Assert.That(snap.BoardItems(AgentBoardIds.MarketLots).Length).IsEqualTo(1);
        await Assert.That(snap.BoardItems("unknown").Length).IsEqualTo(0);
    }

    [Test]
    public async Task MethodNames_match_all_aliases()
    {
        foreach (var (canonical, legacy, bare) in new[]
        {
            (AgentMethodNames.Hello, AgentMethodNames.Legacy.Hello, "hello"),
            (AgentMethodNames.Snapshot, AgentMethodNames.Legacy.Snapshot, "snapshot"),
            (AgentMethodNames.Actions, AgentMethodNames.Legacy.Actions, "actions"),
            (AgentMethodNames.Command, AgentMethodNames.Legacy.Command, "command"),
            (AgentMethodNames.Continue, AgentMethodNames.Legacy.Continue, "continue"),
            (AgentMethodNames.Subscribe, AgentMethodNames.Legacy.Subscribe, "subscribe"),
        })
        {
            await Assert.That(AgentMethodNames.IsHello(canonical) || AgentMethodNames.IsSnapshot(canonical)
                || AgentMethodNames.IsActions(canonical) || AgentMethodNames.IsCommand(canonical)
                || AgentMethodNames.IsContinue(canonical) || AgentMethodNames.IsSubscribe(canonical)).IsTrue();
            await Assert.That(AgentMethodNames.IsHello(legacy) || legacy.Contains("session")).IsTrue();
            await Assert.That(AgentMethodNames.IsHello(bare) || bare is not "hello" || AgentMethodNames.IsHello(bare)).IsTrue();
        }

        await Assert.That(AgentMethodNames.IsHello("HELLO")).IsTrue();
        await Assert.That(AgentMethodNames.IsCommand(null)).IsFalse();
    }

    [Test]
    public async Task FakeAgentHost_tracks_execute_continue_subscribe_and_events()
    {
        var host = new FakeAgentHost();
        var cmd = new AgentCommand { ActionId = "ping" };
        var result = host.Execute(cmd);
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(host.Executed.Count).IsEqualTo(1);

        host.Continue();
        await Assert.That(host.ContinueCount).IsEqualTo(1);

        host.Subscribe();
        await Assert.That(host.SubscribeCount).IsEqualTo(1);

        AgentChangedEvent? changed = null;
        host.Changed += e => changed = e;
        host.RaiseChanged("test");
        await Assert.That(changed).IsNotNull();
        await Assert.That(changed!.Reason).IsEqualTo("test");
    }

    [Test]
    public async Task AgentAnnouncement_from_hello()
    {
        var hello = new AgentHello
        {
            SurfaceId = "unit",
            AppId = "test",
            AppTitle = "Test",
            ProcessId = 123,
            Capabilities = ["agent.hello"],
            HttpPort = 9000,
        };
        var announcement = AgentAnnouncement.From(hello);
        await Assert.That(announcement.SurfaceId).IsEqualTo("unit");
        await Assert.That(announcement.AppId).IsEqualTo("test");
        await Assert.That(announcement.HttpPort).IsEqualTo(9000);
    }

    [Test]
    public async Task JsonDispatcher_snapshot_actions_continue_subscribe()
    {
        var host = new FakeAgentHost();
        var empty = JsonDocument.Parse("{}").RootElement;

        var snap = AgentJsonDispatcher.Dispatch(host, AgentMethodNames.Snapshot, empty);
        await Assert.That(snap).IsTypeOf<AgentSnapshot>();

        var actions = AgentJsonDispatcher.Dispatch(host, "actions", empty);
        await Assert.That(actions).IsTypeOf<AgentActionsResponse>();

        var continued = AgentJsonDispatcher.Dispatch(host, "continue", empty);
        await Assert.That(continued).IsTypeOf<AgentCommandResult>();
        await Assert.That(host.ContinueCount).IsEqualTo(1);

        var subscribed = AgentJsonDispatcher.Dispatch(host, AgentMethodNames.Subscribe, empty);
        await Assert.That(subscribed).IsTypeOf<AgentSubscribeResponse>();
        await Assert.That(host.SubscribeCount).IsEqualTo(1);
    }

    [Test]
    public async Task JsonDispatcher_parse_command_typed_fields()
    {
        using var doc = JsonDocument.Parse(
            """{"actionId":"addLight","params":{"name":"key"},"nodeId":"n1","intensity":2.5,"segments":8,"additive":true}""");
        var cmd = AgentJsonDispatcher.ParseCommand(doc.RootElement);
        await Assert.That(cmd.ActionId).IsEqualTo("addLight");
        await Assert.That(cmd.Get("name")).IsEqualTo("key");
        await Assert.That(cmd.NodeId).IsEqualTo("n1");
        await Assert.That(cmd.Intensity).IsEqualTo(2.5f);
        await Assert.That(cmd.Segments).IsEqualTo(8);
        await Assert.That(cmd.Additive).IsTrue();
    }

    [Test]
    public async Task JsonDispatcher_unknown_method_throws()
    {
        var host = new FakeAgentHost();
        using var doc = JsonDocument.Parse("{}");
        await Assert.That(() => AgentJsonDispatcher.Dispatch(host, "nope", doc.RootElement))
            .Throws<InvalidOperationException>();
    }

    static async Task RoundTrip<T>(T value) where T : class
    {
        var bytes = AgentProtocolCodec.Serialize(value);
        var back = AgentProtocolCodec.Deserialize<T>(bytes);
        await Assert.That(back).IsNotNull();
    }

    [Test]
    public async Task ProtocolCodec_deserialize_readonly_memory()
    {
        var hello = new AgentHello { AppId = "mem", SurfaceId = "s1" };
        var bytes = AgentProtocolCodec.Serialize(hello);
        var back = AgentProtocolCodec.Deserialize<AgentHello>(bytes.AsMemory());
        await Assert.That(back.AppId).IsEqualTo("mem");
    }
}


