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

// ============================================================
// КОМНАТЫ МЕДИЦИНЫ
// ============================================================

public class Medical : MedicalRoomType
{
    public override string Name => "Medical";
    public override string Description => "Медицинская комната. Основной медицинский отсек для лечения и диагностики.";
    public override string DoorProto => "AirlockMedicalLocked";
    public override string GlassDoorProto => "AirlockMedicalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 82, 180, 233);
    public override Color LineColor => Color.FromArgb(255, 82, 180, 233);
    public override int Priority => 15;
}

public class Virology : MedicalRoomType
{
    public override string Name => "Virology";
    public override string Description => "Вирусная лаборатория. Герметичное помещение с усиленными стенами для работы с опасными патогенами.";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockVirologyLocked";
    public override string GlassDoorProto => "AirlockVirologyGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 67, 153, 9);
    public override Color LineColor => Color.FromArgb(255, 67, 153, 9);
    public override int Priority => 60;
}

public class Chemistry : MedicalRoomType
{
    public override string Name => "Chemistry";
    public override string Description => "Химическая лаборатория. Помещение для приготовления лекарств и химических реакций.";
    public override string DoorProto => "AirlockChemistryLocked";
    public override string GlassDoorProto => "AirlockChemistryGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 250, 117, 0);
    public override Color LineColor => Color.FromArgb(255, 250, 117, 0);
    public override int Priority => 70;
}

public class Morgue : MedicalRoomType
{
    public override string Name => "Morgue";
    public override string Description => "Морг. Холодильное помещение для хранения тел и проведения вскрытий.";
    public override string DoorProto => "AirlockMedicalMorgueLocked";
    public override string GlassDoorProto => "AirlockMedicalMorgueGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 120, 160);
    public override Color LineColor => Color.FromArgb(255, 60, 120, 160);
    public override int Priority => 30;
}

// ============================================================
// КАБИНЕТЫ МЕДИЦИНЫ
// ============================================================

public class ChiefMedicalOfficer : MedicalRoomType
{
    public override string Name => "ChiefMedicalOfficer";
    public override string Description => "Кабинет главного врача. Офис руководителя медицинского отдела станции.";
    public override string DoorProto => "AirlockChiefMedicalOfficerLocked";
    public override string GlassDoorProto => "AirlockChiefMedicalOfficerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 82, 180, 233);
    public override int Priority => 250;
}
