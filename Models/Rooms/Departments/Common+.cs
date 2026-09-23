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
    public override int Priority => 10;
}

public class Departures : CommonPlusRoomType
{
    public override string Name => "Departures";
    public override string DisplayName => "Отбытие";
    public override int Priority => 10;
}

public class ToolStorage : CommonPlusRoomType
{
    public override string Name => "ToolStorage";
    public override string DisplayName => "Хранилище инструментов";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override int Priority => 15;
}

public class Cryo : CommonPlusRoomType
{
    public override string Name => "Cryo";
    public override string DisplayName => "Криосон";
    public override int Priority => 10;
}


public class Restaurant : CommonPlusRoomType
{
    public override string Name => "Restaurant";
    public override string DisplayName => "Ресторан";
    public override int Priority => 10;
}
