using System.Threading.Channels;
using Novolis.Agent.Core;

namespace Novolis.Agent.Testing;

/// <summary>Minimal mutable <see cref="IAgentHost"/> for tests.</summary>
public sealed class FakeAgentHost : IAgentHost
{
    public AgentHello HelloResponse { get; set; } = new()
    {
        ProtocolVersion = "1.0",
        AppId = "fake",
        ProcessId = Environment.ProcessId,
        Capabilities = [AgentMethodNames.Hello, AgentMethodNames.Snapshot, AgentMethodNames.Command],
    };

    public AgentSnapshot SnapshotResponse { get; set; } = new();

    public AgentActionsResponse ActionsResponse { get; set; } = new()
    {
        Actions = [new AgentAction { Id = "ping", Label = "Ping", Summary = "Ping", Enabled = true }],
    };

    public List<AgentCommand> Executed { get; } = [];

    public int SubscribeCount { get; private set; }

    public int ContinueCount { get; private set; }

    public event Action<AgentDecisionEvent>? Decision;

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public AgentHello Hello() => HelloResponse;

    public AgentSnapshot Snapshot() => SnapshotResponse;

    public AgentActionsResponse Actions() => ActionsResponse;

    public AgentCommandResult Execute(AgentCommand command)
    {
        Executed.Add(command);
        return new AgentCommandResult
        {
            Ok = true,
            ActionId = command.ActionId,
            Message = "ok",
            Snapshot = SnapshotResponse,
        };
    }

    public AgentCommandResult Continue()
    {
        ContinueCount++;
        return new AgentCommandResult { Ok = true, ActionId = AgentActionIds.Continue, Message = "continued" };
    }

    public void Subscribe() => SubscribeCount++;

    public void RaiseChanged(string reason = "changed") =>
        Changed?.Invoke(new AgentChangedEvent { Reason = reason, Snapshot = SnapshotResponse });

    public void RaiseDecision(string line = "decide") =>
        Decision?.Invoke(new AgentDecisionEvent { DecisionLine = line, Snapshot = SnapshotResponse });

    public void RaiseActionResult(string actionId, bool ok = true) =>
        ActionResult?.Invoke(new AgentActionResultEvent { ActionId = actionId, Ok = ok, Message = "done" });
}
