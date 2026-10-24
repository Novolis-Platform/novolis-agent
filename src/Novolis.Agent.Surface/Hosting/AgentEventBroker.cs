using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// One sequence space for a surface. Remote callbacks are events on the caller's live sink.
/// Delegates never leave the process.
/// </summary>
public sealed class AgentEventBroker : IDisposable
{
    private readonly IAgentHost _host;
    private readonly int _replayLimit;
    private readonly object _gate = new();
    private readonly List<StoredEvent> _replay = [];
    private readonly Dictionary<string, Registration> _watches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AgentQueueSink> _polls = new(StringComparer.Ordinal);
    private long _sequence;
    private bool _disposed;

    public AgentEventBroker(IAgentHost host, int replayLimit = 128)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _replayLimit = Math.Max(1, replayLimit);
        _host.Decision += OnDecision;
        _host.Changed += OnChanged;
        _host.ActionResult += OnActionResult;
    }

    public AgentWatchResponse Open(AgentWatchRequest request, IAgentEventSink sink)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);
        var error = Validate(request);
        if (error is not null)
        {
            return new AgentWatchResponse { Sequence = request.Sequence, Ok = false, Error = error, CallbackId = request.CallbackId };
        }

        _host.Subscribe();
        List<StoredEvent> replay;
        long through;
        long? oldest;
        string watchId;
        var truncated = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            through = _sequence;
            oldest = _replay.Count == 0 ? null : _replay[0].Sequence;
            if (request.AfterEventSequence is long after && oldest is long first && after < first - 1)
            {
                truncated = true;
            }

            replay = Select(request, _replay, through, replayOnly: true);
            watchId = Guid.NewGuid().ToString("n");
            _watches[watchId] = new Registration(watchId, request, sink, through);
            var limit = Math.Clamp(request.ReplayLimit ?? 32, 0, _replayLimit);
            foreach (var item in replay.Take(limit))
            {
                var envelope = ToWatch(watchId, request.CallbackId, item, isReplay: true);
                if (sink is AgentQueueSink queue)
                {
                    if (!queue.TryWrite(envelope))
                    {
                        _watches.Remove(watchId);
                        break;
                    }
                }
                else if (!sink.TryEnqueue(envelope))
                {
                    _watches.Remove(watchId);
                    break;
                }
            }
        }

        return new AgentWatchResponse
        {
            Sequence = request.Sequence,
            Ok = true,
            WatchId = watchId,
            CallbackId = request.CallbackId,
            EstablishedThroughSequence = through,
            OldestAvailableSequence = oldest,
            ReplayTruncated = truncated,
        };
    }

    public AgentWatchResponse OpenPoll(AgentWatchRequest request, int capacity = 32)
    {
        var sink = new AgentQueueSink(Guid.NewGuid().ToString("n"), capacity);
        var response = Open(request, sink);
        if (response.Ok)
        {
            lock (_gate)
            {
                _polls[response.WatchId] = sink;
            }
        }

        return response;
    }

    public IAgentWatchSubscription Watch(
        AgentWatchRequest request,
        AgentWatchCallback callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var sink = new AgentQueueSink(Guid.NewGuid().ToString("n"));
        var response = Open(request, sink);
        if (!response.Ok)
        {
            throw new InvalidOperationException(response.Error ?? "Watch failed.");
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var batch = await sink.TakeAsync(1, TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
                    foreach (var item in batch)
                    {
                        await callback(item, cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Subscription disposed.
            }
            catch
            {
                Close(response.WatchId, sink, "callback-failed");
            }
        }, CancellationToken.None);

        return new Subscription(this, response, () =>
        {
            cts.Cancel();
            Close(response.WatchId, sink, "disposed");
            return loop;
        });
    }

    public AgentUnwatchResponse Close(string watchId, IAgentEventSink? sink, string reason = "unwatch")
    {
        Registration? registration = null;
        lock (_gate)
        {
            if (_watches.TryGetValue(watchId, out var found) && (sink is null || ReferenceEquals(found.Sink, sink) || found.Sink.ConnectionId == sink.ConnectionId))
            {
                _watches.Remove(watchId);
                _polls.Remove(watchId);
                registration = found;
            }
        }

        if (registration is null)
        {
            return new AgentUnwatchResponse { Ok = false, WatchId = watchId };
        }

        var closed = new AgentWatchEvent
        {
            WatchId = watchId,
            CallbackId = registration.Request.CallbackId,
            EventSequence = registration.EstablishedThrough,
            EventName = AgentMethodNames.WatchClosed,
            OccurredUtc = DateTimeOffset.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new AgentWatchClosed { WatchId = watchId, Reason = reason }, AgentJson.Options),
        };
        registration.Sink.TryEnqueue(closed);
        return new AgentUnwatchResponse { Ok = true, WatchId = watchId };
    }

    public void CloseConnection(IAgentEventSink sink, string reason = "disconnect")
    {
        ArgumentNullException.ThrowIfNull(sink);
        string[] ids;
        lock (_gate)
        {
            ids = _watches.Where(pair => ReferenceEquals(pair.Value.Sink, sink)).Select(pair => pair.Key).ToArray();
        }

        foreach (var id in ids)
        {
            Close(id, sink, reason);
        }
    }

    public async Task<AgentWatchNextResponse> PollAsync(
        string watchId,
        int maxEvents,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        AgentQueueSink? sink;
        lock (_gate)
        {
            if (!_polls.TryGetValue(watchId, out sink)
                && _watches.TryGetValue(watchId, out var registration))
            {
                sink = registration.Sink as AgentQueueSink;
            }
        }

        if (sink is null)
        {
            return new AgentWatchNextResponse { Ok = false, WatchId = watchId, Error = "Unknown watch." };
        }

        var events = await sink.TakeAsync(maxEvents, timeout, cancellationToken).ConfigureAwait(false);
        return new AgentWatchNextResponse { Ok = true, WatchId = watchId, Events = events.ToArray() };
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _watches.Clear();
            _polls.Clear();
        }

        _host.Decision -= OnDecision;
        _host.Changed -= OnChanged;
        _host.ActionResult -= OnActionResult;
    }

    private void OnDecision(AgentDecisionEvent evt) =>
        Publish(AgentMethodNames.Decision, evt, evt.CorrelationId, evt.ResourceId ?? evt.HubId, evt.Severity);

    private void OnChanged(AgentChangedEvent evt) =>
        Publish(AgentMethodNames.Changed, evt, evt.CorrelationId, evt.ResourceId, evt.Severity);

    private void OnActionResult(AgentActionResultEvent evt) =>
        Publish(AgentMethodNames.ActionResult, evt, evt.CorrelationId, evt.ResourceId ?? evt.ActionId, evt.Severity ?? (evt.Ok ? AgentSeverity.Info : AgentSeverity.Warning));

    private void Publish(string eventName, object payload, string? correlationId, string? resourceId, string? severity)
    {
        Registration[] targets;
        StoredEvent stored;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            stored = new StoredEvent(++_sequence, eventName, JsonSerializer.Serialize(payload, AgentJson.Options), correlationId, resourceId, severity, DateTimeOffset.UtcNow);
            _replay.Add(stored);
            if (_replay.Count > _replayLimit)
            {
                _replay.RemoveRange(0, _replay.Count - _replayLimit);
            }

            targets = _watches.Values.Where(watch => Matches(watch.Request, stored) && stored.Sequence > watch.EstablishedThrough).ToArray();
        }

        foreach (var watch in targets)
        {
            var envelope = ToWatch(watch.WatchId, watch.Request.CallbackId, stored, isReplay: false);
            if (!watch.Sink.TryEnqueue(envelope))
            {
                Close(watch.WatchId, watch.Sink, "overflow");
            }
        }
    }

    private static AgentWatchEvent ToWatch(string watchId, string? callbackId, StoredEvent stored, bool isReplay) => new()
    {
        WatchId = watchId,
        CallbackId = callbackId,
        EventSequence = stored.Sequence,
        EventName = stored.EventName,
        OccurredUtc = stored.OccurredUtc,
        IsReplay = isReplay,
        CorrelationId = stored.CorrelationId,
        ResourceId = stored.ResourceId,
        Severity = stored.Severity,
        PayloadJson = stored.PayloadJson,
    };

    private static List<StoredEvent> Select(AgentWatchRequest request, List<StoredEvent> source, long through, bool replayOnly)
    {
        var after = request.AfterEventSequence ?? 0;
        return source.Where(item => item.Sequence > after && (!replayOnly || item.Sequence <= through) && Matches(request, item)).ToList();
    }

    private static bool Matches(AgentWatchRequest request, StoredEvent item)
    {
        if (request.EventNames.Length > 0
            && !request.EventNames.Any(name => string.Equals(AgentMethodNames.CanonicalEventName(name), item.EventName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.CorrelationId)
            && !string.Equals(request.CorrelationId, item.CorrelationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.ResourceId)
            && !string.Equals(request.ResourceId, item.ResourceId, StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrEmpty(request.MinSeverity) || AgentSeverity.Rank(item.Severity) >= AgentSeverity.Rank(request.MinSeverity);
    }

    private static string? Validate(AgentWatchRequest request)
    {
        foreach (var name in request.EventNames)
        {
            var canonical = AgentMethodNames.CanonicalEventName(name);
            if (canonical is not (AgentMethodNames.Decision or AgentMethodNames.Changed or AgentMethodNames.ActionResult))
            {
                return $"Unknown event '{name}'.";
            }
        }

        return null;
    }

    private sealed record StoredEvent(
        long Sequence,
        string EventName,
        string PayloadJson,
        string? CorrelationId,
        string? ResourceId,
        string? Severity,
        DateTimeOffset OccurredUtc);

    private sealed class Registration(string watchId, AgentWatchRequest request, IAgentEventSink sink, long establishedThrough)
    {
        public string WatchId { get; } = watchId;
        public AgentWatchRequest Request { get; } = request;
        public IAgentEventSink Sink { get; } = sink;
        public long EstablishedThrough { get; } = establishedThrough;
    }

    private sealed class Subscription : IAgentWatchSubscription
    {
        private readonly AgentEventBroker _broker;
        private readonly Func<Task> _dispose;
        private int _disposed;

        public Subscription(AgentEventBroker broker, AgentWatchResponse response, Func<Task> dispose)
        {
            _broker = broker;
            _dispose = dispose;
            WatchId = response.WatchId;
            EstablishedThroughSequence = response.EstablishedThroughSequence;
        }

        public string WatchId { get; }

        public long EstablishedThroughSequence { get; }

        public async Task<AgentWatchEvent?> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var response = await _broker.PollAsync(WatchId, 1, timeout, cancellationToken).ConfigureAwait(false);
            return response.Events.FirstOrDefault();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await _dispose().ConfigureAwait(false);
        }
    }
}
