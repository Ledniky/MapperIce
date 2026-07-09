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
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 130);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 130);
}

public class Gym : ServicePlusRoomType
{
    public override string Name => "Gym";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 200, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 200, 150);
}

public class Garden : ServicePlusRoomType
{
    public override string Name => "Garden";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 200, 100);
    public override Color LineColor => Color.FromArgb(255, 100, 200, 100);
}

public class Dorms : ServicePlusRoomType
{
    public override string Name => "Dorms";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 200);
}

public class Toilets : ServicePlusRoomType
{
    public override string Name => "Toilets";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 220, 230);
    public override Color LineColor => Color.FromArgb(255, 180, 220, 230);
}

public class LockerRoom : ServicePlusRoomType
{
    public override string Name => "LockerRoom";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 190, 180);
    public override Color LineColor => Color.FromArgb(255, 200, 190, 180);
}

public class Arcade : ServicePlusRoomType
{
    public override string Name => "Arcade";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 150, 200);
}

public class Park : ServicePlusRoomType
{
    public override string Name => "Park";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 220, 100);
    public override Color LineColor => Color.FromArgb(255, 100, 220, 100);
}

public class Courtroom : ServicePlusRoomType
{
    public override string Name => "Courtroom";
    public override string DoorProto => "AirlockServiceLocked";
    public override string GlassDoorProto => "AirlockServiceGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 170, 150);
    public override Color LineColor => Color.FromArgb(255, 180, 170, 150);
}