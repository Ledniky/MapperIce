using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ ОТДЕЛА СНАБЖЕНИЯ
// ============================================================

public abstract class CargoRoomType : RoomType
{
    public override string Category => "Cargo";
    public override string FloorProto => "FloorSteel";
}

// ============================================================
// КОМНАТЫ СНАБЖЕНИЯ
// ============================================================

public class Cargo : CargoRoomType
{
    public override string Name => "Cargo";
    public override string DoorProto => "AirlockCargoLocked";
    public override string GlassDoorProto => "AirlockCargoGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 164, 97, 6);
    public override Color LineColor => Color.FromArgb(255, 164, 97, 6);
}

public class Salvage : CargoRoomType
{
    public override string Name => "Salvage";
    public override string DoorProto => "AirlockSalvageLocked";
    public override string GlassDoorProto => "AirlockSalvageGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 141, 28, 153);
    public override Color LineColor => Color.FromArgb(255, 141, 28, 153);
}

public class Mining : CargoRoomType
{
    public override string Name => "Mining";
    public override string DoorProto => "AirlockMiningLocked";
    public override string GlassDoorProto => "AirlockMiningGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 80, 40);
    public override Color LineColor => Color.FromArgb(255, 180, 80, 40);
}

// ============================================================
// КАБИНЕТЫ СНАБЖЕНИЯ
// ============================================================

public class Quartermaster : CargoRoomType
{
    public override string Name => "Quartermaster";
    public override string DoorProto => "AirlockQuartermasterLocked";
    public override string GlassDoorProto => "AirlockQuartermasterGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 164, 97, 6);
}