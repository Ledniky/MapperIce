using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ МЕДИЦИНЫ (Medical+)
// ============================================================

public abstract class MedicalPlusRoomType : RoomType
{
    public override string Category => "Medical+";
    public override string FloorProto => "FloorWhite";
}

public class MedicalBreakRoom : MedicalPlusRoomType
{
    public override string Name => "MedicalBreakRoom";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 120, 200, 240);
    public override Color LineColor => Color.FromArgb(255, 120, 200, 240);
    public override int Priority => 20;
}

public class Cryogenetics : MedicalPlusRoomType
{
    public override string Name => "Cryogenetics";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 220, 255);
    public override Color LineColor => Color.FromArgb(255, 150, 220, 255);
    public override int Priority => 20;
}

public class Paramedic : MedicalPlusRoomType
{
    public override string Name => "Paramedic";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 200, 220);
    public override Color LineColor => Color.FromArgb(255, 100, 200, 220);
    public override int Priority => 25;
}

public class Psychologist : MedicalPlusRoomType
{
    public override string Name => "Psychologist";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 220);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 220);
    public override int Priority => 20;
}

public class MedicalStorage : MedicalPlusRoomType
{
    public override string Name => "MedicalStorage";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 190, 210);
    public override Color LineColor => Color.FromArgb(255, 150, 190, 210);
    public override int Priority => 20;
}