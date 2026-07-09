using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ ИНЖЕНЕРИИ (Engineering+)
// ============================================================

public abstract class EngineeringPlusRoomType : RoomType
{
    public override string Category => "Engineering+";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
}

public class GravityGenerator : EngineeringPlusRoomType
{
    public override string Name => "GravityGenerator";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 180, 220);
    public override Color LineColor => Color.FromArgb(255, 180, 180, 220);
}

public class Supermatter : EngineeringPlusRoomType
{
    public override string Name => "Supermatter";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 200, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 200, 100);
}

public class Solars : EngineeringPlusRoomType
{
    public override string Name => "Solars";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 220, 200, 100);
    public override Color LineColor => Color.FromArgb(255, 220, 200, 100);
}

public class Telecoms : EngineeringPlusRoomType
{
    public override string Name => "Telecoms";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 150, 180, 200);
}

public class Router : EngineeringPlusRoomType
{
    public override string Name => "Router";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 140, 170, 190);
    public override Color LineColor => Color.FromArgb(255, 140, 170, 190);
}