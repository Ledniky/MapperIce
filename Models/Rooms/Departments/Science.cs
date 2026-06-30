using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ НАУЧНОГО ОТДЕЛА
// ============================================================

public abstract class ScienceRoomType : RoomType
{
    public override string Category => "Science";
    public override string FloorProto => "FloorSteel";
}

public abstract class ScienceOfficeType : ScienceRoomType
{
    public override bool IsOffice => true;
}

// ============================================================
// КОМНАТЫ НАУКИ
// ============================================================

public class Science : ScienceRoomType
{
    public override string Name => "Science";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 211, 129, 201);
    public override Color LineColor => Color.FromArgb(255, 211, 129, 201);
}

// ============================================================
// КАБИНЕТЫ НАУКИ
// ============================================================

public class ResearchDirector : ScienceOfficeType
{
    public override string Name => "ResearchDirector";
    public override string DoorProto => "AirlockResearchDirectorLocked";
    public override string GlassDoorProto => "AirlockResearchDirectorGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 190, 100, 180);
    public override Color LineColor => Color.FromArgb(255, 190, 100, 180);
}