using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ НАУЧНОГО ОТДЕЛА
// ============================================================

public abstract class ScienceRoomType : RoomType
{
    public override string Category => "Science";
    public override string FloorProto => "FloorSteel";
    public override Color FillColor => Color.FromArgb(100, 211, 129, 201);
    public override Color LineColor => Color.FromArgb(255, 211, 129, 201);
}

// ============================================================
// КОМНАТЫ НАУКИ
// ============================================================

public class Science : ScienceRoomType
{
    public override string Name => "Science";
    public override string DisplayName => "Научный отсек";
    public override string Description => "Научная комната. Основной лабораторный отсек для научных исследований и экспериментов.";
    public override string DoorProto => "AirlockScienceLocked";
    public override string GlassDoorProto => "AirlockScienceGlassLocked";
    public override int Priority => 15;
}

public class ResearchDirector : ScienceRoomType
{
    public override string Name => "ResearchDirector";
    public override string DisplayName => "Научный руководитель";
    public override string Description => "Кабинет Нр-а. Офис руководителя научного отдела станции.";
    public override string DoorProto => "AirlockResearchDirectorLocked";
    public override string GlassDoorProto => "AirlockResearchDirectorGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 211, 129, 201);
    public override int Priority => 250;
}
