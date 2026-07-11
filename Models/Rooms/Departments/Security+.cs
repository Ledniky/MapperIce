using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ КОМНАТЫ БЕЗОПАСНОСТИ (Security+)
// ============================================================

public abstract class SecurityPlusRoomType : RoomType
{
    public override string Category => "Security+";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
}

public class Interrogation : SecurityPlusRoomType
{
    public override string Name => "Interrogation";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 80, 80);
    public override Color LineColor => Color.FromArgb(255, 180, 80, 80);
    public override int Priority => 20;
}

public class Permabrig : SecurityPlusRoomType
{
    public override string Name => "Permabrig";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 150, 50, 50);
    public override int Priority => 20;
}

public class Checkpoint : SecurityPlusRoomType
{
    public override string Name => "Checkpoint";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 100, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 100, 100);
    public override int Priority => 20;
}

public class SecurityPost : SecurityPlusRoomType
{
    public override string Name => "SecurityPost";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 190, 90, 90);
    public override Color LineColor => Color.FromArgb(255, 190, 90, 90);
    public override int Priority => 20;
}

public class SecurityOffice : SecurityPlusRoomType
{
    public override string Name => "SecurityOffice";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 80, 80);
    public override Color LineColor => Color.FromArgb(255, 200, 80, 80);
    public override int Priority => 20;
}