namespace MapperIce.Models;

public class Grid
{
    public int Uid { get; set; }
    public string Name { get; set; } = "grid";
    public PointF Position { get; set; }
    public List<Room> Rooms { get; set; } = new();
    public List<MapEntity> Entities { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();
    public List<Door> LooseDoors { get; set; } = new(); // двери, не привязанные к комнате (ставятся при снятом магните)
    public bool IsVisible { get; set; } = true;
    public Color Color { get; set; } = Color.Blue;
    
    public override string ToString() => $"{Name} (UID: {Uid})";
}