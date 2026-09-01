using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ ИНЖЕНЕРНОГО ОТДЕЛА
// ============================================================

public abstract class EngineeringRoomType : RoomType
{
    public override string Category => "Engineering";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
}

// ============================================================
// КОМНАТЫ ИНЖЕНЕРИИ
// ============================================================

public class Engineering : EngineeringRoomType
{
    public override string Name => "Engineering";
    public override string Description => "Инженерная комната. Основной отсек для инженерных работ и обслуживания систем станции.";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override string GlassDoorProto => "AirlockEngineeringGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 239, 179, 65);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
    public override int Priority => 10;
}

public class Atmospherics : EngineeringRoomType
{
    public override string Name => "Atmospherics";
    public override string Description => "Атмосферная станция. Помещение для обслуживания систем воздуха и давления на станции.";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override string GlassDoorProto => "AirlockAtmosphericsGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 62, 179, 136);
    public override Color LineColor => Color.FromArgb(255, 62, 179, 136);
    public override int Priority => 30;
}

public class External : EngineeringRoomType
{
    public override string Name => "External";
    public override string Description => "Внешняя инженерная комната. Открытый отсек для внешних работ и обслуживания корпуса станции.";
    public override string DoorProto => "AirlockExternalLocked";
    public override string GlassDoorProto => "AirlockExternalGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 180, 220);
    public override Color LineColor => Color.FromArgb(255, 100, 180, 220);
    public override int Priority => 50;
}

// ============================================================
// КАБИНЕТЫ ИНЖЕНЕРИИ
// ============================================================

public class ChiefEngineer : EngineeringRoomType
{
    public override string Name => "ChiefEngineer";
    public override string Description => "Кабинет главного инженера. Офис руководителя инженерного отдела станции.";
    public override string DoorProto => "AirlockChiefEngineerLocked";
    public override string GlassDoorProto => "AirlockChiefEngineerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
    public override int Priority => 250;
}
