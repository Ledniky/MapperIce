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
    public override int Priority => 10;
}

public class Robotics : SciencePlusRoomType
{
    public override string Name => "Robotics";
    public override string DisplayName => "Робототехника";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override int Priority => 20;
}

public class Xenobiology : SciencePlusRoomType
{
    public override string Name => "Xenobiology";
    public override string DisplayName => "Ксенобиология";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override int Priority => 10;
}

public class AtmosStorage : SciencePlusRoomType
{
    public override string Name => "AtmosStorage";
    public override string DisplayName => "Атмо-склад";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override int Priority => 10;
}
