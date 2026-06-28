using System.Drawing;
using System.Text.Json.Serialization;

namespace MapperIce.Models;

public class ProjectData
{
    public List<RoomData> Rooms { get; set; } = new();
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
    public string RoomType { get; set; } = "General";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
}