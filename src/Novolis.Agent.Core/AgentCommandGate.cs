namespace Novolis.Agent.Core;

/// <summary>Shared rejection rules for revision, approval, and declared argument schemas.</summary>
public static class AgentCommandGate
{
    public static AgentCommandResult? Reject(AgentAction? action, AgentCommand command, long currentRevision)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ExpectedRevision is long expected && expected != currentRevision)
        {
            return Fail(command, currentRevision, "stale-snapshot", "State revision changed; refresh the snapshot and retry.");
        }

        if (action is { ApprovalRequired: true } && string.IsNullOrWhiteSpace(command.ApprovalToken))
        {
            var preview = string.IsNullOrWhiteSpace(action.EffectPreview)
                ? "Approval token required before this mutation."
                : action.EffectPreview;
            return Fail(command, currentRevision, "approval-required", preview);
        }

        if (action?.Schema is not null && !AgentSchemaValidator.TryValidate(action.Schema, AgentCommandJson.Instance(command), out var error))
        {
            return Fail(command, currentRevision, "invalid-arguments", error);
        }

        return null;
    }

    public static AgentReceipt CreateReceipt(
        AgentCommand command,
        string lifecycle,
        bool ok,
        string message,
        long? stateRevision,
        string? reasonCode = null,
        string? recovery = null,
        AgentEffect[]? effects = null,
        AgentArtifactRef[]? artifacts = null) => new()
    {
        CorrelationId = command.CorrelationId ?? "",
        ActionId = command.ActionId,
        Lifecycle = lifecycle,
        Ok = ok,
        ReasonCode = reasonCode,
        Message = message,
        StateRevision = stateRevision,
        Recovery = recovery,
        Effects = effects ?? [],
        Artifacts = artifacts ?? [],
    };

    private static AgentCommandResult Fail(AgentCommand command, long revision, string reason, string message)
    {
        var receipt = CreateReceipt(command, AgentLifecycle.Rejected, false, message, revision, reason, message);
        return new AgentCommandResult
        {
            Ok = false,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            ErrorCode = reason,
            Message = message,
            StateRevision = revision,
            Receipt = receipt,
        };
    }
}
