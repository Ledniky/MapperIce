using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ ОТДЕЛА СЕРВИСА
// ============================================================

public abstract class ServiceRoomType : RoomType
{
    public override string Category => "Service";
    public override string FloorProto => "FloorSteel";
}

// ============================================================
// КОМНАТЫ СЕРВИСА
// ============================================================

public class Service : ServiceRoomType
{
    public override string Name => "Service";
    public override string Description => "Комната сервиса. Базовое помещение для обслуживания и бытовых нужд экипажа.";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 159, 237, 88);
    public override Color LineColor => Color.FromArgb(255, 159, 237, 88);
    public override int Priority => 10;
}

public class Janitor : ServiceRoomType
{
    public override string Name => "Janitor";
    public override string Description => "Комната уборщика. Помещение для хранения уборочного оборудования и химии.";
    public override string DoorProto => "AirlockJanitorLocked";
    public override string GlassDoorProto => "AirlockJanitorGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 140, 52, 127);
    public override Color LineColor => Color.FromArgb(255, 140, 52, 127);
    public override int Priority => 10;
}

public class Kitchen : ServiceRoomType
{
    public override string Name => "Kitchen";
    public override string Description => "Кухня. Помещение для приготовления пищи и хранения продуктов.";
    public override string DoorProto => "AirlockKitchenLocked";
    public override string GlassDoorProto => "AirlockKitchenGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 100);
    public override int Priority => 10;
}

public class Bar : ServiceRoomType
{
    public override string Name => "Bar";
    public override string Description => "Бар. Место отдыха экипажа с тёмным интерьером для подачи напитков.";
    public override string DoorProto => "AirlockBarLocked";
    public override string GlassDoorProto => "AirlockBarGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 121, 21, 0);
    public override Color LineColor => Color.FromArgb(255, 121, 21, 0);
    public override int Priority => 10;
}

public class Hydroponics : ServiceRoomType
{
    public override string Name => "Hydroponics";
    public override string Description => "Гидропоника. Оранжерея для выращивания растений и овощей без почвы.";
    public override string DoorProto => "AirlockHydroponicsLocked";
    public override string GlassDoorProto => "AirlockHydroGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 180, 60);
    public override Color LineColor => Color.FromArgb(255, 60, 180, 60);
    public override int Priority => 10;
}

public class Chapel : ServiceRoomType
{
    public override string Name => "Chapel";
    public override string Description => "Часовня. Место для молитв и духовных размышлений с тёплым интерьером.";
    public override string DoorProto => "AirlockChapelLocked";
    public override string GlassDoorProto => "AirlockChapelGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
    public override int Priority => 10;
}

public class Theatre : ServiceRoomType
{
    public override string Name => "Theatre";
    public override string Description => "Театр. Зрительный зал для развлечений и культурных мероприятий экипажа.";
    public override string DoorProto => "AirlockTheatreLocked";
    public override string GlassDoorProto => "AirlockTheatreGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 100, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 100, 150);
    public override int Priority => 10;
}

// ============================================================
// КАБИНЕТЫ СЕРВИСА
// ============================================================

public class Lawyer : ServiceRoomType
{
    public override string Name => "Lawyer";
    public override string Description => "Кабинет адвоката. Офис для юридических консультаций и представительства.";
    public override string DoorProto => "AirlockLawyerLocked";
    public override string GlassDoorProto => "AirlockLawyerGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 180, 180, 200);
    public override int Priority => 10;
}
