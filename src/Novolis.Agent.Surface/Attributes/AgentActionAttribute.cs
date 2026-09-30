namespace Novolis.Agent.Surface;

/// <summary>Declares a command action available via <c>agent.actions</c> / <c>agent.command</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AgentActionAttribute : Attribute
{
    public AgentActionAttribute(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ActionId = actionId;
    }

    public string ActionId { get; }

    public string Summary { get; set; } = "";

    /// <summary>Compact param hint, e.g. <c>lightKind|omni,spot; intensity?</c>.</summary>
    public string Params { get; set; } = "";

    public bool EnabledByDefault { get; set; } = true;
}
