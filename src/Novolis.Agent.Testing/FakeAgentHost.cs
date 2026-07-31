using System.Threading.Channels;
using Novolis.Agent.Core;

namespace Novolis.Agent.Testing;

/// <summary>In-memory duplex channel for unit tests.</summary>
public sealed class InMemoryAgentChannel : IAgentChannel
{
    private readonly Channel<AgentFrame> _inbound = Channel.CreateUnbounded<AgentFrame>();
    private readonly Channel<AgentFrame> _outbound = Channel.CreateUnbounded<AgentFrame>();

    public string TransportKind => "in-memory";

    /// <summary>Frames the host side reads (client writes here via <see cref="EnqueueInbound"/>).</summary>
    public ChannelWriter<AgentFrame> InboundWriter => _inbound.Writer;

    /// <summary>Frames the host side sends (tests read via <see cref="ReadOutboundAsync"/>).</summary>
    public ChannelReader<AgentFrame> OutboundReader => _outbound.Reader;

    public void EnqueueInbound(AgentFrame frame) =>
        _inbound.Writer.TryWrite(frame);

    public async IAsyncEnumerable<AgentFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _inbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return frame;
    }

    public ValueTask SendAsync(AgentFrame frame, CancellationToken cancellationToken = default) =>
        _outbound.Writer.WriteAsync(frame, cancellationToken);

    public async Task<AgentFrame> ReadOutboundAsync(CancellationToken cancellationToken = default) =>
        await _outbound.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        _outbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

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
