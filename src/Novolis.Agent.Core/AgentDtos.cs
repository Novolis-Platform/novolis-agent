using MessagePack;

namespace Novolis.Agent.Core;

[MessagePackObject]
public sealed class AgentHello
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string ProtocolVersion { get; set; } = "1.0";
    [Key(2)] public string AppId { get; set; } = "";
    [Key(3)] public string AppTitle { get; set; } = "";
    [Key(4)] public int ProcessId { get; set; }
    [Key(5)] public string[] Capabilities { get; set; } = [];
    [Key(6)] public string SurfaceId { get; set; } = "";
    [Key(7)] public string? Description { get; set; }
    [Key(8)] public int? HttpPort { get; set; }
    [Key(9)] public int? TcpPort { get; set; }
    [Key(10)] public string? DocumentUrl { get; set; }
    [Key(11)] public string? WebSocketUrl { get; set; }
}

[MessagePackObject]
public sealed class AgentHelloRequest
{
    [Key(0)] public long Sequence { get; set; }
}

[MessagePackObject]
public sealed class AgentSnapshotRequest
{
    [Key(0)] public long Sequence { get; set; }
}

[MessagePackObject]
public sealed class AgentActionsRequest
{
    [Key(0)] public long Sequence { get; set; }
}

[MessagePackObject]
public sealed class AgentSubscribeRequest
{
    [Key(0)] public long Sequence { get; set; }
}

[MessagePackObject]
public sealed class AgentSubscribeResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; } = true;
}

[MessagePackObject]
public sealed class AgentContinueRequest
{
    [Key(0)] public long Sequence { get; set; }
}

[MessagePackObject]
public sealed class AgentCommandRequest
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public AgentCommand Command { get; set; } = new();
}

/// <summary>Command envelope: action id plus string parameter bag and optional typed fields.</summary>
[MessagePackObject]
public sealed class AgentCommand
{
    [Key(0)] public string ActionId { get; set; } = "";

    [Key(1)] public Dictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);

    // Scene / CAD typed optional fields (JSON HTTP); ignored by MessagePack key layout beyond 1 when unset.
    [IgnoreMember] public string? Path { get; set; }
    [IgnoreMember] public string? NodeId { get; set; }
    [IgnoreMember] public string? ParentId { get; set; }
    [IgnoreMember] public string? LightKind { get; set; }
    [IgnoreMember] public string? Name { get; set; }
    [IgnoreMember] public float? Intensity { get; set; }
    [IgnoreMember] public float? X { get; set; }
    [IgnoreMember] public float? Y { get; set; }
    [IgnoreMember] public float? Z { get; set; }
    [IgnoreMember] public float? Rx { get; set; }
    [IgnoreMember] public float? Ry { get; set; }
    [IgnoreMember] public float? Rz { get; set; }
    [IgnoreMember] public string? GeneratorKind { get; set; }
    [IgnoreMember] public string? ModifierKind { get; set; }
    [IgnoreMember] public string? SourceId { get; set; }
    [IgnoreMember] public string? InputId { get; set; }
    [IgnoreMember] public string? TargetId { get; set; }
    [IgnoreMember] public string? CutterId { get; set; }
    [IgnoreMember] public string? BooleanKind { get; set; }
    [IgnoreMember] public string? Primitive { get; set; }
    [IgnoreMember] public int? Segments { get; set; }
    [IgnoreMember] public float? Distance { get; set; }
    [IgnoreMember] public int? Count { get; set; }
    [IgnoreMember] public string? Axis { get; set; }
    [IgnoreMember] public string? MaterialColor { get; set; }
    [IgnoreMember] public string? EditMode { get; set; }
    [IgnoreMember] public string? DisplayMode { get; set; }
    [IgnoreMember] public string? Indices { get; set; }
    [IgnoreMember] public bool? Additive { get; set; }
    [IgnoreMember] public Dictionary<string, object?>? Extra { get; set; }

    public string? Get(string key) =>
        Params.TryGetValue(key, out var value) ? value : null;

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        var raw = Get(key);
        return raw is not null && int.TryParse(raw, out value);
    }

    public bool TryGetDouble(string key, out double value)
    {
        value = 0;
        var raw = Get(key);
        return raw is not null && double.TryParse(raw, out value);
    }

    public bool TryGetBool(string key, out bool value)
    {
        value = false;
        var raw = Get(key);
        if (raw is null)
            return false;
        if (bool.TryParse(raw, out value))
            return true;
        if (raw is "1" or "yes")
        {
            value = true;
            return true;
        }

        if (raw is "0" or "no")
        {
            value = false;
            return true;
        }

        return false;
    }

    public AgentCommand With(string key, string? value)
    {
        if (value is not null)
            Params[key] = value;
        return this;
    }

    public AgentCommand With(string key, int value) => With(key, value.ToString());

    public AgentCommand With(string key, double value) => With(key, value.ToString("R"));

    public AgentCommand With(string key, bool value) => With(key, value ? "true" : "false");
}

public static class AgentCommandKeys
{
    public const string DestSystemId = "destSystemId";
    public const string OriginSystemId = "originSystemId";
    public const string Index = "index";
    public const string Sku = "sku";
    public const string Qty = "qty";
    public const string Profile = "profile";
    public const string Label = "label";
    public const string Attention = "attention";
    public const string Speed = "speed";
    public const string Prepare = "prepare";
}

[MessagePackObject]
public sealed class AgentCommandResult
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public bool Ok { get; set; }
    [Key(2)] public string ActionId { get; set; } = "";
    [Key(3)] public string Message { get; set; } = "";
    [Key(4)] public string? ErrorCode { get; set; }
    [Key(5)] public AgentSnapshot? Snapshot { get; set; }
    [Key(6)] public string? NodeId { get; set; }
}

[MessagePackObject]
public sealed class AgentAction
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public string Label { get; set; } = "";
    [Key(2)] public bool Enabled { get; set; }
    [Key(3)] public string? DisabledReason { get; set; }
    [Key(4)] public string Summary { get; set; } = "";
    [Key(5)] public string Params { get; set; } = "";
    [Key(6)] public Dictionary<string, object?>? Schema { get; set; }
}

[MessagePackObject]
public sealed class AgentActionsResponse
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public AgentAction[] Actions { get; set; } = [];
}

[MessagePackObject]
public sealed class AgentLastAction
{
    [Key(0)] public string ActionId { get; set; } = "";
    [Key(1)] public bool Ok { get; set; }
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public string? ErrorCode { get; set; }
}

[MessagePackObject]
public sealed class AgentBoardItem
{
    [Key(0)] public int Index { get; set; }
    [Key(1)] public string Id { get; set; } = "";
    [Key(2)] public string Label { get; set; } = "";
    [Key(3)] public string Detail { get; set; } = "";
    [Key(4)] public bool CanAct { get; set; }
}

[MessagePackObject]
public sealed class AgentBoard
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public AgentBoardItem[] Items { get; set; } = [];
}

public static class AgentBoardIds
{
    public const string SpotFreight = "spotFreight";
    public const string GoodsCharters = "goodsCharters";
    public const string MarketLots = "marketLots";
}

public static class AgentLineKeys
{
    public const string Voyage = "voyage";
    public const string Hull = "hull";
    public const string Cash = "cash";
    public const string Standing = "standing";
    public const string Decision = "decision";
    public const string Coach = "coach";
    public const string SoftFail = "softFail";
    public const string Survival = "survival";
    public const string Mesh = "mesh";
    public const string Hold = "hold";
    public const string Pace = "pace";
}

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

[MessagePackObject]
public sealed class AgentDecisionEvent
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public int Day { get; set; }
    [Key(2)] public string HubId { get; set; } = "";
    [Key(3)] public string DecisionLine { get; set; } = "";
    [Key(4)] public AgentSnapshot? Snapshot { get; set; }
}

[MessagePackObject]
public sealed class AgentChangedEvent
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string Reason { get; set; } = "";
    [Key(2)] public AgentSnapshot? Snapshot { get; set; }
    [Key(3)] public string? DocumentName { get; set; }
    [Key(4)] public int NodeCount { get; set; }
}

[MessagePackObject]
public sealed class AgentActionResultEvent
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string ActionId { get; set; } = "";
    [Key(2)] public bool Ok { get; set; }
    [Key(3)] public string Message { get; set; } = "";
    [Key(4)] public string? ErrorCode { get; set; }
    [Key(5)] public AgentSnapshot? Snapshot { get; set; }
}

[MessagePackObject]
public sealed class AgentFault
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public string Message { get; set; } = "";
}

public static class AgentProtocolCodec
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    public static byte[] Serialize<T>(T value) => MessagePackSerializer.Serialize(value!, Options);

    public static T Deserialize<T>(byte[] payload) =>
        MessagePackSerializer.Deserialize<T>(payload, Options);

    public static T Deserialize<T>(ReadOnlyMemory<byte> payload) =>
        MessagePackSerializer.Deserialize<T>(payload, Options);
}
