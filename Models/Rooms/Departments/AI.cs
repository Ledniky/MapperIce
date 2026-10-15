using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ОТДЕЛ ИИ (AI)
// ============================================================

public abstract class AIRoomType : RoomType
{
    public override string Category => "AI";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";

    public override Color FillColor => Color.FromArgb(100, 10, 25, 60);
    public override Color LineColor => Color.FromArgb(255, 20, 45, 100);
}

public class AI : AIRoomType
{
    public override string Name => "AI";
    public override string DisplayName => "ИИ";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override string? HolopadProto => "HolopadAiCore";
    public override int Priority => 170;
}

public class SatelliteAI : AIRoomType
{
    public override string Name => "Satellite";
    public override string DisplayName => "Спутник ИИ";
    public override string HolopadProto => "HolopadAiBackupPower";
    public override int Priority => 160;
}

public class AIMain : AIRoomType
{
    public override string Name => "AIMain";
    public override string DisplayName => "Главный отсек ИИ";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override string? HolopadProto => "HolopadAiMain";
    public override int Priority => 170;
}

public class AIUpload : AIRoomType
{
    public override string Name => "AIUpload";
    public override string? FaxName => "AI Upload";
    public override string DisplayName => "Загрузка ИИ";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override string? HolopadProto => "HolopadAiUpload";
    public override int Priority => 170;
}

public class AIEntrance : AIRoomType
{
    public override string Name => "AIEntrance";
    public override string DisplayName => "Вход к ИИ";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override string? HolopadProto => "HolopadAiEntrance";
    public override int Priority => 170;
}

public class AIChute : AIRoomType
{
    public override string Name => "AIChute";
    public override string DisplayName => "Труба в ИИ";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override string? HolopadProto => "HolopadAiChute";
    public override int Priority => 170;
}