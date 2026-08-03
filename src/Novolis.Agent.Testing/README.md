<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-agent">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Agent.Testing

**Fake `IAgentHost`**, in-memory **`IAgentChannel`**, and helpers for Agent Surface unit/integration tests without binding HTTP or stdio transports.

## Install

```bash
dotnet add package Novolis.Agent.Testing
```

Depends on `Novolis.Agent.Core`.

## Quick start — fake host

```csharp
using Novolis.Agent.Core;
using Novolis.Agent.Testing;

var host = new FakeAgentHost
{
    SnapshotResponse = new AgentSnapshot { /* … */ },
};

var result = host.Execute(new AgentCommand { ActionId = "ping" });
Assert.True(result.Ok);
Assert.Single(host.Executed);

host.RaiseChanged("inventory updated");
host.RaiseDecision("buy ore");
```

## Quick start — in-memory channel

```csharp
await using var channel = new InMemoryAgentChannel();
channel.EnqueueInbound(new AgentFrame(
    Sequence: 1,
    Kind: AgentFrameKinds.Request,
    Method: AgentMethodNames.Hello,
    Payload: ReadOnlyMemory<byte>.Empty));
// Transport loop reads via channel.ReadFramesAsync; tests assert via ReadOutboundAsync
var reply = await channel.ReadOutboundAsync();
```

Pair with `Novolis.Agent.Surface` transport tests by driving `IAgentChannel` directly.

## API

| Type | Role |
|------|------|
| `FakeAgentHost` | Mutable `Hello` / `Snapshot` / `Actions`; records `Execute` calls |
| `FakeAgentHost.Executed` | Commands passed to `Execute` |
| `FakeAgentHost.SubscribeCount` / `ContinueCount` | Subscription / continue gate counters |
| `FakeAgentHost.RaiseChanged` / `RaiseDecision` / `RaiseActionResult` | Fire host events |
| `InMemoryAgentChannel` | Duplex `IAgentChannel` over unbounded channels |
| `InMemoryAgentChannel.EnqueueInbound` | Inject client → host frames |
| `InMemoryAgentChannel.ReadOutboundAsync` | Read host → client replies in tests |

## Related

| Package | Role |
|---------|------|
| `Novolis.Agent.Core` | `IAgentHost`, `IAgentChannel`, DTOs, `AgentMethodNames` |
| `Novolis.Agent.Surface` | Transports and surface document (test with fake host) |

## Dogfooding

Used by `novolis-agent` tests and any app that attaches Agent Surface but needs fast in-process host/channel coverage.

