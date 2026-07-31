<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Agent.Core` | `dotnet add package Novolis.Agent.Core` | [README](https://github.com/Novolis-Platform/novolis-agent/blob/main/src/Novolis.Agent.Core/README.md) |
| `Novolis.Agent.Surface` | `dotnet add package Novolis.Agent.Surface` | [README](https://github.com/Novolis-Platform/novolis-agent/blob/main/src/Novolis.Agent.Surface/README.md) |
| `Novolis.Agent.Testing` | `dotnet add package Novolis.Agent.Testing` | [README](https://github.com/Novolis-Platform/novolis-agent/blob/main/src/Novolis.Agent.Testing/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->

# novolis-agent

Live **Agent Surface** control for running apps: attributed catalogs, OpenAPI-class documents, announce/discovery, and multi-transport hosts (HTTP/SSE, WebSocket, LocalIpc, TCP JSONL, stdio, JSON-RPC, MCP).

## Packages

| Package | Description |
|---------|-------------|
| `Novolis.Agent.Core` | `IAgentHost`, duplex frames, DTOs, `agent.*` method names |
| `Novolis.Agent.Surface` | Definition, document, announce, AttachAll transports |
| `Novolis.Agent.Testing` | Fake host and in-memory channel |

## Build

```bash
dotnet build
dotnet test
```

Docs: [getting-started](docs/getting-started.md) · [design](docs/design.md) · [agent-surface](docs/agent-surface.md)
