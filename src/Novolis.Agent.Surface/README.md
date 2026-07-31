# Novolis.Agent.Surface

Attributed **agent surfaces** on top of [`Novolis.Agent.Core`](../Novolis.Agent.Core/README.md): a single `[AgentSurface]`-decorated
interface auto-builds an action catalog, JSON Schema, an OpenAPI fragment, MCP tool descriptors, and JSON-RPC method
descriptors — then attaches loopback HTTP (+ WebSocket + SSE), TCP JSONL, JSON-RPC 2.0, local-IPC (MessagePack), stdio
JSONL, and MCP-stdio transports around any `IAgentHost` implementation.

## Install

```bash
dotnet add package Novolis.Agent.Surface
```

## Define a surface

```csharp
[AgentSurface("my-app", Description = "My app's agent surface.")]
[AgentAction("doThing", Summary = "Do the thing", Params = "id; count?")]
public interface IMyAgentSurface;

var definition = AgentSurfaceDefinition.From<IMyAgentSurface>();
```

## Attach transports

```csharp
IAgentHost host = new MyAgentHost();

// Everything, unconditionally (good for "always on" apps):
await using var surface = AgentSurface.AttachAll(host, definition);

// Or gated by environment variables (NOVOLIS_AGENT, NOVOLIS_AGENT_HTTP, ...):
await using var surface = AgentSurface.TryAttachFromEnvironment(host, definition);
```

`surface.HttpBaseUrl`, `surface.WebSocketUrl`, `surface.TcpPort`, and `surface.RpcPort` report the bound endpoints.
Each host writes a small marker file under the temp directory (`{MarkerPrefix}.http`, `.ws`, `.ipc`, `.tcp`, `.rpc`,
`.mcp`) so sidecar processes can discover a running surface without guessing ports.

## Discover the surface

* `GET /agent/document` — full JSON document (capabilities, actions, command schema, OpenAPI, MCP tools, RPC methods).
* `GET /agent/openapi` — OpenAPI 3.0 fragment for `/agent/*` routes.
* `GET /agent/mcp/tools` — MCP tool descriptors (`{surfaceId}_hello`, `_snapshot`, `_actions`, `_command`, ...).
* `GET /agent/rpc/methods` — JSON-RPC method descriptors.
* `GET /agent/announce` — a lightweight `AgentAnnouncement` for presence/discovery.

## Clients

* `AgentHttpClient` — REST client for the HTTP surface.
* `AgentLocalIpcClient` — MessagePack client for the local-IPC surface.

## Command line agent tooling

`AgentMcpStdioTransport` speaks a minimal Model Context Protocol over stdio (`initialize`, `tools/list`,
`tools/call`) so an app can be driven directly by an MCP-aware agent host process.
