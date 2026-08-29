namespace MapperIce.Models;

public class Grid
{
    public int Uid { get; set; }
    public string Name { get; set; } = "grid";
    public PointF Position { get; set; }
    public List<Room> Rooms { get; set; } = new();
    public List<MapEntity> Entities { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();
    public List<PlacedDecal> Decals { get; set; } = new();
    public List<Door> LooseDoors { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public Color Color { get; set; } = Color.Blue;

    /// <summary>
    /// Автоматическое смещение по Y для данного слоя относительно слоя с индексом 1 (нулевого в списке).
    /// Формула: индекс 0,1 → 0; индекс 2 → 0.6; индекс 3 → 1.2; индекс 4 → 1.8 и т.д.
    /// </summary>
    public static float GetLayerOffsetY(int index0Based)
    {
        return (1 - index0Based) * 0.72f;
    }

    public Grid Clone()
    {
        return new Grid
        {
            Uid = Uid,
            Name = Name,
            Position = Position,
            IsVisible = IsVisible,
            Color = Color,
            Rooms = Rooms.ToList(),
            Entities = Entities.ToList(),
            Tiles = Tiles.ToList(),
            Decals = Decals.ToList(),
            LooseDoors = LooseDoors.ToList()
        };
    }
    
    public override string ToString() => $"{Name} (UID: {Uid})";
}