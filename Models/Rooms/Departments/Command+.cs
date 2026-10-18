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
    public override string? FaxName => "Конференц-зал мостика";
    public override string DisplayName => "Конференц-зал";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override string? HolopadProto => "HolopadCommandMeetingRoom";
    public override int Priority => 150;
}

public class CentralCommand : CommandPlusRoomType
{
    public override string Name => "CentralCommand";
    public override string DisplayName => "Центральное командование";
    public override string DoorProto => "AirlockCentralCommandLocked";
    public override string GlassDoorProto => "AirlockCentralCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 80, 30);
    public override Color LineColor => Color.FromArgb(255, 60, 160, 60);
    public override string? HolopadProto => "HolopadCentCommEvacShuttle";
    public override string? BedProto => "Bed";
    public override string? BedsheetProto => "BedsheetCentcom";
    public override int Priority => 400;
}

public class BridgeHallway : CommandPlusRoomType
{
    public override string Name => "BridgeHallway";
    public override string DisplayName => "Коридор мостика";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override string? HolopadProto => "HolopadCommandBridgeHallway";
    public override int Priority => 150;
}
