using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>Non-blocking destination for one connection's watched events.</summary>
public interface IAgentEventSink
{
    string ConnectionId { get; }

    /// <summary>Returns false when the caller must close the watch because the destination is full.</summary>
    bool TryEnqueue(AgentWatchEvent value);
}
