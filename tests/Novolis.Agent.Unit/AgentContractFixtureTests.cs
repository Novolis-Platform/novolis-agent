using MessagePack;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentContractFixtureTests
{
    [Test]
    public async Task Legacy_aliases_and_v1_payloads_keep_their_shape()
    {
        await Assert.That(AgentMethodNames.IsHello("session.hello")).IsTrue();
        await Assert.That(AgentMethodNames.IsCommand("command")).IsTrue();
        await Assert.That(AgentMethodNames.CanonicalEventName("session.changed")).IsEqualTo(AgentMethodNames.Changed);
        await Assert.That(AgentMethodNames.IsWatch("watch")).IsTrue();

        var v1 = new V1Command { ActionId = "ping", Params = new Dictionary<string, string> { ["x"] = "1" } };
        var modern = AgentProtocolCodec.Deserialize<AgentCommand>(AgentProtocolCodec.Serialize(v1));
        await Assert.That(modern.ActionId).IsEqualTo("ping");
        await Assert.That(modern.Get("x")).IsEqualTo("1");
        await Assert.That(modern.CorrelationId).IsNull();

        var full = new AgentCommand { ActionId = "ping", CorrelationId = "c1", ExpectedRevision = 4, ApprovalToken = "t" };
        var back = AgentProtocolCodec.Deserialize<V1Command>(AgentProtocolCodec.Serialize(full));
        await Assert.That(back.ActionId).IsEqualTo("ping");

        const string json = """{"ok":true,"actionId":"ping","message":"ok","sequence":1}""";
        var result = System.Text.Json.JsonSerializer.Deserialize<AgentCommandResult>(json, AgentJson.Options);
        await Assert.That(result!.Ok).IsTrue();
        await Assert.That(result.Message).IsEqualTo("ok");
        await Assert.That(result.Receipt).IsNull();
        await Assert.That(result.StateRevision).IsNull();
        await Assert.That(result.CorrelationId).IsNull();
    }

    [Test]
    public async Task Schema_validator_accepts_legacy_string_numbers()
    {
        var schema = AgentActionSchema.Object(["count"], ("count", "integer"), ("force", "boolean"));
        var command = new AgentCommand { ArgumentsJson = """{"count":"3","force":"true"}""" };
        var ok = AgentSchemaValidator.TryValidate(schema, AgentCommandJson.Instance(command), out var error);
        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsEmpty();
    }

    [Test]
    public async Task Conformance_host_preserves_approval_revision_and_replay()
    {
        var host = new ConformanceAgentHost();
        var read = host.Execute(new AgentCommand { ActionId = "read" });
        await Assert.That(read.Ok).IsTrue();
        await Assert.That(host.Snapshot().StateRevision).IsEqualTo(1);

        var blocked = host.Execute(new AgentCommand { ActionId = "write" }.With("value", "b"));
        await Assert.That(blocked.ErrorCode).IsEqualTo("approval-required");
        await Assert.That(host.Snapshot().StateRevision).IsEqualTo(1);
        await Assert.That(host.Value).IsEqualTo("a");

        var stale = host.Execute(new AgentCommand
        {
            ActionId = "write",
            ApprovalToken = "once",
            ExpectedRevision = 9,
        }.With("value", "b"));
        await Assert.That(stale.ErrorCode).IsEqualTo("stale-snapshot");
        await Assert.That(host.Value).IsEqualTo("a");

        var applied = host.Execute(new AgentCommand
        {
            ActionId = "write",
            CorrelationId = "once",
            ApprovalToken = "once",
            ExpectedRevision = 1,
        }.With("value", "b"));
        await Assert.That(applied.Ok).IsTrue();
        await Assert.That(applied.Receipt!.Lifecycle).IsEqualTo(AgentLifecycle.Applied);
        await Assert.That(host.Value).IsEqualTo("b");
        await Assert.That(host.Snapshot().StateRevision).IsEqualTo(2);

        var replay = host.Execute(new AgentCommand { ActionId = "write", CorrelationId = "once" });
        await Assert.That(replay.Message).IsEqualTo(applied.Message);
        await Assert.That(host.Snapshot().StateRevision).IsEqualTo(2);

        var scenario = await AgentScenarioRunner.RunAsync(host, new AgentScenario
        {
            Name = "conformance",
            Steps = [new AgentScenarioStep { ActionId = "read", ExpectOk = true }],
        });
        await Assert.That(scenario.Passed).IsTrue();
        await Assert.That(scenario.EvidenceComplete).IsTrue();
    }

    [MessagePackObject]
    public sealed class V1Command
    {
        [Key(0)] public string ActionId { get; set; } = "";
        [Key(1)] public Dictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);
    }
}
