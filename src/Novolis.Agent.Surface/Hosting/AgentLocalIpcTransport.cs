using Novolis.Agent.Core;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Surface;

/// <summary>Hosts <c>agent.*</c> request/response over local IPC (named pipe / Unix socket) and fans out events.</summary>
public sealed class AgentLocalIpcTransport : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly AgentSurfaceDefinition _definition;
    private readonly LocalIpcEndpoint _endpoint;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private readonly object _subscribersGate = new();
    private readonly HashSet<ILocalIpcConnection> _subscribers = new();
    private long _eventSequence;
    private ILocalIpcListener? _listener;

    private AgentLocalIpcTransport(IAgentHost host, AgentSurfaceDefinition definition, LocalIpcEndpoint endpoint)
    {
        _host = host;
        _definition = definition;
        _endpoint = endpoint;
        _host.Decision += OnDecision;
        _host.Changed += OnChanged;
        _host.ActionResult += OnActionResult;
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    public string Kind => "local-ipc";

    public static AgentLocalIpcTransport Attach(IAgentHost host, AgentSurfaceDefinition definition, string? endpointAddress = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        var endpoint = AgentEndpoints.CreateIpcEndpoint(definition, endpointAddress);
        return new AgentLocalIpcTransport(host, definition, endpoint);
    }

    public static AgentLocalIpcTransport? TryAttachFromEnvironment(
        IAgentHost host,
        AgentSurfaceDefinition definition,
        string? preferredAddress = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.IsIpcEnabledByEnvironment() ? Attach(host, definition, preferredAddress) : null;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public async ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        await DisposeAsync().ConfigureAwait(false);

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllTextAsync(
                    _definition.IpcMarkerPath,
                    $"{Environment.ProcessId}\n{_endpoint.Kind}\n{_endpoint.Address}\n",
                    cancellationToken)
                .ConfigureAwait(false);

            _listener = LocalIpcTransport.CreateListener(_endpoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnectionAsync(connection, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            try
            {
                await File.WriteAllTextAsync(_definition.IpcMarkerPath + ".error", ex.ToString(), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleConnectionAsync(ILocalIpcConnection connection, CancellationToken cancellationToken)
    {
        await using (connection)
        {
            try
            {
                await foreach (var frame in connection.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (frame.Kind != AgentFrameKinds.Request)
                        continue;

                    try
                    {
                        await DispatchAsync(connection, frame, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await ReplyFaultAsync(connection, frame, ex.Message, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // connection closed
            }
            finally
            {
                lock (_subscribersGate)
                {
                    _subscribers.Remove(connection);
                }
            }
        }
    }

    private async Task DispatchAsync(ILocalIpcConnection connection, LocalIpcFrame frame, CancellationToken cancellationToken)
    {
        if (AgentMethodNames.IsHello(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentHelloRequest>(frame.Payload);
            var res = _host.Hello();
            res.Sequence = req.Sequence;
            await ReplyAsync(connection, frame, res, cancellationToken).ConfigureAwait(false);
        }
        else if (AgentMethodNames.IsSnapshot(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentSnapshotRequest>(frame.Payload);
            var res = _host.Snapshot();
            res.Sequence = req.Sequence;
            await ReplyAsync(connection, frame, res, cancellationToken).ConfigureAwait(false);
        }
        else if (AgentMethodNames.IsActions(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentActionsRequest>(frame.Payload);
            var res = _host.Actions();
            res.Sequence = req.Sequence;
            await ReplyAsync(connection, frame, res, cancellationToken).ConfigureAwait(false);
        }
        else if (AgentMethodNames.IsCommand(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentCommandRequest>(frame.Payload);
            var res = _host.Execute(req.Command);
            res.Sequence = req.Sequence;
            await ReplyAsync(connection, frame, res, cancellationToken).ConfigureAwait(false);
        }
        else if (AgentMethodNames.IsContinue(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentContinueRequest>(frame.Payload);
            var res = _host.Continue();
            res.Sequence = req.Sequence;
            await ReplyAsync(connection, frame, res, cancellationToken).ConfigureAwait(false);
        }
        else if (AgentMethodNames.IsSubscribe(frame.Name))
        {
            var req = AgentProtocolCodec.Deserialize<AgentSubscribeRequest>(frame.Payload);
            _host.Subscribe();
            lock (_subscribersGate)
            {
                _subscribers.Add(connection);
            }

            await ReplyAsync(
                    connection,
                    frame,
                    new AgentSubscribeResponse { Sequence = req.Sequence, Ok = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyFaultAsync(connection, frame, $"Unknown method {frame.Name}", cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask ReplyAsync<T>(
        ILocalIpcConnection connection,
        LocalIpcFrame request,
        T payload,
        CancellationToken cancellationToken) =>
        connection.SendMessageAsync(request.Sequence, AgentFrameKinds.Response, request.Name, payload, cancellationToken);

    private static ValueTask ReplyFaultAsync(
        ILocalIpcConnection connection,
        LocalIpcFrame request,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendMessageAsync(
            request.Sequence,
            AgentFrameKinds.Fault,
            request.Name,
            new AgentFault { Sequence = request.Sequence, Message = message },
            cancellationToken);

    private void OnDecision(AgentDecisionEvent evt)
    {
        evt.Sequence = NextEventSequence();
        Broadcast(AgentMethodNames.Decision, evt);
    }

    private void OnChanged(AgentChangedEvent evt)
    {
        evt.Sequence = NextEventSequence();
        Broadcast(AgentMethodNames.Changed, evt);
    }

    private void OnActionResult(AgentActionResultEvent evt)
    {
        evt.Sequence = NextEventSequence();
        Broadcast(AgentMethodNames.ActionResult, evt);
    }

    private long NextEventSequence() => Interlocked.Increment(ref _eventSequence);

    private void Broadcast<T>(string name, T payload)
    {
        byte[] bytes;
        try
        {
            bytes = AgentProtocolCodec.Serialize(payload);
        }
        catch
        {
            return;
        }

        List<ILocalIpcConnection> targets;
        lock (_subscribersGate)
        {
            targets = _subscribers.ToList();
        }

        foreach (var connection in targets)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await connection.SendAsync(
                            new LocalIpcFrame(0, AgentFrameKinds.Event, name, bytes),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    lock (_subscribersGate)
                    {
                        _subscribers.Remove(connection);
                    }
                }
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        _host.Decision -= OnDecision;
        _host.Changed -= OnChanged;
        _host.ActionResult -= OnActionResult;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_listener is not null)
            await _listener.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _listenTask.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        _cts.Dispose();
        try { File.Delete(_definition.IpcMarkerPath); } catch { /* ignore */ }
    }
}
