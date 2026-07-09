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

public class ConferenceRoom : CommandPlusRoomType
{
    public override string Name => "ConferenceRoom";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 80, 100, 140);
    public override Color LineColor => Color.FromArgb(255, 80, 100, 140);
}

// NTRep - тусклый тёмно-зелёный
public class NTRep : CommandPlusRoomType
{
    public override string Name => "NTRep";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 80, 30);
    public override Color LineColor => Color.FromArgb(255, 60, 160, 60);
}

// BlueShield - зелёная граница (тусклая), синяя заливка
public class BlueShield : CommandPlusRoomType
{
    public override string Name => "BlueShield";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 60, 150);
    public override Color LineColor => Color.FromArgb(255, 60, 160, 60);
}

// CentralCommand - тусклый тёмно-зелёный
public class CentralCommand : CommandPlusRoomType
{
    public override string Name => "CentralCommand";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockCentralCommandLocked";
    public override string GlassDoorProto => "AirlockCentralCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 80, 30);
    public override Color LineColor => Color.FromArgb(255, 60, 160, 60);
}

// AI - командный доступ и цвет
public class AI : CommandPlusRoomType
{
    public override string Name => "AI";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 30, 50, 100);
}