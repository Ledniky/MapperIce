using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ НАУКИ (Science+)
// ============================================================

public abstract class SciencePlusRoomType : ScienceRoomType
{
    public override string Category => "Science+";
    public override string FloorProto => "FloorSteel";
}

public class Anomalistics : SciencePlusRoomType
{
    public override string Name => "Anomalistics";
    public override string DisplayName => "Аномалистика";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceAnomaly";
    public override int Priority => 10;
}

public class Robotics : SciencePlusRoomType
{
    public override string Name => "Robotics";
    public override string DisplayName => "Робототехника";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceRobotics";
    public override string? LockerProto => "WardrobeRoboticsFilled";
    public override int Priority => 20;
}

public class Xenobiology : SciencePlusRoomType
{
    public override string Name => "Xenobiology";
    public override string DisplayName => "Ксенобиология";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceArtifact";
    public override int Priority => 10;
}

public class AtmosStorage : AtmosRoomType
{
    public override string Name => "AtmosStorage";
    public override string DisplayName => "Атмо-склад";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceFront";
    public override int Priority => 10;
}

public class ArtifactNorth : SciencePlusRoomType
{
    public override string Name => "ArtifactNorth";
    public override string DisplayName => "Артефакт — север";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceArtifactNorth";
    public override int Priority => 10;
}

public class ArtifactSouth : SciencePlusRoomType
{
    public override string Name => "ArtifactSouth";
    public override string DisplayName => "Артефакт — юг";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceArtifactSouth";
    public override int Priority => 10;
}

public class ScienceBreakroom : SciencePlusRoomType
{
    public override string Name => "ScienceBreakroom";
    public override string DisplayName => "Комната отдыха науки";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override string? HolopadProto => "HolopadScienceBreakroom";
    public override int Priority => 10;
}
