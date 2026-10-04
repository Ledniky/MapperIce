using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ СЕРВИСА (Service+)
// ============================================================

public abstract class ServicePlusRoomType : RoomType
{
    public override string Category => "Service+";
    public override string FloorProto => "FloorSteel";
}

public class Library : ServicePlusRoomType
{
    public override string Name => "Library";
    public override string DisplayName => "Библиотека";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 130);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 130);
    public override string? HolopadProto => "HolopadServiceLibrary";
    public override string? LockerProto => "WardrobeWhiteFilled";
    public override int Priority => 10;
}

public class Gym : ServicePlusRoomType
{
    public override string Name => "Gym";
    public override string DisplayName => "Тренажёрный зал";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 200, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 200, 150);
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override int Priority => 10;
}

public class Dorms : ServicePlusRoomType
{
    public override string Name => "Dorms";
    public override string DisplayName => "Общежитие (Дормы)";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 200);
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override string? LockerProto => "WardrobeMixedFilled";
    public override int Priority => 10;
}

public class Toilets : ServicePlusRoomType
{
    public override string Name => "Toilets";
    public override string DisplayName => "Туалет";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 220, 230);
    public override Color LineColor => Color.FromArgb(255, 180, 220, 230);
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override int Priority => 10;
}

public class Arcade : ServicePlusRoomType
{
    public override string Name => "Arcade";
    public override string DisplayName => "Зал аркадных автоматов";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 150, 200);
    public override string? HolopadProto => "HolopadGeneralArcade";
    public override int Priority => 10;
}

public class Courtroom : ServicePlusRoomType
{
    public override string Name => "Courtroom";
    public override string DisplayName => "Суд";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 170, 150);
    public override Color LineColor => Color.FromArgb(255, 180, 170, 150);
    public override string? HolopadProto => "HolopadSecurityCourtroom";
    public override int Priority => 15;
}

// ============================================================
// ДОЧЕРНИЕ КОМНАТЫ (находятся внутри Service)
// ============================================================

public class BarBackroom : ServicePlusRoomType
{
    public override string Name => "BarBackroom";
    public override string DisplayName => "Задняя комната бара";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 120, 100);
    public override Color LineColor => Color.FromArgb(255, 150, 120, 100);
    public override string? HolopadProto => "HolopadServiceBar";
    public override string? LockerProto => "LockerBoozeFilled";
    public override int Priority => 15;
}

public class ChapelMorgue : ServicePlusRoomType
{
    public override string Name => "ChapelMorgue";
    public override string DisplayName => "Морг церкви";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 140);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 140);
    public override string? HolopadProto => "HolopadServiceChapel";
    public override int Priority => 15;
}

public class KitchenBackroom : ServicePlusRoomType
{
    public override string Name => "KitchenBackroom";
    public override string DisplayName => "Задняя комната кухни";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 120);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 120);
    public override string? HolopadProto => "HolopadServiceKitchen";
    public override string? LockerProto => "ClosetChefFilled";
    public override int Priority => 15;
}

public class GameRoom : ServicePlusRoomType
{
    public override string Name => "GameRoom";
    public override string DisplayName => "Игровая комната";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceGameRoom";
    public override int Priority => 10;
}

public class Newsroom : ServicePlusRoomType
{
    public override string Name => "Newsroom";
    public override string DisplayName => "Новостная студия";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceNewsroom";
    public override int Priority => 10;
}

public class Zookeeper : ServicePlusRoomType
{
    public override string Name => "Zookeeper";
    public override string DisplayName => "Зоопарк";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceZookeeper";
    public override int Priority => 10;
}

public class Boxer : ServicePlusRoomType
{
    public override string Name => "Boxer";
    public override string DisplayName => "Боксёр";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceBoxer";
    public override int Priority => 10;
}

public class Clown : ServicePlusRoomType
{
    public override string Name => "Clown";
    public override string DisplayName => "Клоун";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceClown";
    public override int Priority => 10;
}

public class Musician : ServicePlusRoomType
{
    public override string Name => "Musician";
    public override string DisplayName => "Музыкант";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceMusician";
    public override int Priority => 10;
}

public class Mime : ServicePlusRoomType
{
    public override string Name => "Mime";
    public override string DisplayName => "Мим";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override string? HolopadProto => "HolopadServiceMime";
    public override int Priority => 10;
}
