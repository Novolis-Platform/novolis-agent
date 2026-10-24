namespace Novolis.Agent.Core;

public static class AgentSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Danger = "danger";

    public static int Rank(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "danger" or "error" or "critical" => 3,
        "warning" or "warn" => 2,
        _ => 1,
    };
}
