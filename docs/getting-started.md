# Getting started

```bash
dotnet add package Novolis.Agent.Surface
```

```csharp
using Novolis.Agent.Core;
using Novolis.Agent.Surface;

[AgentSurface("myapp", HttpPort = 18785)]
[AgentAction("ping", Summary = "Ping")]
public interface IMySurface : IAgentHost;

var def = AgentSurfaceDefinition.From<IMySurface>();
await using var surface = AgentSurface.AttachAll(host, def);

// Document (OpenAPI-class):
// GET http://127.0.0.1:18785/agent/document
// GET http://127.0.0.1:18785/agent/openapi.json
// WebSocket duplex: ws://127.0.0.1:18785/agent/ws
```

See [agent-surface.md](agent-surface.md) for the protocol.
