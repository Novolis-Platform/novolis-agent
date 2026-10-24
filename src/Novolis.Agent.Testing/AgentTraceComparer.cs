namespace Novolis.Agent.Testing;

public static class AgentTraceComparer
{
    public static string? FirstDivergence(AgentTrace expected, AgentTrace actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var count = Math.Max(expected.Steps.Count, actual.Steps.Count);
        for (var i = 0; i < count; i++)
        {
            if (i >= expected.Steps.Count)
            {
                return $"Extra action '{actual.Steps[i].ActionId}' at step {i}.";
            }

            if (i >= actual.Steps.Count)
            {
                return $"Missing action '{expected.Steps[i].ActionId}' at step {i}.";
            }

            var left = expected.Steps[i];
            var right = actual.Steps[i];
            if (!string.Equals(left.ActionId, right.ActionId, StringComparison.Ordinal)
                || left.Ok != right.Ok
                || left.StateRevision != right.StateRevision
                || !string.Equals(left.StateHash, right.StateHash, StringComparison.Ordinal)
                || !string.Equals(left.ReasonCode, right.ReasonCode, StringComparison.Ordinal))
            {
                return $"Step {i} '{right.ActionId}' diverged (revision {right.StateRevision}, ok {right.Ok}, reason {right.ReasonCode}).";
            }
        }

        return null;
    }
}
