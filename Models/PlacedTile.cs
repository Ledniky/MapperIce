namespace MapperIce.Models;

/// <summary>
/// Тайл (пол), размещённый вручную через инструмент PlacePrototype,
/// когда прототип имеет тип "tile" (например, FloorSteel). В отличие от
/// пола комнаты (Room.FloorProto), это точечное переопределение конкретной клетки.
/// </summary>
public class PlacedTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Proto { get; set; } = "";
}