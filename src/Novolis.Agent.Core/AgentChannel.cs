namespace Novolis.Agent.Core;

/// <summary>Duplex framed link (request / response / event / fault).</summary>
public interface IAgentChannel : IAsyncDisposable
{
    string TransportKind { get; }

    IAsyncEnumerable<AgentFrame> ReadFramesAsync(CancellationToken cancellationToken = default);

    ValueTask SendAsync(AgentFrame frame, CancellationToken cancellationToken = default);
}

public sealed record AgentFrame(
    long Sequence,
    string Kind,
    string Method,
    ReadOnlyMemory<byte> Payload);

public static class AgentFrameKinds
{
    public const string Request = "request";
    public const string Response = "response";
    public const string Event = "event";
    public const string Fault = "fault";
}
