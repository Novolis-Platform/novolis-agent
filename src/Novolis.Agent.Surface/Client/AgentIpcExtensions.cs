using Novolis.Agent.Core;
using Novolis.Transports.LocalIpc;

namespace Novolis.Agent.Surface;

public static class AgentIpcExtensions
{
    public static ValueTask SendMessageAsync<T>(
        this ILocalIpcConnection connection,
        long sequence,
        string kind,
        string name,
        T payload,
        CancellationToken cancellationToken = default) =>
        connection.SendAsync(
            new LocalIpcFrame(sequence, kind, name, AgentProtocolCodec.Serialize(payload)),
            cancellationToken);
}
