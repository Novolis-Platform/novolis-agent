using Novolis.Agent.Core;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Surface;

/// <summary>Client for <c>agent.*</c> over local-IPC (MessagePack) frames.</summary>
public sealed class AgentLocalIpcClient : IAsyncDisposable
{
    private readonly ILocalIpcConnection _connection;
    private long _sequence;
    private readonly object _gate = new();
    private readonly Dictionary<long, TaskCompletionSource<LocalIpcFrame>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readLoop;

    public event Action<string, byte[]>? EventReceived;

    private AgentLocalIpcClient(ILocalIpcConnection connection)
    {
        _connection = connection;
        _readLoop = Task.Run(ReadLoopAsync);
    }

    public static async Task<AgentLocalIpcClient> ConnectAsync(
        LocalIpcEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var client = LocalIpcTransport.CreateClient();
        var connection = await client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        return new AgentLocalIpcClient(connection);
    }

    public async Task<AgentHello> HelloAsync(CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var frame = await RequestAsync(seq, AgentMethodNames.Hello, new AgentHelloRequest { Sequence = seq }, cancellationToken)
            .ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentHello>(frame.Payload);
    }

    public async Task<AgentSnapshot> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var frame = await RequestAsync(seq, AgentMethodNames.Snapshot, new AgentSnapshotRequest { Sequence = seq }, cancellationToken)
            .ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentSnapshot>(frame.Payload);
    }

    public async Task<AgentActionsResponse> ActionsAsync(CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var frame = await RequestAsync(seq, AgentMethodNames.Actions, new AgentActionsRequest { Sequence = seq }, cancellationToken)
            .ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentActionsResponse>(frame.Payload);
    }

    public async Task<AgentCommandResult> CommandAsync(AgentCommand command, CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var request = new AgentCommandRequest { Sequence = seq, Command = command };
        var frame = await RequestAsync(seq, AgentMethodNames.Command, request, cancellationToken).ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentCommandResult>(frame.Payload);
    }

    public async Task<AgentCommandResult> ContinueAsync(CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var frame = await RequestAsync(seq, AgentMethodNames.Continue, new AgentContinueRequest { Sequence = seq }, cancellationToken)
            .ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentCommandResult>(frame.Payload);
    }

    public async Task<AgentSubscribeResponse> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        var seq = NextSequence();
        var frame = await RequestAsync(seq, AgentMethodNames.Subscribe, new AgentSubscribeRequest { Sequence = seq }, cancellationToken)
            .ConfigureAwait(false);
        return AgentProtocolCodec.Deserialize<AgentSubscribeResponse>(frame.Payload);
    }

    private long NextSequence()
    {
        lock (_gate)
        {
            return ++_sequence;
        }
    }

    private async Task<LocalIpcFrame> RequestAsync<T>(
        long sequence,
        string method,
        T payload,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<LocalIpcFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _pending[sequence] = tcs;
        }

        await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        await _connection.SendMessageAsync(sequence, AgentFrameKinds.Request, method, payload, cancellationToken)
            .ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            await foreach (var frame in _connection.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                if (frame.Kind is AgentFrameKinds.Response or AgentFrameKinds.Fault)
                {
                    TaskCompletionSource<LocalIpcFrame>? tcs;
                    lock (_gate)
                    {
                        _pending.Remove(frame.Sequence, out tcs);
                    }

                    if (frame.Kind == AgentFrameKinds.Fault)
                    {
                        var fault = AgentProtocolCodec.Deserialize<AgentFault>(frame.Payload);
                        tcs?.TrySetException(new InvalidOperationException(fault.Message));
                    }
                    else
                    {
                        tcs?.TrySetResult(frame);
                    }
                }
                else if (frame.Kind == AgentFrameKinds.Event)
                {
                    EventReceived?.Invoke(frame.Name, frame.Payload);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                foreach (var tcs in _pending.Values)
                {
                    tcs.TrySetException(ex);
                }

                _pending.Clear();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _readLoop.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
