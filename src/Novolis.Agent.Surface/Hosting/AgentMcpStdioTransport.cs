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
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AgentMcpStdioTransport(IAgentHost host, AgentSurfaceDefinition definition, TextReader? input = null, TextWriter? output = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _document = AgentSurfaceDocument.From(definition);
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
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* ignore */ }
        }

        try { File.Delete(_definition.McpMarkerPath); } catch { /* ignore */ }
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
                "tools/call" => HandleToolCall(paramsEl),
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
        capabilities = new { tools = new { } },
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

    private object HandleToolCall(JsonElement paramsEl)
    {
        var name = paramsEl.ValueKind == JsonValueKind.Object && paramsEl.TryGetProperty("name", out var n)
            ? n.GetString() ?? ""
            : "";
        var arguments = paramsEl.ValueKind == JsonValueKind.Object && paramsEl.TryGetProperty("arguments", out var a)
            ? a
            : default;

        object payload = name switch
        {
            _ when name.EndsWith("_hello", StringComparison.Ordinal) => _host.Hello(),
            _ when name.EndsWith("_snapshot", StringComparison.Ordinal) => _host.Snapshot(),
            _ when name.EndsWith("_actions", StringComparison.Ordinal) => _host.Actions(),
            _ when name.EndsWith("_command", StringComparison.Ordinal) =>
                _host.Execute(AgentJsonDispatcher.ParseCommand(arguments)),
            _ when name.EndsWith("_continue", StringComparison.Ordinal) => _host.Continue(),
            _ when name.EndsWith("_subscribe", StringComparison.Ordinal) => Subscribe(),
            _ => throw new InvalidOperationException($"Unknown MCP tool '{name}'."),
        };

        var json = JsonSerializer.Serialize(payload, AgentJson.Options);
        return new { content = new object[] { new { type = "text", text = json } } };
    }

    private AgentSubscribeResponse Subscribe()
    {
        _host.Subscribe();
        return new AgentSubscribeResponse { Ok = true };
    }

    private async Task WriteAsync(object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, AgentJson.Options);
        await _output.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
