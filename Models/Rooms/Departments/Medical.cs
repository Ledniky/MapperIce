using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ МЕДИЦИНСКОГО ОТДЕЛА
// ============================================================

public abstract class MedicalRoomType : RoomType
{
    public override string Category => "Medical";
    public override string FloorProto => "FloorWhite";
}

public abstract class MedicalOfficeType : MedicalRoomType
{
    public override bool IsOffice => true;
}

// ============================================================
// КОМНАТЫ МЕДИЦИНЫ
// ============================================================

public class Medical : MedicalRoomType
{
    public override string Name => "Medical";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 82, 180, 233);
    public override Color LineColor => Color.FromArgb(255, 82, 180, 233);
}

public class Virology : MedicalRoomType
{
    public override string Name => "Virology";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockVirologyLocked";
    public override string GlassDoorProto => "AirlockVirologyGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 67, 153, 9);
    public override Color LineColor => Color.FromArgb(255, 67, 153, 9);
}

public class Chemistry : MedicalRoomType
{
    public override string Name => "Chemistry";
    public override string DoorProto => "AirlockChemistryLocked";
    public override string GlassDoorProto => "AirlockChemistryGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 250, 117, 0);
    public override Color LineColor => Color.FromArgb(255, 250, 117, 0);
}

public class Morgue : MedicalRoomType
{
    public override string Name => "Morgue";
    public override string DoorProto => "AirlockMedicalMorgueLocked";
    public override string GlassDoorProto => "AirlockMedicalMorgueGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 120, 160);
    public override Color LineColor => Color.FromArgb(255, 60, 120, 160);
}

// ============================================================
// КАБИНЕТЫ МЕДИЦИНЫ
// ============================================================

public class ChiefMedicalOfficer : MedicalOfficeType
{
    public override string Name => "ChiefMedicalOfficer";
    public override string DoorProto => "AirlockChiefMedicalOfficerLocked";
    public override string GlassDoorProto => "AirlockChiefMedicalOfficerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 70, 160, 210);
    public override Color LineColor => Color.FromArgb(255, 70, 160, 210);
}