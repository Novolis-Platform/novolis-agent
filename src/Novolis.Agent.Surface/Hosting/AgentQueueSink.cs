using System.Threading.Channels;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

public sealed class AgentQueueSink : IAgentEventSink
{
    private readonly Channel<AgentWatchEvent> _channel;

    public AgentQueueSink(string connectionId, int capacity = 32, Action<AgentWatchEvent>? onEnqueued = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ConnectionId = connectionId;
        OnEnqueued = onEnqueued;
        _channel = Channel.CreateBounded<AgentWatchEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
        Capacity = capacity;
    }

    public string ConnectionId { get; }

    public int Capacity { get; }

    public Action<AgentWatchEvent>? OnEnqueued { get; set; }

    public int Pending => _channel.Reader.Count;

    public bool TryEnqueue(AgentWatchEvent value)
    {
        if (!TryWrite(value))
        {
            return false;
        }

        Notify(value);
        return true;
    }

    /// <summary>Queues without invoking <see cref="OnEnqueued"/>. Callers inside the broker lock use this.</summary>
    public bool TryWrite(AgentWatchEvent value) => _channel.Writer.TryWrite(value);

    public void Notify(AgentWatchEvent value)
    {
        try
        {
            OnEnqueued?.Invoke(value);
        }
        catch
        {
            // Push observers must not fail a watch that already queued the event.
        }
    }

    public async Task<IReadOnlyList<AgentWatchEvent>> TakeAsync(
        int max,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(1, max);
        var list = new List<AgentWatchEvent>(limit);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            while (list.Count < limit)
            {
                while (list.Count < limit && _channel.Reader.TryRead(out var ready))
                {
                    list.Add(ready);
                }

                if (list.Count > 0 || !await _channel.Reader.WaitToReadAsync(timeoutSource.Token).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or caller cancellation returns whatever already arrived.
        }

        return list;
    }
}
