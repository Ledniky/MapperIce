using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ НАУКИ (Science+)
// ============================================================
public abstract class SciencePlusRoomType : RoomType
{
    public override string Category => "Science+";
    public override string FloorProto => "FloorSteel";
}
public class Anomalistics : SciencePlusRoomType
{
    public override string Name => "Anomalistics";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 100, 220);
    public override Color LineColor => Color.FromArgb(255, 200, 100, 220);
}

public class Robotics : SciencePlusRoomType
{
    public override string Name => "Robotics";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 150, 150, 200);
}

public class Xenobiology : SciencePlusRoomType
{
    public override string Name => "Xenobiology";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 120, 200, 120);
    public override Color LineColor => Color.FromArgb(255, 120, 200, 120);
}

public class Toxins : SciencePlusRoomType
{
    public override string Name => "Toxins";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 150, 80);
    public override Color LineColor => Color.FromArgb(255, 200, 150, 80);
}

public class RnD : SciencePlusRoomType
{
    public override string Name => "RnD";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 130, 200);
    public override Color LineColor => Color.FromArgb(255, 180, 130, 200);
}

public class TestingLab : SciencePlusRoomType
{
    public override string Name => "TestingLab";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 180);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 180);
}