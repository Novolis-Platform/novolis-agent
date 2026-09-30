using MessagePack;

namespace Novolis.Agent.Core;

public static class AgentProtocolCodec
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    public static byte[] Serialize<T>(T value) => MessagePackSerializer.Serialize(value!, Options);

    public static T Deserialize<T>(byte[] payload) =>
        MessagePackSerializer.Deserialize<T>(payload, Options);

    public static T Deserialize<T>(ReadOnlyMemory<byte> payload) =>
        MessagePackSerializer.Deserialize<T>(payload, Options);
}
