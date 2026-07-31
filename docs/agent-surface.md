# Agent Surface protocol

Live control API of a running process: snapshot, actions, commands, events — over multiple transports — with a machine-readable **document** and **announcement**.

## Wire methods (`agent.*`)

| Method | Kind |
|--------|------|
| `agent.hello` | req/res |
| `agent.snapshot` | req/res |
| `agent.actions` | req/res |
| `agent.command` | req/res |
| `agent.continue` | req/res (optional gate) |
| `agent.subscribe` | req/res |
| `agent.decision` / `agent.changed` / `agent.actionResult` | events |

Hosts also accept legacy `session.*` aliases during migration.

## HTTP

| Path | Notes |
|------|-------|
| `GET /agent/document` | Full `AgentSurfaceDocument` |
| `GET /agent/openapi.json` | OpenAPI 3 projection |
| `GET /agent/mcp/tools` | MCP tool descriptors |
| `GET /agent/rpc/methods` | JSON-RPC method table |
| `GET /agent/announce` | Live endpoints |
| `GET /agent/hello\|snapshot\|actions` | |
| `POST /agent/command\|subscribe\|continue\|rpc` | |
| `GET /agent/events` | SSE |
| `GET /agent/ws` | WebSocket duplex |
| `/session/*` | Temporary aliases |

## Other transports

LocalIpc (MessagePack), TCP JSONL, Stdio JSONL, JSON-RPC TCP, MCP stdio (headless only).

## Announce

Temp markers: `%TEMP%/novolis-agent-{surfaceId}.{http\|ws\|ipc\|tcp\|mcp\|rpc}`.
