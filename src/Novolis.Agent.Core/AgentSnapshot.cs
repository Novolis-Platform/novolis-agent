using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentSnapshot
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public int Day { get; set; }
    [Key(2)] public string SeedHash { get; set; } = "";
    [Key(3)] public string HubId { get; set; } = "";
    [Key(4)] public string HubName { get; set; } = "";
    [Key(5)] public string PauseReason { get; set; } = "Running";
    [Key(6)] public Dictionary<string, string> StatusLines { get; set; } = new(StringComparer.Ordinal);
    [Key(7)] public bool Underway { get; set; }
    [Key(8)] public bool DockedIdle { get; set; }
    [Key(9)] public bool Complete { get; set; }
    [Key(10)] public bool SoftFail { get; set; }
    [Key(11)] public bool StandbyOffer { get; set; }
    [Key(12)] public string? TravelTargetSystemId { get; set; }
    [Key(13)] public string[] RouteSystemIds { get; set; } = [];
    [Key(14)] public AgentBoard[] Boards { get; set; } = [];
    [Key(15)] public string[] Manifest { get; set; } = [];
    [Key(16)] public AgentAction[] Actions { get; set; } = [];
    [Key(17)] public AgentLastAction? LastAction { get; set; }
    [Key(18)] public string Attention { get; set; } = "runAlways";
    [Key(19)] public double SimSpeedScale { get; set; } = 1.0;
    [Key(20)] public string[] IntentStack { get; set; } = [];
    [Key(21)] public double MapX { get; set; }
    [Key(22)] public double MapY { get; set; }
    [Key(23)] public bool MapVisible { get; set; }
    [Key(24)] public double GameHoursPerRealMinute { get; set; }
    [Key(25)] public double SessionGameHoursPerRealMinute { get; set; }

    // Scene-oriented fields (JSON clients)
    [Key(26)] public string DocumentName { get; set; } = "";
    [Key(27)] public int NodeCount { get; set; }
    [Key(28)] public string? SelectionId { get; set; }
    [Key(29)] public string? ActiveCameraId { get; set; }
    [Key(30)] public object? Document { get; set; }

    public string Line(string key) =>
        StatusLines.TryGetValue(key, out var value) ? value : "";

    public AgentBoardItem[] BoardItems(string boardId)
    {
        foreach (var board in Boards)
        {
            if (string.Equals(board.Id, boardId, StringComparison.Ordinal))
                return board.Items;
        }

        return [];
    }
}
