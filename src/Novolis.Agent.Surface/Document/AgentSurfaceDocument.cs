using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>
/// Builds discovery artifacts (document JSON, OpenAPI fragment, MCP tool list, JSON-RPC method list) for an
/// <see cref="AgentSurfaceDefinition"/>, given the ports the surface is actually bound to.
/// </summary>
public sealed class AgentSurfaceDocument
{
    private readonly AgentSurfaceDefinition _definition;

    private AgentSurfaceDocument(AgentSurfaceDefinition definition, int httpPort, int tcpPort, int rpcPort)
    {
        _definition = definition;
        HttpPort = httpPort;
        TcpPort = tcpPort;
        RpcPort = rpcPort;
    }

    public int HttpPort { get; }

    public int TcpPort { get; }

    public int RpcPort { get; }

    public static AgentSurfaceDocument From(
        AgentSurfaceDefinition definition,
        int? httpPort = null,
        int? tcpPort = null,
        int? rpcPort = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new AgentSurfaceDocument(
            definition,
            httpPort ?? definition.DefaultHttpPort,
            tcpPort ?? definition.DefaultTcpPort,
            rpcPort ?? definition.DefaultRpcPort);
    }

    public object ToDocumentObject() => new
    {
        surfaceId = _definition.SurfaceId,
        protocolVersion = _definition.ProtocolVersion,
        description = _definition.Description,
        enableEnv = _definition.EnableEnv,
        httpPort = HttpPort,
        tcpPort = TcpPort,
        rpcPort = RpcPort,
        markers = new
        {
            http = _definition.HttpMarkerPath,
            ws = _definition.WsMarkerPath,
            ipc = _definition.IpcMarkerPath,
            tcp = _definition.TcpMarkerPath,
            rpc = _definition.RpcMarkerPath,
            mcp = _definition.McpMarkerPath,
        },
        capabilities = _definition.Methods,
        actions = _definition.Actions,
        commandSchema = _definition.BuildCommandJsonSchema(),
        openApi = ToOpenApiObject(),
        mcpTools = ToMcpTools(),
        rpcMethods = ToRpcMethods(),
    };

    public string ToJson() => JsonSerializer.Serialize(ToDocumentObject(), AgentJson.Options);

    public Dictionary<string, object?> ToOpenApiObject() => new()
    {
        ["openapi"] = "3.0.3",
        ["info"] = new Dictionary<string, object?>
        {
            ["title"] = $"Novolis Agent Surface ({_definition.SurfaceId})",
            ["version"] = _definition.ProtocolVersion,
            ["description"] = _definition.Description ?? $"Loopback agent surface '{_definition.SurfaceId}'.",
        },
        ["servers"] = new object[]
        {
            new Dictionary<string, object?> { ["url"] = $"http://127.0.0.1:{HttpPort}" },
        },
        ["paths"] = new Dictionary<string, object?>
        {
            ["/agent/hello"] = GetPath("Hello handshake"),
            ["/agent/snapshot"] = GetPath("Current snapshot"),
            ["/agent/actions"] = GetPath("Action catalog"),
            ["/agent/command"] = PostPath("Execute a command", _definition.BuildCommandJsonSchema()),
            ["/agent/continue"] = PostPath("Release the decision gate"),
            ["/agent/subscribe"] = PostPath("Subscribe to events"),
            ["/agent/watch"] = PostPath("Open a filtered watch"),
            ["/agent/watch/next"] = PostPath("Poll events for a watch"),
            ["/agent/unwatch"] = PostPath("Close a watch"),
            ["/agent/events"] = GetPath("Server-sent event stream"),
            ["/agent/ws"] = GetPath("WebSocket duplex channel"),
            ["/agent/document"] = GetPath("Full surface document"),
            ["/agent/openapi"] = GetPath("OpenAPI 3 projection"),
            ["/agent/openapi.json"] = GetPath("OpenAPI 3 projection (alias)"),
            ["/agent/mcp/tools"] = GetPath("MCP tool descriptors"),
            ["/agent/rpc/methods"] = GetPath("JSON-RPC method list"),
            ["/agent/announce"] = GetPath("Surface announcement"),
            ["/health"] = GetPath("Health"),
        },
    };

    public string ToOpenApiJson() => JsonSerializer.Serialize(ToOpenApiObject(), AgentJson.Options);

    public IReadOnlyList<McpToolDescriptor> ToMcpTools()
    {
        var empty = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>(),
        };

        var tools = new List<McpToolDescriptor>
        {
            new($"{_definition.SurfaceId}_hello", $"{AgentMethodNames.Hello} for {_definition.SurfaceId}", empty),
            new($"{_definition.SurfaceId}_snapshot", $"{AgentMethodNames.Snapshot} for {_definition.SurfaceId}", empty),
            new($"{_definition.SurfaceId}_actions", $"{AgentMethodNames.Actions} for {_definition.SurfaceId}", empty),
            new($"{_definition.SurfaceId}_command", $"{AgentMethodNames.Command} for {_definition.SurfaceId}", _definition.BuildCommandJsonSchema()),
            new($"{_definition.SurfaceId}_continue", $"{AgentMethodNames.Continue} for {_definition.SurfaceId}", empty),
            new($"{_definition.SurfaceId}_subscribe", $"{AgentMethodNames.Subscribe} for {_definition.SurfaceId}", empty),
            new($"{_definition.SurfaceId}_watch", "Open a filtered event watch on this connection", WatchSchema()),
            new($"{_definition.SurfaceId}_watch_next", "Poll the next events for a watch", WatchNextSchema()),
            new($"{_definition.SurfaceId}_unwatch", "Close a watch", UnwatchSchema()),
        };

        foreach (var action in _definition.Actions)
        {
            tools.Add(new McpToolDescriptor(
                ToolName(action.Id),
                string.IsNullOrWhiteSpace(action.Summary) ? action.Label : action.Summary,
                action.Schema ?? empty));
        }

        return tools;
    }

    public string ToolName(string actionId) =>
        $"{_definition.SurfaceId}_{Sanitize(actionId)}";

    private static string Sanitize(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    private static Dictionary<string, object?> WatchSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["eventNames"] = new Dictionary<string, object?> { ["type"] = "array" },
            ["callbackId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["correlationId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["resourceId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["minSeverity"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["afterEventSequence"] = new Dictionary<string, object?> { ["type"] = "integer" },
        },
    };

    private static Dictionary<string, object?> WatchNextSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new[] { "watchId" },
        ["properties"] = new Dictionary<string, object?>
        {
            ["watchId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["maxEvents"] = new Dictionary<string, object?> { ["type"] = "integer" },
        },
    };

    private static Dictionary<string, object?> UnwatchSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new[] { "watchId" },
        ["properties"] = new Dictionary<string, object?>
        {
            ["watchId"] = new Dictionary<string, object?> { ["type"] = "string" },
        },
    };

    public IReadOnlyList<AgentRpcMethodDescriptor> ToRpcMethods() =>
    [
        new AgentRpcMethodDescriptor(AgentMethodNames.Hello, "Hello handshake", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Snapshot, "Current snapshot", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Actions, "Action catalog", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Command, "Execute a command", _definition.BuildCommandJsonSchema()),
        new AgentRpcMethodDescriptor(AgentMethodNames.Continue, "Release the decision gate", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Subscribe, "Subscribe to events", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Watch, "Open a filtered watch", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.WatchNext, "Poll a watch", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Unwatch, "Close a watch", null),
    ];

    private static Dictionary<string, object?> GetPath(string summary) => new()
    {
        ["get"] = new Dictionary<string, object?>
        {
            ["summary"] = summary,
            ["responses"] = new Dictionary<string, object?>
            {
                ["200"] = new Dictionary<string, object?> { ["description"] = "ok" },
            },
        },
    };

    private static Dictionary<string, object?> PostPath(string summary, Dictionary<string, object?>? schema = null)
    {
        var post = new Dictionary<string, object?>
        {
            ["summary"] = summary,
            ["responses"] = new Dictionary<string, object?>
            {
                ["200"] = new Dictionary<string, object?> { ["description"] = "ok" },
            },
        };

        if (schema is not null)
        {
            post["requestBody"] = new Dictionary<string, object?>
            {
                ["content"] = new Dictionary<string, object?>
                {
                    ["application/json"] = new Dictionary<string, object?> { ["schema"] = schema },
                },
            };
        }

        return new Dictionary<string, object?> { ["post"] = post };
    }
}
