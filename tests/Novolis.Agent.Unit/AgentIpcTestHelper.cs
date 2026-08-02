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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (var i = 0; i < 300; i++)
        {
            try
            {
                return await AgentLocalIpcClient.ConnectAsync(endpoint, cts.Token);
            }
            catch (Exception) when (i < 299)
            {
                await Task.Delay(20, cts.Token);
            }
        }

        throw new TimeoutException($"IPC listener not ready: {endpoint.Address}");
    }

    public static async Task<ILocalIpcConnection> ConnectConnectionAsync(LocalIpcEndpoint endpoint)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = LocalIpcTransport.CreateClient();
        for (var i = 0; i < 300; i++)
        {
            try
            {
                return await transport.ConnectAsync(endpoint, cts.Token);
            }
            catch (Exception) when (i < 299)
            {
                await Task.Delay(20, cts.Token);
            }
        }

        throw new TimeoutException($"IPC listener not ready: {endpoint.Address}");
    }
}

