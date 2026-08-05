using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapperIce.Models;

public class ProjectData
{
    public List<PlacedDecal> Decals { get; set; } = new();
    public List<RoomData> Rooms { get; set; } = new();
    public List<GenericEntityData> Entities { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();   // ← добавлено
    public string? ActiveGridName { get; set; }
    public DateTime LastSaved { get; set; }
    public string? Version { get; set; } = "1.0";
}

public class RoomData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string RoomType { get; set; } = "";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public List<DoorData> Doors { get; set; } = new();
}

public class DoorData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Proto { get; set; } = "Airlock";
}

public class GenericEntityData
{
    public string Type { get; set; } = "";   // имя класса, например "PipeEntity", "FireAlarmEntity"
    public JsonElement Data { get; set; }    // все поля конкретного типа как есть
}