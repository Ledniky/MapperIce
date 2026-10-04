using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ МЕДИЦИНЫ (Medical+)
// ============================================================

public abstract class MedicalPlusRoomType : RoomType
{
    public override string Category => "Medical+";
    public override string FloorProto => "FloorWhite";
    public override Color FillColor => Color.FromArgb(100, 120, 200, 240);
    public override Color LineColor => Color.FromArgb(255, 120, 200, 240);
}

public class MedicalBreakRoom : MedicalPlusRoomType
{
    public override string Name => "MedicalBreakRoom";
    public override string DisplayName => "Комната отдыха медиков";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalBreakroom";
    public override int Priority => 20;
}

public class Cryogenetics : MedicalPlusRoomType
{
    public override string Name => "Cryogenetics";
    public override string DisplayName => "Крионика";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalCryopods";
    public override int Priority => 20;
}

public class Paramedic : MedicalPlusRoomType
{
    public override string Name => "Paramedic";
    public override string DisplayName => "Парамедик";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalParamed";
    public override string? LockerProto => "LockerParamedicFilled";
    public override int Priority => 25;
}

public class Psychologist : MedicalPlusRoomType
{
    public override string Name => "Psychologist";
    public override string DisplayName => "Психолог";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalFront";
    public override int Priority => 20;
}

public class MedicalStorage : MedicalPlusRoomType
{
    public override string Name => "MedicalStorage";
    public override string DisplayName => "Медсклад";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalMedbay";
    public override int Priority => 20;
}

public class MedicalSurgery : MedicalPlusRoomType
{
    public override string Name => "MedicalSurgery";
    public override string DisplayName => "Хирургия";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalSurgery";
    public override int Priority => 20;
}

public class MedicalClinic : MedicalPlusRoomType
{
    public override string Name => "MedicalClinic";
    public override string DisplayName => "Отдельная клиника";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override string? HolopadProto => "HolopadMedicalClinic";
    public override int Priority => 20;
}
