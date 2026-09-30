namespace Novolis.Agent.Core;

public sealed record AgentFrame(
    long Sequence,
    string Kind,
    string Method,
    ReadOnlyMemory<byte> Payload);
