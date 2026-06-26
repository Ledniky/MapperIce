using System.Drawing;

namespace MapperIce.Models;

public class Room
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    public Color FillColor { get; set; } = Color.FromArgb(128, 230, 230, 230);
    public Color LineColor { get; set; } = Color.FromArgb(255, 200, 200, 200);
    
    public string RoomType { get; set; } = "BaseRoom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";

    public Room Clone()
    {
        return new Room
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            FillColor = FillColor,
            LineColor = LineColor,
            RoomType = RoomType,
            WallProto = WallProto,
            FloorProto = FloorProto
        };
    }
}