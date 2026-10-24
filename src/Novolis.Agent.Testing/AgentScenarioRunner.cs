using Novolis.Agent.Core;

namespace Novolis.Agent.Testing;

/// <summary>Runs a recorded action script against any host. Live model calls stay outside this runner.</summary>
public static class AgentScenarioRunner
{
    public static async Task<AgentScenarioReport> RunAsync(
        IAgentHost host,
        AgentScenario scenario,
        TimeSpan? awaitTimeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scenario);
        var timeout = awaitTimeout ?? TimeSpan.FromSeconds(5);
        var trace = new AgentTrace { Scenario = scenario.Name };
        var report = new AgentScenarioReport { Scenario = scenario.Name, Trace = trace };
        var pending = new TaskCompletionSource<AgentActionResultEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnResult(AgentActionResultEvent evt) => pending.TrySetResult(evt);
        host.ActionResult += OnResult;
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in scenario.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var action = host.Actions().Actions.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, step.ActionId, StringComparison.Ordinal));
                if (action is not null
                    && scenario.PermittedCapabilities.Length > 0
                    && action.Capability.Length > 0
                    && !scenario.PermittedCapabilities.Contains(action.Capability, StringComparer.Ordinal))
                {
                    return Diverge(report, $"Capability '{action.Capability}' is not permitted for '{step.ActionId}'.");
                }

                if (step.CorrelationId is not null && !seen.Add(step.CorrelationId))
                {
                    report.Retries++;
                }

                pending = new TaskCompletionSource<AgentActionResultEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                var command = new AgentCommand
                {
                    ActionId = step.ActionId,
                    CorrelationId = step.CorrelationId,
                    ApprovalToken = step.ApprovalToken,
                    ExpectedRevision = step.ExpectedRevision,
                    ArgumentsJson = step.ArgumentsJson,
                };
                foreach (var pair in step.Params)
                {
                    command.Params[pair.Key] = pair.Value;
                }

                var result = host.Execute(command);
                if (step.AwaitActionResult)
                {
                    var finished = await Task.WhenAny(pending.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
                    if (finished != pending.Task)
                    {
                        return Diverge(report, $"Timed out waiting for '{step.ActionId}'.");
                    }
                }

                trace.Steps.Add(new AgentTraceStep
                {
                    ActionId = result.ActionId,
                    CorrelationId = result.CorrelationId,
                    StateRevision = result.StateRevision,
                    Ok = result.Ok,
                    ReasonCode = result.ErrorCode ?? result.Receipt?.ReasonCode,
                    Message = result.Message,
                    Lifecycle = result.Receipt?.Lifecycle,
                    StateHash = $"{result.StateRevision}:{result.Ok}:{result.ActionId}:{result.Message}",
                });
                report.CompletedSteps++;
                if (result.Receipt is null)
                {
                    report.EvidenceComplete = false;
                }

                if (!result.Ok && string.IsNullOrWhiteSpace(result.Receipt?.Recovery))
                {
                    report.RecoveryRecorded = false;
                }

                if (result.Ok != step.ExpectOk)
                {
                    return Diverge(report, $"'{step.ActionId}' ok={result.Ok}, expected {step.ExpectOk}. {result.Message}");
                }

                if (step.ExpectReason is not null
                    && !string.Equals(step.ExpectReason, result.ErrorCode ?? result.Receipt?.ReasonCode, StringComparison.Ordinal))
                {
                    return Diverge(report, $"'{step.ActionId}' reason '{result.ErrorCode}', expected '{step.ExpectReason}'.");
                }

                if (step.ExpectMessageContains is not null
                    && !result.Message.Contains(step.ExpectMessageContains, StringComparison.Ordinal))
                {
                    return Diverge(report, $"'{step.ActionId}' message did not contain '{step.ExpectMessageContains}'.");
                }

                if (action is { ApprovalRequired: true } && result.Ok && string.IsNullOrWhiteSpace(step.ApprovalToken))
                {
                    report.ApprovalCompliant = false;
                    return Diverge(report, $"'{step.ActionId}' applied without an approval token.");
                }
            }

            report.Passed = true;
            return report;
        }
        finally
        {
            host.ActionResult -= OnResult;
        }
    }

    private static AgentScenarioReport Diverge(AgentScenarioReport report, string message)
    {
        report.Passed = false;
        report.FirstDivergence = message;
        return report;
    }
}
