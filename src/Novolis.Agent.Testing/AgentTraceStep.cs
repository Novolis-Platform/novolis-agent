namespace Novolis.Agent.Testing;

public sealed class AgentTraceStep
{
    public string ActionId { get; init; } = "";

    public string? CorrelationId { get; init; }

    public long? StateRevision { get; init; }

    public bool Ok { get; init; }

    public string? ReasonCode { get; init; }

    public string Message { get; init; } = "";

    public string? Lifecycle { get; init; }

    public string StateHash { get; init; } = "";
}
