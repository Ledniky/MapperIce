using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ КОМАНДОВАНИЯ
// ============================================================

public abstract class CommandRoomType : RoomType
{
    public override string Category => "Command";
    public override string FloorProto => "FloorSteel";
}

// ============================================================
// КОМНАТЫ КОМАНДОВАНИЯ
// ============================================================

public class Command : CommandRoomType
{
    public override string Name => "Command";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 51, 77, 109);
    public override Color LineColor => Color.FromArgb(255, 51, 77, 109);
}

public class EVA : CommandRoomType
{
    public override string Name => "EVA";
    public override string DoorProto => "AirlockEVALocked";
    public override string GlassDoorProto => "AirlockEVAGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 80, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 80, 150, 200);
}

public class Vault : CommandRoomType
{
    public override string Name => "Vault";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "HighSecCommandLocked";
    public override string GlassDoorProto => "HighSecCommandLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 180, 50);
    public override Color LineColor => Color.FromArgb(255, 180, 180, 50);
}

// ============================================================
// КАБИНЕТЫ КОМАНДОВАНИЯ
// ============================================================

public class Captain : CommandRoomType
{
    public override string Name => "Captain";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockCaptainLocked";
    public override string GlassDoorProto => "AirlockCaptainGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 80);
    public override Color LineColor => Color.FromArgb(255, 30, 50, 80);
}

public class HeadOfPersonnel : CommandRoomType
{
    public override string Name => "HeadOfPersonnel";
    public override string DoorProto => "AirlockHeadOfPersonnelLocked";
    public override string GlassDoorProto => "AirlockHeadOfPersonnelGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 70, 90, 130);
    public override Color LineColor => Color.FromArgb(255, 70, 90, 130);
}

public class CentralCommand : CommandRoomType
{
    public override string Name => "CentralCommand";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockCentralCommandLocked";
    public override string GlassDoorProto => "AirlockCentralCommandGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 40, 60, 100);
    public override Color LineColor => Color.FromArgb(255, 40, 60, 100);
}