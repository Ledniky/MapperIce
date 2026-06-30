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
}

// ============================================================
// КОМНАТЫ ИНЖЕНЕРИИ
// ============================================================

public class Engineering : EngineeringRoomType
{
    public override string Name => "Engineering";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 239, 179, 65);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
}

public class Atmospherics : EngineeringRoomType
{
    public override string Name => "Atmospherics";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override string GlassDoorProto => "AirlockAtmosphericsGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 62, 179, 136);
    public override Color LineColor => Color.FromArgb(255, 62, 179, 136);
}

public class External : EngineeringRoomType
{
    public override string Name => "External";
    public override string DoorProto => "AirlockExternalLocked";
    public override string GlassDoorProto => "AirlockExternalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 180, 220);
    public override Color LineColor => Color.FromArgb(255, 100, 180, 220);
}

// ============================================================
// КАБИНЕТЫ ИНЖЕНЕРИИ
// ============================================================

public class ChiefEngineer : EngineeringRoomType
{
    public override string Name => "ChiefEngineer";
    public override string DoorProto => "AirlockChiefEngineerLocked";
    public override string GlassDoorProto => "AirlockChiefEngineerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 220, 160, 50);
    public override Color LineColor => Color.FromArgb(255, 220, 160, 50);
}