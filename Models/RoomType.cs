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
    public virtual string DoorProto => "";
    public virtual Color FillColor => Color.FromArgb(100, 230, 230, 230);
    public virtual Color LineColor => Color.FromArgb(255, 180, 180, 180);
    public virtual bool IsCustom => false;
    public virtual bool IsHidden => false;
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ОБЩИХ ТИПОВ (Common)
// ============================================================

public abstract class CommonRoomType : RoomType
{
    public override string Category => "Common";
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ДЕПАРТАМЕНТОВ (Departmental)
// ============================================================

public abstract class DepartmentalRoomType : RoomType
{
    public override string Category => "Departments";
}

// ============================================================
// ОБЩИЕ ТИПЫ (Common)
// ============================================================

public class General : CommonRoomType
{
    public override string Name => "General";
    public override Color FillColor => Color.FromArgb(100, 220, 220, 220);
}

public class Technical : CommonRoomType
{
    public override string Name => "Technical";
    public override Color FillColor => Color.FromArgb(100, 255, 240, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
}

public class BaseRoom : CommonRoomType
{
    public override string Name => "BaseRoom";
    public override bool IsHidden => true;
}

// ============================================================
// ДЕПАРТАМЕНТЫ (Departmental)
// ============================================================

public class Command : DepartmentalRoomType
{
    public override string Name => "Command";
    public override Color FillColor => Color.FromArgb(100, 51, 77, 109);
    public override Color LineColor => Color.FromArgb(255, 51, 77, 109);
}

public class Medical : DepartmentalRoomType
{
    public override string Name => "Medical";
    public override Color FillColor => Color.FromArgb(100, 82, 180, 233);
    public override Color LineColor => Color.FromArgb(255, 82, 180, 233);
}

public class Service : DepartmentalRoomType
{
    public override string Name => "Service";
    public override Color FillColor => Color.FromArgb(100, 159, 237, 88);
    public override Color LineColor => Color.FromArgb(255, 159, 237, 88);
}

public class Engineering : DepartmentalRoomType
{
    public override string Name => "Engineering";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 239, 179, 65);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
}

public class Security : DepartmentalRoomType
{
    public override string Name => "Security";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 222, 58, 58);
    public override Color LineColor => Color.FromArgb(255, 222, 58, 58);
}

public class Bar : DepartmentalRoomType
{
    public override string Name => "Bar";
    public override Color FillColor => Color.FromArgb(100, 121, 21, 0);
    public override Color LineColor => Color.FromArgb(255, 121, 21, 0);
}

public class Science : DepartmentalRoomType
{
    public override string Name => "Science";
    public override Color FillColor => Color.FromArgb(100, 211, 129, 201);
    public override Color LineColor => Color.FromArgb(255, 211, 129, 201);
}

public class Cargo : DepartmentalRoomType
{
    public override string Name => "Cargo";
    public override Color FillColor => Color.FromArgb(100, 164, 97, 6);
    public override Color LineColor => Color.FromArgb(255, 164, 97, 6);
}

public class Janitor : DepartmentalRoomType
{
    public override string Name => "Janitor";
    public override Color FillColor => Color.FromArgb(100, 140, 52, 127);
    public override Color LineColor => Color.FromArgb(255, 140, 52, 127);
}

public class Chemistry : DepartmentalRoomType
{
    public override string Name => "Chemistry";
    public override Color FillColor => Color.FromArgb(100, 250, 117, 0);
    public override Color LineColor => Color.FromArgb(255, 250, 117, 0);
}

public class Virology : DepartmentalRoomType
{
    public override string Name => "Virology";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 67, 153, 9);
    public override Color LineColor => Color.FromArgb(255, 67, 153, 9);
}

public class Atmospherics : DepartmentalRoomType
{
    public override string Name => "Atmospherics";
    public override string WallProto => "WallReinforced";
    public override Color FillColor => Color.FromArgb(100, 62, 179, 136);
    public override Color LineColor => Color.FromArgb(255, 62, 179, 136);
}

public class Salvage : DepartmentalRoomType
{
    public override string Name => "Salvage";
    public override Color FillColor => Color.FromArgb(100, 141, 28, 153);
    public override Color LineColor => Color.FromArgb(255, 141, 28, 153);
}

public class Neutral : DepartmentalRoomType
{
    public override string Name => "Neutral";
    public override Color FillColor => Color.FromArgb(100, 212, 212, 212);
    public override Color LineColor => Color.FromArgb(255, 212, 212, 212);
}

public class NeutralLight : DepartmentalRoomType
{
    public override string Name => "Neutral Light";
    public override Color FillColor => Color.FromArgb(180, 212, 212, 212);
    public override Color LineColor => Color.FromArgb(200, 212, 212, 212);
}

// ============================================================
// КАСТОМНЫЙ ТИП
// ============================================================

public class CustomRoomType : RoomType
{
    public CustomRoomTypeData Data { get; }

    public CustomRoomType(CustomRoomTypeData data)
    {
        Data = data;
    }

    public override string Name => Data.Name;
    public override string Category => Data.Category;
    public override string WallProto => Data.WallProto;
    public override string FloorProto => Data.FloorProto;
    public override string DoorProto => Data.DoorProto;
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
    public string DoorProto { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
}

public class ExportData
{
    public string Type { get; set; } = "Single";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
}