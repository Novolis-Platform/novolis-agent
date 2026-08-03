<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-agent.svg" width="100%" alt="novolis-agent"/>
</p>

<p align="center">
  <strong>Live agent surfaces</strong><br/>
  Control surfaces for live apps — Core, Surface, and Testing helpers.
</p>

<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-agent/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-agent/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-agent"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
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

