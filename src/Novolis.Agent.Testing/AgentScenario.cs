namespace Novolis.Agent.Testing;

public sealed class AgentScenario
{
    public string Name { get; init; } = "";

    public string[] PermittedCapabilities { get; init; } = [];

    public AgentScenarioStep[] Steps { get; init; } = [];
}
