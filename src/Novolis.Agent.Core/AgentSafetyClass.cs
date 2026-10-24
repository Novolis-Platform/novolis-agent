namespace Novolis.Agent.Core;

/// <summary>Whether an action observes state, changes it, or cannot be undone.</summary>
public static class AgentSafetyClass
{
    public const string Read = "read";
    public const string Mutate = "mutate";
    public const string Irreversible = "irreversible";
}
