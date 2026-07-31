using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>Raw TCP JSONL host — same line protocol as <see cref="AgentStdioHost"/>.</summary>
public sealed class AgentTcpJsonlHost : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly AgentSurfaceDefinition _definition;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly object _eventGate = new();
    private readonly List<StreamWriter> _eventWriters = new();
    private readonly Action<AgentDecisionEvent> _onDecision;
    private readonly Action<AgentChangedEvent> _onChanged;
    private readonly Action<AgentActionResultEvent> _onActionResult;

    private AgentTcpJsonlHost(IAgentHost host, AgentSurfaceDefinition definition, int port)
    {
        _host = host;
        _definition = definition;
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _onDecision = e => Broadcast(AgentMethodNames.Decision, e);
        _onChanged = e => Broadcast(AgentMethodNames.Changed, e);
        _onActionResult = e => Broadcast(AgentMethodNames.ActionResult, e);
        _host.Decision += _onDecision;
        _host.Changed += _onChanged;
        _host.ActionResult += _onActionResult;
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
        try
        {
            File.WriteAllText(_definition.TcpMarkerPath, $"{Environment.ProcessId}\n{port}\n");
        }
        catch
        {
            // ignore
        }
    }

    public string Kind => "tcp-jsonl";

    public int Port { get; }

    public static AgentTcpJsonlHost Attach(IAgentHost host, AgentSurfaceDefinition definition, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        return new AgentTcpJsonlHost(host, definition, port ?? definition.ResolveTcpPort());
    }

    public static AgentTcpJsonlHost? TryAttachFromEnvironment(IAgentHost host, AgentSurfaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.IsTcpEnabledByEnvironment() ? Attach(host, definition) : null;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public async ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        await DisposeAsync().ConfigureAwait(false);

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            try
            {
                await File.WriteAllTextAsync(_definition.TcpMarkerPath + ".error", ex.ToString(), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) break;
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0L;

                        if (AgentMethodNames.IsSubscribe(method))
                        {
                            lock (_eventGate)
                            {
                                _eventWriters.Add(writer);
                            }
                        }

                        var result = AgentJsonDispatcher.Dispatch(_host, method, root);
                        var payload = JsonSerializer.Serialize(new { id, ok = true, result }, AgentJson.Options);
                        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var payload = JsonSerializer.Serialize(new { ok = false, error = ex.Message }, AgentJson.Options);
                        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch
            {
                // client closed
            }
            finally
            {
                lock (_eventGate)
                {
                    _eventWriters.Remove(writer);
                }
            }
        }
    }

    private void Broadcast(string eventName, object payload)
    {
        string line;
        try
        {
            line = JsonSerializer.Serialize(new { eventName, payload }, AgentJson.Options);
        }
        catch
        {
            return;
        }

        List<StreamWriter> writers;
        lock (_eventGate)
        {
            writers = _eventWriters.ToList();
        }

        foreach (var w in writers)
        {
            try { w.WriteLine(line); }
            catch
            {
                lock (_eventGate) { _eventWriters.Remove(w); }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _host.Decision -= _onDecision;
        _host.Changed -= _onChanged;
        _host.ActionResult -= _onActionResult;
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { /* ignore */ }
        try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        _cts.Dispose();
        try { File.Delete(_definition.TcpMarkerPath); } catch { /* ignore */ }
    }
}
