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
