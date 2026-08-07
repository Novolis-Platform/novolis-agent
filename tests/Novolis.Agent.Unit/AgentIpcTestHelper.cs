using System.Net.Sockets;
using Novolis.Agent.Surface;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Unit;

static class AgentIpcTestHelper
{
    static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        await Gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            Gate.Release();
        }
    }

    public static Task RunAsync(Func<Task> action) =>
        RunAsync(async () =>
        {
            await action();
            return true;
        });

    public static async Task<AgentLocalIpcClient> ConnectAsync(LocalIpcEndpoint endpoint)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Exception? lastError = null;
        while (!timeout.IsCancellationRequested)
        {
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            attempt.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                return await AgentLocalIpcClient.ConnectAsync(endpoint, attempt.Token);
            }
            catch (Exception ex) when (
                !timeout.IsCancellationRequested
                && ex is IOException or SocketException or OperationCanceledException)
            {
                lastError = ex;
            }

            await DelayBeforeRetryAsync(timeout.Token);
        }

        throw new TimeoutException($"IPC listener not ready after 30 seconds: {endpoint.Address}", lastError);
    }

    public static async Task<ILocalIpcConnection> ConnectConnectionAsync(LocalIpcEndpoint endpoint)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var transport = LocalIpcTransport.CreateClient();
        Exception? lastError = null;
        while (!timeout.IsCancellationRequested)
        {
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            attempt.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                return await transport.ConnectAsync(endpoint, attempt.Token);
            }
            catch (Exception ex) when (
                !timeout.IsCancellationRequested
                && ex is IOException or SocketException or OperationCanceledException)
            {
                lastError = ex;
            }

            await DelayBeforeRetryAsync(timeout.Token);
        }

        throw new TimeoutException($"IPC listener not ready after 30 seconds: {endpoint.Address}", lastError);
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(50, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caller reports a stable TimeoutException below.
        }
    }
}

