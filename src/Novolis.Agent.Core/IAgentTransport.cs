namespace Novolis.Agent.Core;

/// <summary>Start/stop a transport that serves an <see cref="IAgentHost"/>.</summary>
public interface IAgentTransport
{
    string Kind { get; }

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
