using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>MCP tool descriptor: name, human summary, and JSON Schema input shape.</summary>
public sealed record McpToolDescriptor(string Name, string Description, Dictionary<string, object?> InputSchema);

/// <summary>JSON-RPC 2.0 method descriptor exposed by an agent surface.</summary>
public sealed record AgentRpcMethodDescriptor(string Method, string Summary, Dictionary<string, object?>? ParamsSchema);

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
            ["/agent/events"] = GetPath("Server-sent event stream"),
            ["/agent/ws"] = GetPath("WebSocket duplex channel"),
            ["/agent/document"] = GetPath("Full surface document"),
            ["/agent/openapi.json"] = GetPath("OpenAPI 3 projection"),
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

        return
        [
            new McpToolDescriptor($"{_definition.SurfaceId}_hello", $"{AgentMethodNames.Hello} for {_definition.SurfaceId}", empty),
            new McpToolDescriptor($"{_definition.SurfaceId}_snapshot", $"{AgentMethodNames.Snapshot} for {_definition.SurfaceId}", empty),
            new McpToolDescriptor($"{_definition.SurfaceId}_actions", $"{AgentMethodNames.Actions} for {_definition.SurfaceId}", empty),
            new McpToolDescriptor($"{_definition.SurfaceId}_command", $"{AgentMethodNames.Command} for {_definition.SurfaceId}", _definition.BuildCommandJsonSchema()),
            new McpToolDescriptor($"{_definition.SurfaceId}_continue", $"{AgentMethodNames.Continue} for {_definition.SurfaceId}", empty),
            new McpToolDescriptor($"{_definition.SurfaceId}_subscribe", $"{AgentMethodNames.Subscribe} for {_definition.SurfaceId}", empty),
        ];
    }

    public IReadOnlyList<AgentRpcMethodDescriptor> ToRpcMethods() =>
    [
        new AgentRpcMethodDescriptor(AgentMethodNames.Hello, "Hello handshake", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Snapshot, "Current snapshot", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Actions, "Action catalog", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Command, "Execute a command", _definition.BuildCommandJsonSchema()),
        new AgentRpcMethodDescriptor(AgentMethodNames.Continue, "Release the decision gate", null),
        new AgentRpcMethodDescriptor(AgentMethodNames.Subscribe, "Subscribe to events", null),
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
