using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ КОМАНДОВАНИЯ (Command+)
// ============================================================

public abstract class CommandPlusRoomType : CommandRoomType
{
    public override string Category => "Command+";
    public override string FloorProto => "FloorSteel";

}

public class ConferenceRoom : CommandPlusRoomType
{
    public override string Name => "ConferenceRoom";
    public override string DisplayName => "Конференц-зал";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override int Priority => 150;
}

public class CentralCommand : CommandPlusRoomType
{
    public override string Name => "CentralCommand";
    public override string DisplayName => "Центральное командование";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockCentralCommandLocked";
    public override string GlassDoorProto => "AirlockCentralCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 80, 30);
    public override Color LineColor => Color.FromArgb(255, 60, 160, 60);
    public override int Priority => 400;
}

public class AI : CommandPlusRoomType
{
    public override string Name => "AI";
    public override string DisplayName => "ИИ";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override int Priority => 170;
}

public class SatelliteAI : CommandPlusRoomType
{
    public override string Name => "Satellite";
    public override string DisplayName => "Спутник ИИ";
    public override string WallProto => "WallReinforced";
    public override int Priority => 160;
}
