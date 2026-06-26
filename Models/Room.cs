using System.Drawing;

namespace MapperIce.Models;

public class Room
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    // Заливка: светло-серая с прозрачностью 60% (альфа 153)
    public Color FillColor { get; set; } = Color.FromArgb(128, 230, 230, 230);
    
    // Линия: синяя, почти непрозрачная (альфа 200)
    public Color LineColor { get; set; } = Color.FromArgb(255, 200, 200, 200);

    public Room Clone()
    {
        return new Room
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            FillColor = FillColor,
            LineColor = LineColor
        };
    }
}