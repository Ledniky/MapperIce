using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ СНАБЖЕНИЯ (Cargo+)
// ============================================================

public abstract class CargoPlusRoomType : RoomType
{
    public override string Category => "Cargo+";
    public override string FloorProto => "FloorSteel";
}

public class CargoOffice : CargoPlusRoomType
{
    public override string Name => "CargoOffice";
    public override string DoorProto => "AirlockCargoLocked";
    public override string GlassDoorProto => "AirlockCargoGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 110, 30);
    public override Color LineColor => Color.FromArgb(255, 180, 110, 30);
}

public class Mailroom : CargoPlusRoomType
{
    public override string Name => "Mailroom";
    public override string DoorProto => "AirlockCargoLocked";
    public override string GlassDoorProto => "AirlockCargoGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 170, 100, 20);
    public override Color LineColor => Color.FromArgb(255, 170, 100, 20);
}

public class Recycling : CargoPlusRoomType
{
    public override string Name => "Recycling";
    public override string DoorProto => "AirlockCargoLocked";
    public override string GlassDoorProto => "AirlockCargoGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 120, 40);
    public override Color LineColor => Color.FromArgb(255, 150, 120, 40);
}