namespace MapperIce.Models;

/// <summary>
/// Сущность на карте (предмет, стена, дверь и т.д.)
/// </summary>
public class MapEntity
{
    public string Proto { get; set; } = "";      // "WallSolid", "AirlockArmoryLocked"
    public float X { get; set; }                 // Координата X в тайлах (с дробной частью)
    public float Y { get; set; }                 // Координата Y в тайлах (с дробной частью)
    public int? ParentGridUid { get; set; }      // К какому гриду привязан (если null - к карте)
    public float Rotation { get; set; } = 0;     // Угол поворота в радианах

}