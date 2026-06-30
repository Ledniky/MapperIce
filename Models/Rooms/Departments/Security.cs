using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ ОТДЕЛА БЕЗОПАСНОСТИ
// ============================================================

public abstract class SecurityRoomType : RoomType
{
    public override string Category => "Security";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
}

// ============================================================
// КОМНАТЫ БЕЗОПАСНОСТИ
// ============================================================

public class Security : SecurityRoomType
{
    public override string Name => "Security";
    public override string DoorProto => "AirlockSecurityLocked";
    public override string GlassDoorProto => "AirlockSecurityGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 222, 58, 58);
    public override Color LineColor => Color.FromArgb(255, 222, 58, 58);
}

public class Brig : SecurityRoomType
{
    public override string Name => "Brig";
    public override string DoorProto => "AirlockBrigLocked";
    public override string GlassDoorProto => "AirlockBrigGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 180, 50, 50);
}

public class Armory : SecurityRoomType
{
    public override string Name => "Armory";
    public override string DoorProto => "AirlockArmoryLocked";
    public override string GlassDoorProto => "AirlockArmoryGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 150, 30, 30);
}

public class ArmoryVault : SecurityRoomType
{
    public override string Name => "Armory";
    public override string DoorProto => "HighSecArmoryLocked";
    public override string GlassDoorProto => "HighSecArmoryLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 150, 30, 30);
}

// ============================================================
// КАБИНЕТЫ БЕЗОПАСНОСТИ
// ============================================================

public class Detective : SecurityRoomType
{
    public override string Name => "Detective";
    public override string DoorProto => "AirlockDetectiveLocked";
    public override string GlassDoorProto => "AirlockDetectiveGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 150, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 150, 100);
}

public class Warden : Armory
{
    public override string Name => "Warden";
    public override string DoorProto => "AirlockWardenLocked";
    public override string GlassDoorProto => "AirlockWardenGlassLocked";
    public override string FloorProto => "FloorWhite";
    public override Color FillColor => Color.FromArgb(100, 180, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 180, 50, 50);
}

public class HeadOfSecurity : SecurityRoomType
{
    public override string Name => "HeadOfSecurity";
    public override string DoorProto => "AirlockHeadOfSecurityLocked";
    public override string GlassDoorProto => "AirlockHeadOfSecurityGlassLocked";
    public override string FloorProto => "FloorDark";
    public override Color FillColor => Color.FromArgb(100, 200, 40, 40);
    public override Color LineColor => Color.FromArgb(255, 200, 40, 40);
}