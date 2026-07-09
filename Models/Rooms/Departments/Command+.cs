using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ КОМАНДОВАНИЯ (Command+)
// ============================================================

public abstract class CommandPlusRoomType : RoomType
{
    public override string Category => "Command+";
    public override string FloorProto => "FloorSteel";
}

public class Bridge : CommandPlusRoomType
{
    public override string Name => "Bridge";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 80, 120);
    public override Color LineColor => Color.FromArgb(255, 60, 80, 120);
}

public class ConferenceRoom : CommandPlusRoomType
{
    public override string Name => "ConferenceRoom";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 80, 100, 140);
    public override Color LineColor => Color.FromArgb(255, 80, 100, 140);
}

public class NTRep : CommandPlusRoomType
{
    public override string Name => "NTRep";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 40, 60, 100);
    public override Color LineColor => Color.FromArgb(255, 40, 60, 100);
}

public class BlueShield : CommandPlusRoomType
{
    public override string Name => "BlueShield";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 60, 150);
    public override Color LineColor => Color.FromArgb(255, 30, 60, 150);
}