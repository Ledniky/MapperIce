using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ КЛАСС (все типы наследуются от него)
// ============================================================

public abstract class RoomType
{
    public abstract string Name { get; }
    public virtual string Category => "Common";
    public virtual string WallProto => "WallSolid";
    public virtual string FloorProto => "Plating";
    public virtual string DoorProto => "Airlock";
    public virtual string GlassDoorProto => "AirlockGlass";
    public virtual Color FillColor => Color.FromArgb(100, 230, 230, 230);
    public virtual Color LineColor => Color.FromArgb(255, 180, 180, 180);
    public virtual bool IsCustom => false;
    public virtual bool IsHidden => false;
    public virtual int Priority => 0;  // ← ДОБАВЛЕНО
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ОБЩИХ ТИПОВ (Common)
// ============================================================

public abstract class CommonRoomType : RoomType
{
    public override string Category => "Common";
}

// ============================================================
// БАЗОВЫЙ ДЛЯ АНТАГОНИСТОВ (Antags)
// ============================================================

public abstract class AntagRoomType : RoomType
{
    public override string Category => "Antags";
    public override string FloorProto => "FloorSteel";
}

// ============================================================
// ОБЩИЕ ТИПЫ (Common) - коридоры, технические и базовые
// ============================================================

public class General : CommonRoomType
{
    public override string Name => "General";
    public override Color FillColor => Color.FromArgb(100, 220, 220, 220);
    public override int Priority => 0;
}

public class Vox : CommonRoomType
{
    public override string Name => "Vox";
    public override Color FillColor => Color.FromArgb(100, 254, 1, 64);
    public override int Priority => 15;
}

public class Technical : CommonRoomType
{
    public override string Name => "Technical";
    public override string DoorProto => "AirlockMaintLocked";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 255, 240, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
    public override int Priority => 0;
}

public class Hallway : CommonRoomType
{
    public override string Name => "Hallway";
    public override Color FillColor => Color.FromArgb(100, 230, 230, 240);
    public override Color LineColor => Color.FromArgb(255, 200, 200, 210);
    public override int Priority => 0;
}

public class BaseRoom : CommonRoomType
{
    public override string Name => "BaseRoom";
    public override bool IsHidden => true;
    public override int Priority => 0;
}

// ============================================================
// АНТАГОНИСТЫ (Antags)
// ============================================================

public class Syndicate : AntagRoomType
{
    public override string Name => "Syndicate";
    public override string DoorProto => "AirlockSyndicateLocked";
    public override string GlassDoorProto => "AirlockSyndicateGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 200, 50, 50);
    public override int Priority => 250;
}

public class Nukeop : AntagRoomType
{
    public override string Name => "Nukeop";
    public override string DoorProto => "AirlockSyndicateNukeopLocked";
    public override string GlassDoorProto => "AirlockSyndicateNukeopGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 200, 30, 30);
    public override int Priority => 300;
}

// ============================================================
// КАСТОМНЫЙ ТИП
// ============================================================

public class CustomRoomType : RoomType
{
    public CustomRoomTypeData Data { get; }
    public int Priority { get; }

    public CustomRoomType(CustomRoomTypeData data)
    {
        Data = data;
        Priority = data.Priority;
    }

    public override string Name => Data.Name;
    public override string Category => Data.Category;
    public override string WallProto => Data.WallProto;
    public override string FloorProto => Data.FloorProto;
    public override string DoorProto => Data.DoorProto;
    public override string GlassDoorProto => Data.GlassDoorProto;
    public override Color FillColor => ParseColor(Data.FillColor);
    public override Color LineColor => ParseColor(Data.LineColor);
    public override bool IsCustom => true;

    private static Color ParseColor(string value)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 4)
                return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }
        catch { }
        return Color.FromArgb(200, 230, 230, 230);
    }
}

// ============================================================
// МОДЕЛИ ДЛЯ ХРАНЕНИЯ
// ============================================================

public class CustomRoomTypeData
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public int Priority { get; set; } = 0;
}

public class ExportData
{
    public string Type { get; set; } = "Single";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public int Priority { get; set; } = 0;
}