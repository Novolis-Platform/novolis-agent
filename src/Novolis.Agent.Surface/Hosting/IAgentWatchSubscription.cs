using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

public interface IAgentWatchSubscription : IAsyncDisposable
{
    string WatchId { get; }

    long EstablishedThroughSequence { get; }

    Task<AgentWatchEvent?> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
