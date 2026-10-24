using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

public sealed class AgentDispatchContext
{
    public AgentDispatchContext(AgentEventBroker broker, IAgentEventSink sink)
    {
        Broker = broker ?? throw new ArgumentNullException(nameof(broker));
        Sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public AgentEventBroker Broker { get; }

    public IAgentEventSink Sink { get; }
}
