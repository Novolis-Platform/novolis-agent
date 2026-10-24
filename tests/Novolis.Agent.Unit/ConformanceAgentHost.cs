using Novolis.Agent.Core;

namespace Novolis.Agent.Unit;

public sealed class ConformanceAgentHost : Novolis.Agent.Core.IAgentHost
{
    private long _revision = 1;
    private readonly Dictionary<string, AgentCommandResult> _done = new(StringComparer.Ordinal);

    public string Value { get; private set; } = "a";

    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;

    public AgentHello Hello() => new() { ProtocolVersion = "1.0", AppId = "conformance" };

    public AgentSnapshot Snapshot() => new()
    {
        StateRevision = _revision,
        MetadataVersion = AgentMetadata.Version,
        StatusLines = new Dictionary<string, string> { ["value"] = Value },
    };

    public AgentActionsResponse Actions() => new()
    {
        Actions =
        [
            new AgentAction
            {
                Id = "read",
                Label = "Read",
                Enabled = true,
                Capability = "fixture.read",
                SafetyClass = AgentSafetyClass.Read,
                MetadataVersion = AgentMetadata.Version,
            },
            new AgentAction
            {
                Id = "write",
                Label = "Write",
                Enabled = true,
                Capability = "fixture.mutate",
                SafetyClass = AgentSafetyClass.Mutate,
                ApprovalRequired = true,
                Idempotency = "at-most-once",
                EffectPreview = "Replace the fixture value.",
                MetadataVersion = AgentMetadata.Version,
                Schema = AgentActionSchema.Object(["value"], ("value", "string")),
            },
        ],
    };

    public AgentCommandResult Continue() => new() { Ok = true, ActionId = AgentActionIds.Continue, Message = "continue", StateRevision = _revision };

    public void Subscribe()
    {
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.CorrelationId) && _done.TryGetValue(command.CorrelationId, out var prior))
        {
            return prior;
        }

        var action = Actions().Actions.FirstOrDefault(candidate => candidate.Id == command.ActionId);
        var rejected = AgentCommandGate.Reject(action, command, _revision);
        if (rejected is not null)
        {
            Publish(rejected);
            return rejected;
        }

        AgentCommandResult result;
        if (command.ActionId == "read")
        {
            result = Done(command, true, AgentLifecycle.Applied, Value, null, mutate: false);
        }
        else if (command.ActionId == "write")
        {
            Value = command.Get("value") ?? "";
            result = Done(command, true, AgentLifecycle.Applied, Value, null, mutate: true);
        }
        else
        {
            result = Done(command, false, AgentLifecycle.Rejected, "Unknown action.", "unknown", mutate: false);
        }

        if (!string.IsNullOrWhiteSpace(result.CorrelationId))
        {
            _done[result.CorrelationId] = result;
        }

        Publish(result);
        return result;
    }

    private AgentCommandResult Done(AgentCommand command, bool ok, string lifecycle, string message, string? reason, bool mutate)
    {
        if (mutate)
        {
            _revision++;
        }

        var receipt = AgentCommandGate.CreateReceipt(command, lifecycle, ok, message, _revision, reason, ok ? null : message);
        return new AgentCommandResult
        {
            Ok = ok,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            Message = message,
            ErrorCode = reason,
            StateRevision = _revision,
            Receipt = receipt,
            Snapshot = Snapshot(),
        };
    }

    private void Publish(AgentCommandResult result)
    {
        var evt = new AgentActionResultEvent
        {
            ActionId = result.ActionId,
            Ok = result.Ok,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            CorrelationId = result.CorrelationId,
            Receipt = result.Receipt,
        };
        ActionResult?.Invoke(evt);
        Changed?.Invoke(new AgentChangedEvent { Reason = result.Ok ? "applied" : "rejected", CorrelationId = result.CorrelationId });
        Decision?.Invoke(new AgentDecisionEvent { DecisionLine = result.Message });
    }
}
