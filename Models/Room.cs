using System.Drawing;

namespace MapperIce.Models;
public class Room
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string RoomType { get; set; } = "General";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public Color FillColor { get; set; } = Color.FromArgb(100, 230, 230, 230);
    public Color LineColor { get; set; } = Color.FromArgb(255, 180, 180, 180);
    public List<Door> Doors { get; set; } = new();
    
    public Room Clone()
    {
        var clone = new Room
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            RoomType = RoomType,
            WallProto = WallProto,
            FloorProto = FloorProto,
            DoorProto = DoorProto,
            GlassDoorProto = GlassDoorProto,
            FillColor = FillColor,
            LineColor = LineColor
        };
        
        foreach (var door in Doors)
        {
            clone.Doors.Add(new Door
            {
                X = door.X,
                Y = door.Y,
                Proto = door.Proto
            });
        }
        
        return clone;
    }
}