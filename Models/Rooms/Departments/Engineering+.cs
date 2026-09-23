using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ ИНЖЕНЕРИИ (Engineering+)
// ============================================================

public abstract class EngineeringPlusRoomType : EngineeringRoomType
{
    public override string Category => "Engineering+";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
}

public class GravityGenerator : EngineeringPlusRoomType
{
    public override string Name => "GravityGenerator";
    public override string DisplayName => "Генератор гравитации";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override int Priority => 10;
}

public class Supermatter : EngineeringPlusRoomType
{
    public override string Name => "Supermatter";
    public override string DisplayName => "Суперматерия";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override int Priority => 10;
}

public class Solars : EngineeringPlusRoomType
{
    public override string Name => "Solars";
    public override string DisplayName => "Солнечные панели";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override int Priority => 10;
}

public class Telecoms : EngineeringPlusRoomType
{
    public override string Name => "Telecoms";
    public override string DisplayName => "Телекоммуникации";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override int Priority => 10;
}

public class Router : EngineeringPlusRoomType
{
    public override string Name => "Router";
    public override string DisplayName => "Маршрутизатор";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override int Priority => 10;
}
