namespace Novolis.Agent.Core;

/// <summary>In-process agent control host: snapshot, commands, and push events.</summary>
public interface IAgentHost
{
    AgentHello Hello();

    AgentSnapshot Snapshot();

    AgentActionsResponse Actions();

    AgentCommandResult Execute(AgentCommand command);

    /// <summary>Optional decision-gate release. Default hosts may return a no-op success.</summary>
    AgentCommandResult Continue();

    void Subscribe();

    event Action<AgentDecisionEvent>? Decision;

    event Action<AgentChangedEvent>? Changed;

    event Action<AgentActionResultEvent>? ActionResult;
}
