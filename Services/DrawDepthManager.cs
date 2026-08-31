// Services/DrawDepthManager.cs
namespace MapperIce.Services;

/// <summary>
/// Управляет слоями отрисовки (drawdepth). Хранит определения слоёв,
/// разрешает имена в целочисленные offset'ы, позволяет менять порядок.
/// </summary>
public class DrawDepthManager
{
    private readonly Dictionary<string, int> _layerOffsets = new();
    private readonly List<string> _layerNames = new();

    public IReadOnlyList<string> LayerNames => _layerNames;
    public IReadOnlyDictionary<string, int> LayerOffsets => _layerOffsets;

    public const int DefaultOffset = 0;

    public DrawDepthManager()
    {
        ResetToDefaults();
    }

    /// <summary>
    /// Сбросить все слои к значениям по умолчанию.
    /// </summary>
    public void ResetToDefaults()
    {
        _layerOffsets.Clear();
        _layerNames.Clear();

        var layers = new (string name, int offset)[]
        {
            ("LowFloors", -22),
            ("ThickPipe", -21),
            ("ThickWire", -20),
            ("ThinPipe", -17),
            ("ThinWire", -16),
            ("BelowFloor", -15),
            ("FloorTiles", -14),
            ("FloorObjects", -13),
            ("Puddles", -12),
            ("HighFloorObjects", -5),
            ("DeadMobs", -4),
            ("SmallMobs", -3),
            ("Walls", -2),
            ("WallTops", -1),
            ("Objects", 0),
            ("SmallObjects", 1),
            ("WallMountedItems", 2),
            ("LargeObjects", 3),
            ("Items", 4),
            ("BelowMobs", 5),
            ("Mobs", 6),
            ("OverMobs", 7),
            ("Doors", 8),
            ("BlastDoors", 9),
            ("Overdoors", 10),
            ("Gasses", 17),
            ("Effects", 18),
            ("Ghosts", 19),
            ("Overlays", 20),
        };

        foreach (var (name, offset) in layers)
        {
            _layerOffsets[name] = offset;
            _layerNames.Add(name);
        }
    }

    /// <summary>
    /// Получить offset слоя по имени. Возвращает 0, если слой не найден.
    /// </summary>
    public int GetOffset(string layerName)
    {
        if (string.IsNullOrEmpty(layerName))
            return DefaultOffset;

        if (_layerOffsets.TryGetValue(layerName, out int offset))
            return offset;

        return DefaultOffset;
    }

    /// <summary>
    /// Установить offset слоя по имени.
    /// </summary>
    public void SetOffset(string layerName, int offset)
    {
        if (string.IsNullOrEmpty(layerName))
            return;

        if (_layerOffsets.ContainsKey(layerName))
        {
            _layerOffsets[layerName] = offset;
        }
        else
        {
            _layerOffsets[layerName] = offset;
            _layerNames.Add(layerName);
        }
    }

    /// <summary>
    /// Сдвинуть слой на N позиций (поднять или опустить).
    /// </summary>
    public void ShiftLayer(string layerName, int steps)
    {
        if (string.IsNullOrEmpty(layerName) || steps == 0)
            return;

        if (_layerOffsets.TryGetValue(layerName, out int current))
        {
            _layerOffsets[layerName] = current + steps;
        }
    }

    /// <summary>
    /// Получить все слои как список пар (имя, offset).
    /// </summary>
    public List<(string Name, int Offset)> GetAllLayers()
    {
        return _layerNames.Select(n => (n, _layerOffsets[n])).ToList();
    }
}
