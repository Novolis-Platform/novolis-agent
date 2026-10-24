namespace Novolis.Agent.Testing;

public sealed class AgentScenarioReport
{
    public string Scenario { get; init; } = "";

    public bool Passed { get; set; }

    public string? FirstDivergence { get; set; }

    public int CompletedSteps { get; set; }

    public int Retries { get; set; }

    public bool ApprovalCompliant { get; set; } = true;

    public bool EvidenceComplete { get; set; } = true;

    public bool RecoveryRecorded { get; set; } = true;

    public AgentTrace Trace { get; init; } = new();
}
