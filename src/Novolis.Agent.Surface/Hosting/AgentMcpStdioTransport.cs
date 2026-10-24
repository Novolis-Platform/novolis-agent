using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// Minimal Model Context Protocol server over stdio: <c>initialize</c>, <c>tools/list</c> (from the surface's MCP
/// tool descriptors), and <c>tools/call</c> (routed to <c>agent.hello</c> / <c>snapshot</c> / <c>actions</c> /
/// <c>command</c> / <c>continue</c> / <c>subscribe</c>).
/// </summary>
public sealed class AgentMcpStdioTransport : IAsyncDisposable, IAgentTransport
{
    private readonly IAgentHost _host;
    private readonly AgentSurfaceDefinition _definition;
    private readonly AgentSurfaceDocument _document;
    private readonly AgentEventBroker _broker;
    private readonly bool _ownsBroker;
    private readonly AgentQueueSink _sink;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AgentMcpStdioTransport(
        IAgentHost host,
        AgentSurfaceDefinition definition,
        TextReader? input = null,
        TextWriter? output = null,
        AgentEventBroker? broker = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _document = AgentSurfaceDocument.From(definition);
        _ownsBroker = broker is null;
        _broker = broker ?? new AgentEventBroker(host);
        _sink = new AgentQueueSink("mcp", onEnqueued: evt => _ = WriteNotificationAsync(evt));
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public string Kind => "mcp-stdio";

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            File.WriteAllText(_definition.McpMarkerPath, $"{Environment.ProcessId}\n");
        }
        catch
        {
            // ignore
        }

        _loop = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            // Console.In.ReadLineAsync can ignore cancellation until a line arrives.
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { /* ignore cancel/timeout from a blocked read */ }
        }

        try { File.Delete(_definition.McpMarkerPath); } catch { /* ignore */ }
        if (_ownsBroker)
        {
            _broker.Dispose();
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
            if (string.IsNullOrWhiteSpace(line)) continue;

            await HandleLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        JsonElement? id = null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            id = root.TryGetProperty("id", out var idEl) ? idEl.Clone() : null;
            var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            var paramsEl = root.TryGetProperty("params", out var p) ? p : default;

            object result = method switch
            {
                "initialize" => BuildInitializeResult(),
                "tools/list" => BuildToolsListResult(),
                "tools/call" => await HandleToolCallAsync(paramsEl, cancellationToken).ConfigureAwait(false),
                "ping" => new { },
                _ => throw new InvalidOperationException($"Unknown MCP method '{method}'."),
            };

            if (id is null) return;
            await WriteAsync(new { jsonrpc = "2.0", result, id }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (id is null) return;
            await WriteAsync(
                    new { jsonrpc = "2.0", error = new { code = -32603, message = ex.Message }, id },
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private object BuildInitializeResult() => new
    {
        protocolVersion = "2024-11-05",
        serverInfo = new { name = _definition.SurfaceId, version = _definition.ProtocolVersion },
        capabilities = new { tools = new { }, experimental = new { novolisAgentEvents = true } },
    };

    private object BuildToolsListResult() => new
    {
        tools = _document.ToMcpTools().Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema,
        }),
    };

    private async Task<object> HandleToolCallAsync(JsonElement paramsEl, CancellationToken cancellationToken)
    {
        var name = paramsEl.ValueKind == JsonValueKind.Object && paramsEl.TryGetProperty("name", out var n)
            ? n.GetString() ?? ""
            : "";
        var arguments = paramsEl.ValueKind == JsonValueKind.Object && paramsEl.TryGetProperty("arguments", out var a)
            ? a
            : default;

        object payload;
        if (name.Equals($"{_definition.SurfaceId}_watch", StringComparison.Ordinal))
        {
            payload = _broker.Open(AgentJsonDispatcher.ParseWatch(arguments), _sink);
        }
        else if (name.Equals($"{_definition.SurfaceId}_unwatch", StringComparison.Ordinal))
        {
            var watchId = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("watchId", out var id) ? id.GetString() ?? "" : "";
            payload = _broker.Close(watchId, _sink);
        }
        else if (name.Equals($"{_definition.SurfaceId}_watch_next", StringComparison.Ordinal))
        {
            var watchId = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("watchId", out var id) ? id.GetString() ?? "" : "";
            var max = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("maxEvents", out var maxEl) && maxEl.TryGetInt32(out var parsed) ? parsed : 16;
            payload = await _broker.PollAsync(watchId, max, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        else if (name.EndsWith("_hello", StringComparison.Ordinal))
        {
            payload = _host.Hello();
        }
        else if (name.EndsWith("_snapshot", StringComparison.Ordinal))
        {
            payload = _host.Snapshot();
        }
        else if (name.EndsWith("_actions", StringComparison.Ordinal))
        {
            payload = _host.Actions();
        }
        else if (name.EndsWith("_command", StringComparison.Ordinal))
        {
            payload = _host.Execute(AgentJsonDispatcher.ParseCommand(arguments));
        }
        else if (name.EndsWith("_continue", StringComparison.Ordinal))
        {
            payload = _host.Continue();
        }
        else if (name.EndsWith("_subscribe", StringComparison.Ordinal))
        {
            payload = Subscribe();
        }
        else if (FindAction(name) is { } action)
        {
            payload = _host.Execute(CommandFor(action, arguments));
        }
        else
        {
            throw new InvalidOperationException($"Unknown MCP tool '{name}'.");
        }

        var json = JsonSerializer.Serialize(payload, AgentJson.Options);
        return new { content = new object[] { new { type = "text", text = json } } };
    }

    private AgentAction? FindAction(string toolName)
    {
        foreach (var action in _host.Actions().Actions.Concat(_definition.Actions))
        {
            if (string.Equals(_document.ToolName(action.Id), toolName, StringComparison.Ordinal))
            {
                return action;
            }
        }

        return null;
    }

    private static AgentCommand CommandFor(AgentAction action, JsonElement arguments)
    {
        var command = AgentJsonDispatcher.ParseCommand(arguments);
        command.ActionId = action.Id;
        if (arguments.ValueKind == JsonValueKind.Object)
        {
            command.ArgumentsJson = arguments.GetRawText();
        }

        return command;
    }

    private async Task WriteNotificationAsync(AgentWatchEvent evt)
    {
        try
        {
            await WriteAsync(new { jsonrpc = "2.0", method = "notifications/novolis.agent.event", @params = evt }, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            // The caller can still poll agent.watch.next.
        }
    }

    private AgentSubscribeResponse Subscribe()
    {
        _host.Subscribe();
        return new AgentSubscribeResponse { Ok = true };
    }

    private async Task WriteAsync(object payload, CancellationToken cancellationToken)
    {
        await _write.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(payload, AgentJson.Options);
            await _output.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _write.Release();
        }
    }
}
