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
    /// true — статичный грид (станция, BecomesStation, без Physics/Shuttle).
    /// false — динамичный грид (шаттл, с Physics, Shuttle, FTLDrive).
    /// </summary>
    public bool IsStaticGrid { get; set; } = false;

    /// <summary>
    /// Прототип «наружной стены»: стены на внешнем периметре комнаты (та сторона,
    /// за которой — полностью пустой тайл, без пола/стены другой комнаты) создаются
    /// инструментом «Комната» именно этим прототипом. Пусто/Null — использовать
    /// обычный WallProto комнаты.
    /// </summary>
    public string ExteriorWallProto { get; set; } = "";

    /// <summary>
    /// true — стены на внешнем периметре комнаты (за которыми полностью пустой
    /// тайл, включая диагональные) создаются инструментом «Комната» прототипом
    /// ExteriorWallProto вместо обычного WallProto комнаты. По умолчанию включено.
    /// </summary>
    public bool ReplaceExteriorWalls { get; set; } = true;

    /// <summary>
    /// Области разделения инструмента «Разделитель гридов» (прямоугольники
    /// «Выделить»/«Вычесть»). Каждая область при экспорте в .yml выносится
    /// в отдельный новый грид-слой. Сохраняется в .ice-проект.
    /// </summary>
    public List<GridDividerArea> DividerAreas { get; set; } = new();

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
            IsStaticGrid = IsStaticGrid,
            ExteriorWallProto = ExteriorWallProto,
            ReplaceExteriorWalls = ReplaceExteriorWalls,
            Rooms = Rooms.ToList(),
            Entities = Entities.ToList(),
            Tiles = Tiles.ToList(),
            Decals = Decals.ToList(),
            LooseDoors = LooseDoors.ToList(),
            DividerAreas = DividerAreas.Select(a => a.Clone()).ToList()
        };
    }
    
    public override string ToString() => $"{Name} (UID: {Uid})";
}