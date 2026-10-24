namespace Novolis.Agent.Testing;

public sealed class AgentScenarioStep
{
    public string ActionId { get; init; } = "";

    public Dictionary<string, string> Params { get; init; } = new(StringComparer.Ordinal);

    public string? ArgumentsJson { get; init; }

    public string? CorrelationId { get; init; }

    public string? ApprovalToken { get; init; }

    public long? ExpectedRevision { get; init; }

    public bool ExpectOk { get; init; } = true;

    public string? ExpectReason { get; init; }

    public string? ExpectMessageContains { get; init; }

    public bool AwaitActionResult { get; init; }
}
