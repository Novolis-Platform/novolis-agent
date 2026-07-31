# Novolis.Agent.Core

Contracts for **Agent Surface**: `IAgentHost`, duplex `IAgentChannel` / `AgentFrame`, shared DTOs, and `agent.*` wire method names.

Implement `IAgentHost` in your app; attach transports via `Novolis.Agent.Surface`.

## Install

```bash
dotnet add package Novolis.Agent.Core
```

## Quick start

```csharp
using Novolis.Agent.Core;

public sealed class MyAgentHost : IAgentHost
{
    public AgentHello Hello() => new() { AppId = "my-app", ProtocolVersion = "1.0" };
    public AgentSnapshot Snapshot() => new();
    public AgentActionsResponse Actions() => new();
    public AgentCommandResult Execute(AgentCommand command) =>
        new() { Ok = true, ActionId = command.ActionId };
    public AgentCommandResult Continue() => new() { Ok = true };
    public void Subscribe() { }
}
```

Wire payloads use `AgentProtocolCodec` and method names from `AgentMethodNames` / `AgentActionIds`.

## API

| Type | Role |
|------|------|
| `IAgentHost` | Hello, snapshot, actions, execute, continue, subscribe, events |
| `IAgentTransport` | Start/stop a transport serving a host |
| `IAgentChannel` | Duplex frame stream (`ReadFramesAsync`, `SendAsync`) |
| `AgentFrame` / `AgentFrameKinds` | JSON-RPC-style request/response/event frames |
| `AgentHello` / `AgentSnapshot` / `AgentActionsResponse` | Handshake and state DTOs |
| `AgentCommand` / `AgentCommandResult` | Action invocation |
| `AgentDecisionEvent` / `AgentChangedEvent` / `AgentActionResultEvent` | Push events |
| `AgentMethodNames` / `AgentActionIds` | Wire method and well-known action ids |
| `AgentProtocolCodec` | Serialize/deserialize frames |
| `AgentCommandKeys` / `AgentBoardIds` / `AgentLineKeys` | Structured payload keys |

## Related

| Package | Role |
|---------|------|
| `Novolis.Agent.Surface` | Surface definition, OpenAPI/MCP, HTTP/stdio transports |
| `Novolis.Agent.Testing` | `FakeAgentHost`, `InMemoryAgentChannel` for tests |
