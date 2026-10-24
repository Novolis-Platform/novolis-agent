namespace Novolis.Agent.Core;

public static class AgentMethodNames
{
    public const string Hello = "agent.hello";
    public const string Snapshot = "agent.snapshot";
    public const string Actions = "agent.actions";
    public const string Command = "agent.command";
    public const string Continue = "agent.continue";
    public const string Subscribe = "agent.subscribe";
    public const string Watch = "agent.watch";
    public const string WatchNext = "agent.watch.next";
    public const string Unwatch = "agent.unwatch";
    public const string Event = "agent.event";
    public const string WatchClosed = "agent.watchClosed";

    public const string Decision = "agent.decision";
    public const string Changed = "agent.changed";
    public const string ActionResult = "agent.actionResult";

    /// <summary>Legacy wire aliases accepted by hosts during migration.</summary>
    public static class Legacy
    {
        public const string Hello = "session.hello";
        public const string Snapshot = "session.snapshot";
        public const string Actions = "session.actions";
        public const string Command = "session.command";
        public const string Continue = "session.continue";
        public const string Subscribe = "session.subscribe";
        public const string Decision = "session.decision";
        public const string Changed = "session.changed";
        public const string ActionResult = "session.actionResult";
    }

    public static bool IsHello(string? method) =>
        Equals(method, Hello) || Equals(method, Legacy.Hello) || Equals(method, "hello");

    public static bool IsSnapshot(string? method) =>
        Equals(method, Snapshot) || Equals(method, Legacy.Snapshot) || Equals(method, "snapshot");

    public static bool IsActions(string? method) =>
        Equals(method, Actions) || Equals(method, Legacy.Actions) || Equals(method, "actions");

    public static bool IsCommand(string? method) =>
        Equals(method, Command) || Equals(method, Legacy.Command) || Equals(method, "command");

    public static bool IsContinue(string? method) =>
        Equals(method, Continue) || Equals(method, Legacy.Continue) || Equals(method, "continue");

    public static bool IsSubscribe(string? method) =>
        Equals(method, Subscribe) || Equals(method, Legacy.Subscribe) || Equals(method, "subscribe");

    public static bool IsWatch(string? method) =>
        Equals(method, Watch) || Equals(method, "watch");

    public static bool IsWatchNext(string? method) =>
        Equals(method, WatchNext) || Equals(method, "watch.next");

    public static bool IsUnwatch(string? method) =>
        Equals(method, Unwatch) || Equals(method, "unwatch");

    public static string CanonicalEventName(string? method)
    {
        if (IsDecision(method)) return Decision;
        if (IsChanged(method)) return Changed;
        if (IsActionResult(method)) return ActionResult;
        return method ?? "";
    }

    public static bool IsDecision(string? method) =>
        Equals(method, Decision) || Equals(method, Legacy.Decision) || Equals(method, "decision");

    public static bool IsChanged(string? method) =>
        Equals(method, Changed) || Equals(method, Legacy.Changed) || Equals(method, "changed");

    public static bool IsActionResult(string? method) =>
        Equals(method, ActionResult) || Equals(method, Legacy.ActionResult) || Equals(method, "actionResult");

    private static bool Equals(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
