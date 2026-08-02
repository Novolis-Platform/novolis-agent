using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// Super-slim localhost HTTP surface for <c>agent.*</c> — no Kestrel. REST JSON, SSE events, and a JSON WebSocket
/// duplex channel at <c>/agent/ws</c>, so agents can talk to one EXE over one port.
/// </summary>
public sealed class AgentHttpHost : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly AgentSurfaceDefinition _definition;
    private readonly AgentSurfaceDocument _document;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly object _fanoutGate = new();
    private readonly List<StreamWriter> _sseClients = new();
    private readonly List<WebSocket> _wsClients = new();
    private readonly Action<AgentDecisionEvent> _onDecision;
    private readonly Action<AgentChangedEvent> _onChanged;
    private readonly Action<AgentActionResultEvent> _onActionResult;
    private long _eventSequence;

    private AgentHttpHost(IAgentHost host, AgentSurfaceDefinition definition, string prefix, int port)
    {
        _host = host;
        _definition = definition;
        _document = AgentSurfaceDocument.From(definition, port, definition.ResolveTcpPort(), definition.ResolveRpcPort());
        BaseUrl = prefix.TrimEnd('/');
        var listenPrefix = prefix.EndsWith('/') ? prefix : prefix + "/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(listenPrefix);
        _onDecision = e => Broadcast(AgentMethodNames.Decision, e);
        _onChanged = e => Broadcast(AgentMethodNames.Changed, e);
        _onActionResult = e => Broadcast(AgentMethodNames.ActionResult, e);
        _host.Decision += _onDecision;
        _host.Changed += _onChanged;
        _host.ActionResult += _onActionResult;
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
        WriteMarkers();
    }

    public string Kind => "http";

    public string BaseUrl { get; }

    public string WebSocketUrl =>
        (BaseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" + BaseUrl[5..] : "ws" + BaseUrl[4..])
        + "/agent/ws";

    public static AgentHttpHost Attach(IAgentHost host, AgentSurfaceDefinition definition, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definition);
        var p = port ?? definition.ResolveHttpPort();
        return new AgentHttpHost(host, definition, $"http://127.0.0.1:{p}/", p);
    }

    public static AgentHttpHost? TryAttachFromEnvironment(IAgentHost host, AgentSurfaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.IsHttpEnabledByEnvironment() ? Attach(host, definition) : null;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public async ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        await DisposeAsync().ConfigureAwait(false);

    private void WriteMarkers()
    {
        try
        {
            File.WriteAllText(_definition.HttpMarkerPath, $"{Environment.ProcessId}\n{BaseUrl}\n");
        }
        catch
        {
            // ignore marker failures
        }

        try
        {
            File.WriteAllText(_definition.WsMarkerPath, $"{Environment.ProcessId}\n{WebSocketUrl}\n");
        }
        catch
        {
            // ignore marker failures
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
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
                await File.WriteAllTextAsync(_definition.HttpMarkerPath + ".error", ex.ToString(), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var req = context.Request;
            var rawPath = (req.Url?.AbsolutePath ?? "/").TrimEnd('/');
            if (rawPath.Length == 0)
                rawPath = "/";

            if (req.HttpMethod == "OPTIONS")
            {
                WriteCors(context.Response);
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (rawPath is "/health" or "/agent/health" or "/session/health")
            {
                await WriteJsonAsync(
                        context.Response,
                        200,
                        new { ok = true, transport = Kind, baseUrl = BaseUrl, surfaceId = _definition.SurfaceId },
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var path = NormalizePath(rawPath);

            if (path == "/agent/ws" && req.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (path == "/agent/events" && req.HttpMethod == "GET")
            {
                await HandleSseAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            object result;
            if (path == "/agent/document" && req.HttpMethod == "GET")
                result = JsonSerializer.Deserialize<object>(_document.ToJson(), AgentJson.Options)!;
            else if ((path is "/agent/openapi" or "/agent/openapi.json") && req.HttpMethod == "GET")
                result = JsonSerializer.Deserialize<object>(_document.ToOpenApiJson(), AgentJson.Options)!;
            else if (path == "/agent/mcp/tools" && req.HttpMethod == "GET")
                result = _document.ToMcpTools();
            else if (path == "/agent/rpc/methods" && req.HttpMethod == "GET")
                result = _document.ToRpcMethods();
            else if (path == "/agent/announce" && req.HttpMethod == "GET")
                result = BuildAnnouncement();
            else if (path == "/agent/hello" && req.HttpMethod == "GET")
                result = _host.Hello();
            else if (path == "/agent/snapshot" && req.HttpMethod == "GET")
                result = _host.Snapshot();
            else if (path == "/agent/actions" && req.HttpMethod == "GET")
                result = _host.Actions();
            else if (path == "/agent/continue" && req.HttpMethod == "POST")
                result = _host.Continue();
            else if (path == "/agent/subscribe" && req.HttpMethod == "POST")
            {
                _host.Subscribe();
                result = new AgentSubscribeResponse { Ok = true };
            }
            else if (path == "/agent/command" && req.HttpMethod == "POST")
            {
                using var doc = await ReadJsonAsync(req, cancellationToken).ConfigureAwait(false);
                result = _host.Execute(AgentJsonDispatcher.ParseCommand(doc.RootElement));
            }
            else if (path == "/agent/rpc" && req.HttpMethod == "POST")
            {
                using var doc = await ReadJsonAsync(req, cancellationToken).ConfigureAwait(false);
                var method = doc.RootElement.TryGetProperty("method", out var m) ? m.GetString() : null;
                result = AgentJsonDispatcher.Dispatch(_host, method, doc.RootElement);
            }
            else
            {
                await WriteJsonAsync(context.Response, 404, new { ok = false, error = $"not found {rawPath}" }, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context.Response, 200, new { ok = true, result }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJsonAsync(context.Response, 500, new { ok = false, error = ex.Message }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // connection dead
            }
        }
    }

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("/session/", StringComparison.Ordinal))
            return "/agent/" + path["/session/".Length..];
        return path == "/session" ? "/agent" : path;
    }

    private AgentAnnouncement BuildAnnouncement()
    {
        var hello = _host.Hello();
        return AgentAnnouncement.From(hello) with
        {
            HttpPort = _document.HttpPort,
            TcpPort = _document.TcpPort,
            DocumentUrl = BaseUrl + "/agent/document",
            WebSocketUrl = WebSocketUrl,
        };
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        HttpListenerWebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // ignore
            }

            return;
        }

        var socket = wsContext.WebSocket;
        lock (_fanoutGate)
        {
            _wsClients.Add(socket);
        }

        _host.Subscribe();

        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleWsMessageAsync(socket, text, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (WebSocketException)
        {
            // client gone
        }
        finally
        {
            lock (_fanoutGate)
            {
                _wsClients.Remove(socket);
            }

            try
            {
                socket.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleWsMessageAsync(WebSocket socket, string text, CancellationToken cancellationToken)
    {
        long sequence = 0;
        var method = "";
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            sequence = root.TryGetProperty("sequence", out var s) && s.TryGetInt64(out var seq) ? seq : 0;
            method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
            var payload = root.TryGetProperty("payload", out var p) ? p : default;
            var target = payload.ValueKind == JsonValueKind.Undefined ? root : payload;
            var result = AgentJsonDispatcher.Dispatch(_host, method, target);
            await SendWsFrameAsync(socket, sequence, AgentFrameKinds.Response, method, result, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SendWsFrameAsync(socket, sequence, AgentFrameKinds.Fault, method, new { message = ex.Message }, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task SendWsFrameAsync(
        WebSocket socket,
        long sequence,
        string kind,
        string method,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(new { sequence, kind, method, payload }, AgentJson.Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // client gone
        }
    }

    private async Task HandleSseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        _host.Subscribe();
        var response = context.Response;
        WriteCors(response);
        response.StatusCode = 200;
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";
        response.SendChunked = true;

        var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)) { AutoFlush = true };
        lock (_fanoutGate)
        {
            _sseClients.Add(writer);
        }

        try
        {
            await writer.WriteAsync(": connected\n\n").ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(15000, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(": ping\n\n").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch
        {
            // client gone
        }
        finally
        {
            lock (_fanoutGate)
            {
                _sseClients.Remove(writer);
            }

            try { writer.Dispose(); } catch { /* ignore */ }
            try { response.Close(); } catch { /* ignore */ }
        }
    }

    private void Broadcast(string eventName, object payload)
    {
        var sequence = Interlocked.Increment(ref _eventSequence);
        BroadcastSse(eventName, payload);
        _ = BroadcastWsAsync(sequence, eventName, payload);
    }

    private void BroadcastSse(string eventName, object payload)
    {
        string data;
        try
        {
            data = JsonSerializer.Serialize(payload, AgentJson.Options);
        }
        catch
        {
            return;
        }

        var frame = $"event: {eventName}\ndata: {data}\n\n";
        List<StreamWriter> clients;
        lock (_fanoutGate)
        {
            clients = _sseClients.ToList();
        }

        foreach (var client in clients)
        {
            try
            {
                client.Write(frame);
            }
            catch
            {
                lock (_fanoutGate)
                {
                    _sseClients.Remove(client);
                }

                try { client.Dispose(); } catch { /* ignore */ }
            }
        }
    }

    private async Task BroadcastWsAsync(long sequence, string eventName, object payload)
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(new { sequence, kind = AgentFrameKinds.Event, method = eventName, payload }, AgentJson.Options);
        }
        catch
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        List<WebSocket> targets;
        lock (_fanoutGate)
        {
            targets = _wsClients.ToList();
        }

        foreach (var socket in targets)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                lock (_fanoutGate)
                {
                    _wsClients.Remove(socket);
                }
            }
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(text) ? JsonDocument.Parse("{}") : JsonDocument.Parse(text);
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        int status,
        object payload,
        CancellationToken cancellationToken)
    {
        WriteCors(response);
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, AgentJson.Options);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static void WriteCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "content-type";
    }

    public async ValueTask DisposeAsync()
    {
        _host.Decision -= _onDecision;
        _host.Changed -= _onChanged;
        _host.ActionResult -= _onActionResult;
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { /* ignore */ }
        try { _listener.Close(); } catch { /* ignore */ }

        lock (_fanoutGate)
        {
            foreach (var c in _sseClients)
            {
                try { c.Dispose(); } catch { /* ignore */ }
            }

            _sseClients.Clear();

            foreach (var s in _wsClients)
            {
                try { s.Dispose(); } catch { /* ignore */ }
            }

            _wsClients.Clear();
        }

        try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        _cts.Dispose();
        try { File.Delete(_definition.HttpMarkerPath); } catch { /* ignore */ }
        try { File.Delete(_definition.WsMarkerPath); } catch { /* ignore */ }
    }
}
