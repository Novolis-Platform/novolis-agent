# Novolis.Agent.Core

Contracts for **Agent Surface**: `IAgentHost`, duplex `IAgentChannel` / `AgentFrame`, shared DTOs, and `agent.*` wire method names.

## Install

```bash
dotnet add package Novolis.Agent.Core
```

## Host

Implement `IAgentHost` in your app. Attach transports via `Novolis.Agent.Surface`.
