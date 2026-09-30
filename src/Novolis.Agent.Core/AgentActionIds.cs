namespace Novolis.Agent.Core;

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
