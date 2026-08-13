// Models/PlacedDecal.cs
namespace MapperIce.Models;

/// <summary>
/// Декаль (визуальный оверлей на полу), размещённая через PlacePrototype,
/// когда прототип имеет тип "decal". В отличие от MapEntity, декали не являются
/// ECS-сущностями в игре — хранятся и экспортируются отдельно, через DecalGrid.
/// </summary>
public class PlacedDecal
{
    public float X { get; set; }
    public float Y { get; set; }
    public string Proto { get; set; } = "";
    public string Color { get; set; } = "#FFFFFFFF";
    public float Rotation { get; set; } = 0;
    public bool Cleanable { get; set; } = false;

    // Заполнено только у декалей, сгенерированных Decal Rule — позволяет снести
    // и перестроить именно их при пересчёте, не трогая ручные декали с левой панели
    public int? PatternOwnerId { get; set; } = null;
    public int PatternLayerIndex { get; set; } = 0;
}