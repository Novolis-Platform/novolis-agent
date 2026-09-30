namespace Novolis.Agent.Surface;

/// <summary>Maps a method used on the wire (<c>agent.hello</c>, ...) to a member for capability discovery.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentMethodAttribute : Attribute
{
    public AgentMethodAttribute(string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Method = method;
    }

    public string Method { get; }
}
