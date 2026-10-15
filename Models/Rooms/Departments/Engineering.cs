using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ ИНЖЕНЕРНОГО ОТДЕЛА
// ============================================================

public abstract class EngineeringRoomType : RoomType
{
    public override string Category => "Engineering";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 239, 179, 65);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
}

// ============================================================
// КОМНАТЫ ИНЖЕНЕРИИ
// ============================================================

public class Engineering : EngineeringRoomType
{
    public override string Name => "Engineering";
    public override string DisplayName => "Инженерный отсек";
    public override string Description => "Инженерная комната. Основной отсек для инженерных работ и обслуживания систем станции.";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringMain";
    public override string? LockerProto => "LockerEngineerFilled";
    public override string? SuitStorageProto => "SuitStorageEngi";
    public override int Priority => 10;
}

public class Atmospherics : AtmosRoomType
{
    public override string Name => "Atmospherics";
    public override string DisplayName => "Атмосия";
    public override string Description => "Атмосферная станция. Помещение для обслуживания систем воздуха и давления на станции.";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override string GlassDoorProto => "AirlockAtmosphericsGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringAtmosMain";
    public override string? LockerProto => "LockerAtmosphericsFilled";
    public override string? SuitStorageProto => "SuitStorageAtmos";
    public override int Priority => 30;
}

public class External : EngineeringRoomType
{
    public override string Name => "External";
    public override string DisplayName => "Внешний";
    public override string Description => "Внешняя инженерная комната. Открытый отсек для внешних работ и обслуживания корпуса станции.";
    public override string DoorProto => "AirlockExternalLocked";
    public override string GlassDoorProto => "AirlockExternalGlassLocked";
    public override string? HolopadProto => "HolopadEngineeringFront";
    public override string? SuitStorageProto => "SuitStorageEVAEmergency";
    public override int Priority => 50;
}

public class ChiefEngineer : EngineeringRoomType
{
    public override string Name => "ChiefEngineer";
    public override string DisplayName => "Старший инженер";
    public override string Description => "Кабинет главного инженера. Офис руководителя инженерного отдела станции.";
    public override string DoorProto => "AirlockChiefEngineerLocked";
    public override string GlassDoorProto => "AirlockChiefEngineerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
    public override string? HolopadProto => "HolopadCommandCe";
    public override string? LockerProto => "LockerChiefEngineerFilled";
    public override string? SuitStorageProto => "SuitStorageCE";
    public override string? BedProto => "Bed";
    public override string? BedsheetProto => "BedsheetCE";
    public override string? DresserProto => "DresserChiefEngineerFilled";
    public override int Priority => 250;
}
