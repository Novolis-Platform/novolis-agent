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
| `agent.watch` | open a filtered watch on the live connection |
| `agent.watch.next` | long-poll events for clients that cannot wake on a notification |
| `agent.unwatch` | close a watch |
| `agent.event` / `agent.watchClosed` | watch delivery |
| `agent.decision` / `agent.changed` / `agent.actionResult` | events |

Hosts also accept legacy `session.*` aliases during migration.

## HTTP

| Path | Notes |
|------|-------|
| `GET /agent/document` | Full `AgentSurfaceDocument` |
| `GET /agent/openapi` / `GET /agent/openapi.json` | OpenAPI 3 projection |
| `GET /agent/mcp/tools` | MCP tool descriptors |
| `GET /agent/rpc/methods` | JSON-RPC method table |
| `GET /agent/announce` | Live endpoints |
| `GET /agent/hello\|snapshot\|actions` | |
| `POST /agent/command\|subscribe\|continue\|rpc` | |
| `POST /agent/watch` / `POST /agent/watch/next` / `POST /agent/unwatch` | loopback poll; no cross-origin header |
| `GET /agent/watch` | SSE push (`Last-Event-ID` resumes); no cross-origin header |
| `GET /agent/events` | SSE |
| `GET /agent/ws` | WebSocket duplex |
| `/session/*` | Temporary aliases |

## Other transports

LocalIpc (MessagePack), TCP JSONL, Stdio JSONL, JSON-RPC TCP, MCP stdio (headless only).

## Announce

Temp markers: `%TEMP%/novolis-agent-{surfaceId}.{http\|ws\|ipc\|tcp\|mcp\|rpc}`.

## Contract v2

Existing method names, `Params`, and MessagePack keys stay valid. Optional fields add capability, safety class, approval, idempotency, effect preview, schemas, `correlationId`, `expectedRevision`, `approvalToken`, `stateRevision`, observations, and receipts. The metadata version is `agent.contract.v2`.

A remote callback is an event on the already-open connection (`agent.event` or MCP `notifications/novolis.agent.event`). It is not a callback URL or a serialized delegate. `agent.watch.next` is the fallback when a host cannot surface notifications to the model. Profile authoring is in [agent-profiles.md](agent-profiles.md).

