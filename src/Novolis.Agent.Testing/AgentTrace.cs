namespace Novolis.Agent.Testing;

public sealed class AgentTrace
{
    public string Scenario { get; init; } = "";

    public List<AgentTraceStep> Steps { get; init; } = [];
}
