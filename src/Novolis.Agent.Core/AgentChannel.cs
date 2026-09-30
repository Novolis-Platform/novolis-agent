namespace Novolis.Agent.Core;

/// <summary>Duplex framed link (request / response / event / fault).</summary>
public interface IAgentChannel : IAsyncDisposable
{
    string TransportKind { get; }

    IAsyncEnumerable<AgentFrame> ReadFramesAsync(CancellationToken cancellationToken = default);

    ValueTask SendAsync(AgentFrame frame, CancellationToken cancellationToken = default);
}
