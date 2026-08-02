namespace Novolis.Agent.Core;

public static class AgentMethodNames
{
    public const string Hello = "agent.hello";
    public const string Snapshot = "agent.snapshot";
    public const string Actions = "agent.actions";
    public const string Command = "agent.command";
    public const string Continue = "agent.continue";
    public const string Subscribe = "agent.subscribe";

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

    private static bool Equals(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

public static class AgentActionIds
{
    public const string Travel = "travel";
    public const string AcceptSpot = "acceptSpot";
    public const string AcceptCharter = "acceptCharter";
    public const string MarketBuy = "marketBuy";
    public const string MarketSell = "marketSell";
    public const string Depart = "depart";
    public const string RefuseStandby = "refuseStandby";
    public const string AcceptStandby = "acceptStandby";
    public const string Wait = "wait";
    public const string Premium = "premium";
    public const string Overhaul = "overhaul";
    public const string Step = "step";
    public const string Continue = "continue";
    public const string Resume = "resume";
    public const string Save = "save";
    public const string SetClock = "setClock";
    public const string CancelStack = "cancelStack";
    public const string PrepareDepart = "prepareDepart";
    /// <summary>Framebuffer / scene click — params <c>x</c>, <c>y</c> (pixels) or typed <see cref="AgentCommand.X"/>/<see cref="AgentCommand.Y"/>.</summary>
    public const string Click = "click";
}
