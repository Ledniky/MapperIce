using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// ДОПОЛНИТЕЛЬНЫЕ ОБЩИЕ КОМНАТЫ (Common+)
// ============================================================

public abstract class CommonPlusRoomType : RoomType
{
    public override string Category => "Common+";
}

public class Arrivals : CommonPlusRoomType
{
    public override string Name => "Arrivals";
    public override Color FillColor => Color.FromArgb(100, 200, 220, 240);
    public override Color LineColor => Color.FromArgb(255, 200, 220, 240);
}

public class Departures : CommonPlusRoomType
{
    public override string Name => "Departures";
    public override Color FillColor => Color.FromArgb(100, 200, 210, 230);
    public override Color LineColor => Color.FromArgb(255, 200, 210, 230);
}

public class ToolStorage : CommonPlusRoomType
{
    public override string Name => "ToolStorage";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 190, 170);
    public override Color LineColor => Color.FromArgb(255, 200, 190, 170);
}

public class Cryo : CommonPlusRoomType
{
    public override string Name => "Cryo";
    public override Color FillColor => Color.FromArgb(100, 180, 220, 255);
    public override Color LineColor => Color.FromArgb(255, 180, 220, 255);
}

public class AI : CommonPlusRoomType
{
    public override string Name => "AI";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockAI";
    public override string GlassDoorProto => "AirlockAIGlass";
    public override Color FillColor => Color.FromArgb(100, 100, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 100, 150, 200);
}

public class Satellite : CommonPlusRoomType
{
    public override string Name => "Satellite";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 150, 150, 180);
    public override Color LineColor => Color.FromArgb(255, 150, 150, 180);
}

public class Restaurant : CommonPlusRoomType
{
    public override string Name => "Restaurant";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
}

public class KitchenBackroom : CommonPlusRoomType
{
    public override string Name => "KitchenBackroom";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 120);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 120);
}

public class BarBackroom : CommonPlusRoomType
{
    public override string Name => "BarBackroom";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 120, 100);
    public override Color LineColor => Color.FromArgb(255, 150, 120, 100);
}

public class ChapelMorgue : CommonPlusRoomType
{
    public override string Name => "ChapelMorgue";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 140);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 140);
}

public class JanitorCloset : CommonPlusRoomType
{
    public override string Name => "JanitorCloset";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 150, 180, 200);
}

public class Maintenance : CommonPlusRoomType
{
    public override string Name => "Maintenance";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 160, 140);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 140);
}