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

// ============================================================
// ОБЩАЯ АТМОСОВСКАЯ БАЗА (все атмос-комнаты наследуют её, цвет атмосов — здесь)
// ============================================================

public abstract class AtmosRoomType : EngineeringRoomType
{
    public override string FloorProto => "FloorSteel";
    public override Color FillColor => Color.FromArgb(100, 62, 179, 136);
    public override Color LineColor => Color.FromArgb(255, 62, 179, 136);
    // Атмос-комнаты показываем ПОСЛЕ всех инженерных, а не вперемешку с ними
    public override int SortGroup => 1;
}

public class GravityGenerator : EngineeringPlusRoomType
{
    public override string Name => "GravityGenerator";
    public override string DisplayName => "Генератор гравитации";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringPower";
    public override int Priority => 10;
}

public class Supermatter : EngineeringPlusRoomType
{
    public override string Name => "Supermatter";
    public override string DisplayName => "Суперматерия";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringAME";
    public override int Priority => 10;
}

public class Solars : EngineeringPlusRoomType
{
    public override string Name => "Solars";
    public override string DisplayName => "Солнечные панели";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringPower";
    public override int Priority => 10;
}

public class Telecoms : EngineeringPlusRoomType
{
    public override string Name => "Telecoms";
    public override string DisplayName => "Телекоммуникации";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringTelecoms";
    public override int Priority => 10;
}

public class Router : EngineeringPlusRoomType
{
    public override string Name => "Router";
    public override string DisplayName => "Маршрутизатор";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringTelecoms";
    public override int Priority => 10;
}

public class AtmosFront : AtmosRoomType
{
    public override string Name => "AtmosFront";
    public override string DisplayName => "Раздевалка атмосферы";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override string GlassDoorProto => "AirlockAtmosphericsGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringAtmosFront";
    public override int Priority => 25;
}

public class AtmosTeg : AtmosRoomType
{
    public override string Name => "AtmosTeg";
    public override string DisplayName => "ТЭГ";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override string GlassDoorProto => "AirlockAtmosphericsGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringAtmosTeg";
    public override int Priority => 25;
}

public class EngineeringStorage : EngineeringPlusRoomType
{
    public override string Name => "EngineeringStorage";
    public override string DisplayName => "Инженерный склад";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringStorage";
    public override string? LockerProto => "LockerElectricalSuppliesFilled";
    public override int Priority => 10;
}

public class EngineeringBreakroom : EngineeringPlusRoomType
{
    public override string Name => "EngineeringBreakroom";
    public override string DisplayName => "Инженерная комната отдыха";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringBreakroom";
    public override int Priority => 10;
}

public class TechVault : EngineeringPlusRoomType
{
    public override string Name => "TechVault";
    public override string DisplayName => "Хранилище плат";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringTechVault";
    public override int Priority => 10;
}
