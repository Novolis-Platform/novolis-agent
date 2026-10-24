namespace Novolis.Agent.Core;

/// <summary>Terminal and in-progress states for one correlated request.</summary>
public static class AgentLifecycle
{
    public const string Preview = "preview";
    public const string Accepted = "accepted";
    public const string Queued = "queued";
    public const string Deferred = "deferred";
    public const string Applied = "applied";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}
