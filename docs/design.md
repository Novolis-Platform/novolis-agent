# Design

`novolis-agent` owns **live process control** for agents and tools.

| Package | Role |
|---------|------|
| `Novolis.Agent.Core` | `IAgentHost`, frames, DTOs, `agent.*` names |
| `Novolis.Agent.Surface` | Attributes, document, announce, transports |
| `Novolis.Agent.Testing` | Fakes for tests |

Vocabulary: **Surface / Host / Channel / Transport / Document / Announce**.

Not Commands (intent parse/queue). Not Avalonia UI glass (`Novolis.Avalonia.Agent` stays in `novolis-avalonia`).

Dependency: `Novolis.Transports.LocalIpc` for named-pipe / UDS duplex.
