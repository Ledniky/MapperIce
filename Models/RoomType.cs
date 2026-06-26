using System.Drawing;

namespace MapperIce.Models;

public abstract class RoomType
{
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string WallProto { get; }
    public abstract string FloorProto { get; }
    public abstract Color FillColor { get; }
    public abstract Color LineColor { get; }
    public virtual bool IsCustom => false;
    public virtual string Description => "";
    public virtual bool CanEdit => !IsCustom;
}

// === Vanilla (только эти по умолчанию) ===
public class BaseRoom : RoomType
{
    public override string Name => "BaseRoom";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 230, 230, 230);
    public override Color LineColor => Color.FromArgb(255, 200, 200, 200);
}

public class Armory : RoomType
{
    public override string Name => "Armory";
    public override string Category => "Vanilla";
    public override string WallProto => "WallReinforced";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 220, 200, 200);
    public override Color LineColor => Color.FromArgb(255, 255, 100, 100);
}

public class Medical : RoomType
{
    public override string Name => "Medical";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 240, 240, 255);
    public override Color LineColor => Color.FromArgb(255, 100, 220, 220);
}

public class Engineering : RoomType
{
    public override string Name => "Engineering";
    public override string Category => "Vanilla";
    public override string WallProto => "WallReinforced";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 255, 220, 200);
    public override Color LineColor => Color.FromArgb(255, 255, 200, 150);
}

public class Security : RoomType
{
    public override string Name => "Security";
    public override string Category => "Vanilla";
    public override string WallProto => "WallReinforced";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 200, 200, 255);
    public override Color LineColor => Color.FromArgb(255, 150, 150, 255);
}

public class Science : RoomType
{
    public override string Name => "Science";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 240, 220, 255);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 255);
}

public class Cargo : RoomType
{
    public override string Name => "Cargo";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 200, 220, 200);
    public override Color LineColor => Color.FromArgb(255, 180, 200, 180);
}

public class Kitchen : RoomType
{
    public override string Name => "Kitchen";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 255, 240, 200);
    public override Color LineColor => Color.FromArgb(255, 255, 200, 150);
}

public class Bar : RoomType
{
    public override string Name => "Bar";
    public override string Category => "Vanilla";
    public override string WallProto => "WallSolid";
    public override string FloorProto => "Plating";
    public override Color FillColor => Color.FromArgb(128, 220, 200, 180);
    public override Color LineColor => Color.FromArgb(255, 180, 160, 140);
}