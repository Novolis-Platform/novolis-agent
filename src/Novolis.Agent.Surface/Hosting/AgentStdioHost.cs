using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// Stdio JSONL host: one JSON object per line with method + args. Headless agents share the same
/// <c>agent.*</c> protocol as local-IPC / HTTP / TCP.
/// </summary>
public sealed class AgentStdioHost : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AgentStdioHost(IAgentHost host, TextReader? input = null, TextWriter? output = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public string Kind => "stdio-jsonl";

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* ignore */ }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    public async Task RunUntilEofAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(cancellationToken).ConfigureAwait(false);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0L;
                var result = AgentJsonDispatcher.Dispatch(_host, method, root);
                await WriteAsync(new { id, ok = true, result }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await WriteAsync(new { ok = false, error = ex.Message }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task WriteAsync(object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, AgentJson.Options);
        await _output.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
