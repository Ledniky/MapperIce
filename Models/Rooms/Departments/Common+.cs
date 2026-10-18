using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ ОБЩИЕ КОМНАТЫ (Common+)
// ============================================================

public abstract class CommonPlusRoomType : CommonRoomType
{
    public override string Category => "Common+";
}

public class Arrivals : CommonPlusRoomType
{
    public override string Name => "Arrivals";
    public override string DisplayName => "Прибытие";
    public override string? HolopadProto => "HolopadGeneralArrivals";
    public override int Priority => 10;
}

public class Departures : CommonPlusRoomType
{
    public override string Name => "Departures";
    public override string DisplayName => "Отбытие";
    public override string? HolopadProto => "HolopadGeneralEvac";
    public override int Priority => 10;
}

public class ToolStorage : CommonPlusRoomType
{
    public override string Name => "ToolStorage";
    public override string DisplayName => "Хранилище инструментов";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override string? HolopadProto => "HolopadGeneralTools";
    public override string? LockerProto => "ClosetToolFilled";
    public override int Priority => 15;
}

public class Cryo : CommonPlusRoomType
{
    public override string Name => "Cryo";
    public override string DisplayName => "Криосон";
    public override string? HolopadProto => "HolopadGeneralCryosleep";
    public override string? BedProto => "CryogenicSleepUnitSpawnerLateJoin";
    public override int Priority => 10;
}


public class Restaurant : CommonPlusRoomType
{
    public override string Name => "Restaurant";
    public override string DisplayName => "Ресторан";
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override int Priority => 10;
}

public class Disposals : Technical
{
    public override string? HolopadProto => "HolopadGeneralDisposals";
    public override int Priority => 5;
}
