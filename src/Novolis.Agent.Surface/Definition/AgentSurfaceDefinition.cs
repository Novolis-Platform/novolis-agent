using System.Reflection;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>Auto-constructed surface metadata from <see cref="AgentSurfaceAttribute"/> / <see cref="AgentActionAttribute"/>.</summary>
public sealed class AgentSurfaceDefinition
{
    private AgentSurfaceDefinition(
        string surfaceId,
        string protocolVersion,
        string enableEnv,
        string markerPrefix,
        int httpPort,
        int tcpPort,
        string? description,
        IReadOnlyList<AgentAction> actions,
        IReadOnlyList<string> methods)
    {
        SurfaceId = surfaceId;
        ProtocolVersion = protocolVersion;
        EnableEnv = enableEnv;
        MarkerPrefix = markerPrefix;
        DefaultHttpPort = httpPort;
        DefaultTcpPort = tcpPort;
        DefaultRpcPort = tcpPort + 1;
        Description = description;
        Actions = actions;
        Methods = methods;
    }

    public string SurfaceId { get; }

    public string ProtocolVersion { get; }

    public string EnableEnv { get; }

    public string MarkerPrefix { get; }

    public int DefaultHttpPort { get; }

    public int DefaultTcpPort { get; }

    public int DefaultRpcPort { get; }

    public string? Description { get; }

    public IReadOnlyList<AgentAction> Actions { get; }

    /// <summary>Wire capabilities (default: the six <c>agent.*</c> methods).</summary>
    public IReadOnlyList<string> Methods { get; }

    public string HttpEnableEnv => EnableEnv + "_HTTP";
    public string HttpPortEnv => EnableEnv + "_HTTP_PORT";
    public string IpcEnableEnv => EnableEnv + "_IPC";
    public string IpcAddressEnv => EnableEnv + "_ENDPOINT";
    public string TcpEnableEnv => EnableEnv + "_TCP";
    public string TcpPortEnv => EnableEnv + "_TCP_PORT";
    public string RpcEnableEnv => EnableEnv + "_RPC";
    public string RpcPortEnv => EnableEnv + "_RPC_PORT";
    public string McpEnableEnv => EnableEnv + "_MCP";
    public string StdioEnableEnv => EnableEnv + "_STDIO";

    public string HttpMarkerFileName => MarkerPrefix + ".http";
    public string WsMarkerFileName => MarkerPrefix + ".ws";
    public string IpcMarkerFileName => MarkerPrefix + ".ipc";
    public string TcpMarkerFileName => MarkerPrefix + ".tcp";
    public string RpcMarkerFileName => MarkerPrefix + ".rpc";
    public string McpMarkerFileName => MarkerPrefix + ".mcp";

    public string HttpMarkerPath => Path.Combine(Path.GetTempPath(), HttpMarkerFileName);
    public string WsMarkerPath => Path.Combine(Path.GetTempPath(), WsMarkerFileName);
    public string IpcMarkerPath => Path.Combine(Path.GetTempPath(), IpcMarkerFileName);
    public string TcpMarkerPath => Path.Combine(Path.GetTempPath(), TcpMarkerFileName);
    public string RpcMarkerPath => Path.Combine(Path.GetTempPath(), RpcMarkerFileName);
    public string McpMarkerPath => Path.Combine(Path.GetTempPath(), McpMarkerFileName);

    public static AgentSurfaceDefinition From<T>() => From(typeof(T));

    public static AgentSurfaceDefinition From(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var surface = type.GetCustomAttribute<AgentSurfaceAttribute>()
                      ?? throw new InvalidOperationException($"{type.Name} requires [AgentSurface].");

        var actions = type.GetCustomAttributes<AgentActionAttribute>(inherit: true)
            .Select(a => new AgentAction
            {
                Id = a.ActionId,
                Label = string.IsNullOrEmpty(a.Summary) ? a.ActionId : a.Summary,
                Summary = a.Summary,
                Params = a.Params,
                Enabled = a.EnabledByDefault,
                Schema = BuildParamSchema(a.Params),
            })
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Select(m => m.GetCustomAttribute<AgentMethodAttribute>()?.Method)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (methods.Count == 0)
        {
            methods =
            [
                AgentMethodNames.Hello,
                AgentMethodNames.Snapshot,
                AgentMethodNames.Actions,
                AgentMethodNames.Command,
                AgentMethodNames.Continue,
                AgentMethodNames.Subscribe,
            ];
        }

        return new AgentSurfaceDefinition(
            surface.SurfaceId,
            surface.ProtocolVersion,
            surface.EnableEnv,
            surface.MarkerPrefix,
            surface.HttpPort,
            surface.TcpPort,
            surface.Description,
            actions,
            methods);
    }

    public AgentHello BuildHello(
        string appId = "",
        string appTitle = "",
        int? httpPort = null,
        int? tcpPort = null,
        string? documentUrl = null,
        string? webSocketUrl = null) => new()
    {
        ProtocolVersion = ProtocolVersion,
        AppId = appId,
        AppTitle = appTitle,
        ProcessId = Environment.ProcessId,
        Capabilities = Methods.ToArray(),
        SurfaceId = SurfaceId,
        Description = Description,
        HttpPort = httpPort ?? DefaultHttpPort,
        TcpPort = tcpPort ?? DefaultTcpPort,
        DocumentUrl = documentUrl,
        WebSocketUrl = webSocketUrl,
    };

    public AgentActionsResponse BuildActions(Func<AgentAction, AgentAction>? policy = null)
    {
        var list = Actions.Select(a =>
        {
            var copy = new AgentAction
            {
                Id = a.Id,
                Label = a.Label,
                Enabled = a.Enabled,
                DisabledReason = a.DisabledReason,
                Summary = a.Summary,
                Params = a.Params,
                Schema = a.Schema,
            };
            return policy?.Invoke(copy) ?? copy;
        }).ToArray();
        return new AgentActionsResponse { Actions = list };
    }

    public Dictionary<string, object?> BuildCommandJsonSchema() => new()
    {
        ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
        ["title"] = $"{SurfaceId}.command",
        ["type"] = "object",
        ["required"] = new[] { "actionId" },
        ["properties"] = new Dictionary<string, object?>
        {
            ["actionId"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["enum"] = Actions.Select(a => a.Id).ToArray(),
            },
            ["params"] = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["additionalProperties"] = new Dictionary<string, object?> { ["type"] = "string" },
            },
            ["path"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["nodeId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["parentId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["lightKind"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["enum"] = new[] { "omni", "spot", "infinite", "area" },
            },
            ["name"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["intensity"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["x"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["y"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["z"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["rx"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["ry"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["rz"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["generatorKind"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["modifierKind"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["sourceId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["inputId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["targetId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["cutterId"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["booleanKind"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["primitive"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["segments"] = new Dictionary<string, object?> { ["type"] = "integer" },
            ["distance"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["count"] = new Dictionary<string, object?> { ["type"] = "integer" },
            ["axis"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["materialColor"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["editMode"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["displayMode"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["indices"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["additive"] = new Dictionary<string, object?> { ["type"] = "boolean" },
        },
    };

    public bool IsEnabledByEnvironment() => EnvTruthy(Environment.GetEnvironmentVariable(EnableEnv));

    /// <summary>HTTP is on when the surface is on, unless explicitly disabled; explicit "1" also enables HTTP alone.</summary>
    public bool IsHttpEnabledByEnvironment()
    {
        var http = Environment.GetEnvironmentVariable(HttpEnableEnv);
        if (EnvFalsy(http))
            return false;
        if (EnvTruthy(http))
            return true;
        return IsEnabledByEnvironment();
    }

    /// <summary>Local-IPC is on when the surface is on, unless explicitly disabled.</summary>
    public bool IsIpcEnabledByEnvironment()
    {
        var ipc = Environment.GetEnvironmentVariable(IpcEnableEnv);
        if (EnvFalsy(ipc))
            return false;
        if (EnvTruthy(ipc))
            return true;
        return IsEnabledByEnvironment();
    }

    public bool IsTcpEnabledByEnvironment() => EnvTruthy(Environment.GetEnvironmentVariable(TcpEnableEnv));

    public bool IsRpcEnabledByEnvironment() => EnvTruthy(Environment.GetEnvironmentVariable(RpcEnableEnv));

    public bool IsMcpEnabledByEnvironment() => EnvTruthy(Environment.GetEnvironmentVariable(McpEnableEnv));

    public bool IsStdioEnabledByEnvironment() => EnvTruthy(Environment.GetEnvironmentVariable(StdioEnableEnv));

    public int ResolveHttpPort() => ResolvePort(HttpPortEnv, DefaultHttpPort);

    public int ResolveTcpPort() => ResolvePort(TcpPortEnv, DefaultTcpPort);

    public int ResolveRpcPort() => ResolvePort(RpcPortEnv, DefaultRpcPort);

    public string? TryReadHttpBaseUrl() => TryReadMarkerSecondLine(HttpMarkerPath);

    public string? TryReadWebSocketUrl() => TryReadMarkerSecondLine(WsMarkerPath);

    private static string? TryReadMarkerSecondLine(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var lines = File.ReadAllLines(path);
            return lines.Length >= 2 ? lines[1].Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private int ResolvePort(string envVar, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var port) && port is > 0 and < 65536 ? port : fallback;
    }

    private static Dictionary<string, object?>? BuildParamSchema(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return null;

        var props = new Dictionary<string, object?>();
        foreach (var part in hint.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var body = part.EndsWith('?') ? part[..^1] : part;
            var bits = body.Split('|', 2);
            var name = bits[0].Trim();
            props[name] = bits.Length == 2
                ? new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = bits[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                }
                : new Dictionary<string, object?> { ["type"] = "string" };
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = props,
        };
    }

    private static bool EnvTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool EnvFalsy(string? value) =>
        string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
}
