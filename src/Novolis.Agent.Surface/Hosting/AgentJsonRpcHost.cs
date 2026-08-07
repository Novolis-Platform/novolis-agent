using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>TCP JSON-RPC 2.0 host (<c>jsonrpc</c> / <c>method</c> / <c>params</c> / <c>id</c>); pushes events as notifications.</summary>
public sealed class AgentJsonRpcHost : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly AgentSurfaceDefinition _definition;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly object _clientsGate = new();
    private readonly List<StreamWriter> _clients = new();
    private readonly Action<AgentDecisionEvent> _onDecision;
    private readonly Action<AgentChangedEvent> _onChanged;
    private readonly Action<AgentActionResultEvent> _onActionResult;

    private AgentJsonRpcHost(IAgentHost host, AgentSurfaceDefinition definition, int port)
    {
        _host = host;
        _definition = definition;
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _onDecision = e => Notify(AgentMethodNames.Decision, e);
        _onChanged = e => Notify(AgentMethodNames.Changed, e);
        _onActionResult = e => Notify(AgentMethodNames.ActionResult, e);
        _host.Decision += _onDecision;
        _host.Changed += _onChanged;
        _host.ActionResult += _onActionResult;
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
        try
        {
            File.WriteAllText(_definition.RpcMarkerPath, $"{Environment.ProcessId}\n{port}\n");
        }
        catch
        {
            // ignore
        }
    }

    public string Kind => "json-rpc";

    public int Port { get; }

    public static AgentJsonRpcHost Attach(IAgentHost host, AgentSurfaceDefinition definition, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        return new AgentJsonRpcHost(host, definition, port ?? definition.ResolveRpcPort());
    }

    public static AgentJsonRpcHost? TryAttachFromEnvironment(IAgentHost host, AgentSurfaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.IsRpcEnabledByEnvironment() ? Attach(host, definition) : null;
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
                await File.WriteAllTextAsync(_definition.RpcMarkerPath + ".error", ex.ToString(), CancellationToken.None)
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
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    await HandleRequestAsync(writer, line, cancellationToken).ConfigureAwait(false);
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
                lock (_clientsGate)
                {
                    _clients.Remove(writer);
                }
            }
        }
    }

    private async Task HandleRequestAsync(StreamWriter writer, string line, CancellationToken cancellationToken)
    {
        JsonElement? id = null;
        var parsed = false;
        try
        {
            using var doc = JsonDocument.Parse(line);
            parsed = true;
            var root = doc.RootElement;
            id = root.TryGetProperty("id", out var idEl) ? idEl.Clone() : null;
            var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            var paramsEl = root.TryGetProperty("params", out var p) ? p : default;

            if (AgentMethodNames.IsSubscribe(method))
            {
                lock (_clientsGate)
                {
                    _clients.Add(writer);
                }
            }

            var target = paramsEl.ValueKind == JsonValueKind.Undefined ? root : paramsEl;
            var result = AgentJsonDispatcher.Dispatch(_host, method, target);

            if (id is null)
                return;

            var reply = JsonSerializer.Serialize(new { jsonrpc = "2.0", result, id }, AgentJson.Options);
            await writer.WriteLineAsync(reply.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (parsed && id is null)
                return;

            var error = new { code = -32603, message = ex.Message };
            var replyId = id ?? JsonSerializer.SerializeToElement(0);
            var reply = JsonSerializer.Serialize(new { jsonrpc = "2.0", error, id = replyId }, AgentJson.Options);
            try { await writer.WriteLineAsync(reply.AsMemory(), cancellationToken).ConfigureAwait(false); }
            catch { /* ignore */ }
        }
    }

    private void Notify(string method, object payload)
    {
        string line;
        try
        {
            line = JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = payload }, AgentJson.Options);
        }
        catch
        {
            return;
        }

        List<StreamWriter> targets;
        lock (_clientsGate)
        {
            targets = _clients.ToList();
        }

        foreach (var w in targets)
        {
            try { w.WriteLine(line); }
            catch
            {
                lock (_clientsGate) { _clients.Remove(w); }
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
        lock (_clientsGate) { _clients.Clear(); }
        try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        _cts.Dispose();
        try { File.Delete(_definition.RpcMarkerPath); } catch { /* ignore */ }
    }
}
