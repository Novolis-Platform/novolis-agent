namespace Novolis.Agent.Unit;

static class BoundedReads
{
    public static async Task<string?> ReadLineAsync(this TextReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s waiting for a line.");
        }
    }
}
