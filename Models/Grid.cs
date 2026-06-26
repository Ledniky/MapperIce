namespace MapperIce.Models;

public class Grid
{
    public int Uid { get; set; }
    public string Name { get; set; } = "grid";
    public PointF Position { get; set; }  // Смещение в тайлах (может быть дробным)
    public List<Room> Rooms { get; set; } = new();
    public List<MapEntity> Entities { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public Color Color { get; set; } = Color.Blue;
    
    // Для отладки
    public override string ToString() => $"{Name} (UID: {Uid})";
}