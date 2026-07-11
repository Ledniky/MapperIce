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
    public override int Priority => 10;
}

public class Departures : CommonPlusRoomType
{
    public override string Name => "Departures";
    public override Color FillColor => Color.FromArgb(100, 200, 210, 230);
    public override Color LineColor => Color.FromArgb(255, 200, 210, 230);
    public override int Priority => 10;
}

public class ToolStorage : CommonPlusRoomType
{
    public override string Name => "ToolStorage";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 190, 170);
    public override Color LineColor => Color.FromArgb(255, 200, 190, 170);
    public override int Priority => 15;
}

public class Cryo : CommonPlusRoomType
{
    public override string Name => "Cryo";
    public override Color FillColor => Color.FromArgb(100, 180, 220, 255);
    public override Color LineColor => Color.FromArgb(255, 180, 220, 255);
    public override int Priority => 10;
}


public class Restaurant : CommonPlusRoomType
{
    public override string Name => "Restaurant";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
    public override int Priority => 10;
}