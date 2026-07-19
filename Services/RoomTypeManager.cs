using MapperIce.Models;
using System.Reflection;
using System.Text.Json;

namespace MapperIce.Services;

public class RoomTypeManager
{
    public string SelectedType { get; private set; } = "General";
    public event Action? OnTypeChanged;

    private Dictionary<string, RoomType> _types = new();
    public Dictionary<string, Color> _categoryColors { get; set; } = new();

    private string _customTypesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce",
        "custom_types.json"
    );

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public RoomTypeManager()
    {
        try
        {
            LoadVanillaTypes();
            LoadCustomTypes();

            if (!_types.ContainsKey(SelectedType))
            {
                SelectedType = _types.Keys.FirstOrDefault() ?? "General";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка в конструкторе RoomTypeManager: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }

    private void LoadVanillaTypes()
    {
        try
        {
#pragma warning disable IL2026, IL2070
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();

            var roomTypes = allTypes
                .Where(t => t.IsClass &&
                        !t.IsAbstract &&
                        t.IsSubclassOf(typeof(RoomType)) &&
                        t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();
#pragma warning restore IL2026, IL2070

            foreach (var type in roomTypes)
            {
                try
                {
#pragma warning disable IL2026
                    var instance = Activator.CreateInstance(type) as RoomType;
#pragma warning restore IL2026
                    if (instance == null) continue;

                    if (instance.IsHidden) continue;

                    _types[instance.Name] = instance;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка создания типа {type.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки типов: {ex.Message}");
            throw;
        }
    }

    private void LoadCustomTypes()
    {
        if (!File.Exists(_customTypesPath)) return;

        try
        {
            var json = File.ReadAllText(_customTypesPath);
#pragma warning disable IL2026, IL3050
            var data = JsonSerializer.Deserialize<List<CustomRoomTypeData>>(json, _jsonOptions);
#pragma warning restore IL2026, IL3050
            if (data == null) return;

            foreach (var item in data)
            {
                var type = new CustomRoomType(item);
                _types[type.Name] = type;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки кастомных типов: {ex.Message}");
        }
    }

    private void SaveCustomTypes()
    {
        try
        {
            var customTypes = _types.Values
                .Where(t => t.IsCustom)
                .Cast<CustomRoomType>()
                .Select(t => t.Data)
                .ToList();

            var dir = Path.GetDirectoryName(_customTypesPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

#pragma warning disable IL2026, IL3050
            var json = JsonSerializer.Serialize(customTypes, _jsonOptions);
#pragma warning restore IL2026, IL3050
            File.WriteAllText(_customTypesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения кастомных типов: {ex.Message}");
        }
    }

    public int GetPriorityForType(string typeName)
    {
        if (_types.TryGetValue(typeName, out var type))
            return type.Priority;

        return 0;
    }

    public void CreateCustomType(string pack, string name, string category, string wallProto, string floorProto,
        string doorProto, string glassDoorProto, Color fillColor, Color lineColor, int priority = 0)
    {
        if (_types.ContainsKey(name))
        {
            MessageBox.Show($"Тип с именем '{name}' уже существует!");
            return;
        }

        var data = new CustomRoomTypeData
        {
            Name = name,
            Pack = string.IsNullOrWhiteSpace(pack) ? "Custom" : pack,
            Category = category,
            WallProto = wallProto,
            FloorProto = floorProto,
            DoorProto = doorProto,
            GlassDoorProto = glassDoorProto,
            FillColor = $"{fillColor.A},{fillColor.R},{fillColor.G},{fillColor.B}",
            LineColor = $"{lineColor.A},{lineColor.R},{lineColor.G},{lineColor.B}",
            Priority = priority
        };

        var type = new CustomRoomType(data);
        _types[type.Name] = type;

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    public void EditCustomType(string oldName, string newName, string pack, string category, string wallProto,
        string floorProto, string doorProto, string glassDoorProto, Color fillColor, Color lineColor, int priority = 0)
    {
        var oldType = _types.Values.FirstOrDefault(t => t.IsCustom && t.Name == oldName);
        if (oldType == null) return;

        if (oldName != newName && _types.ContainsKey(newName))
        {
            MessageBox.Show($"Тип с именем '{newName}' уже существует!");
            return;
        }

        _types.Remove(oldName);

        var data = new CustomRoomTypeData
        {
            Name = newName,
            Pack = string.IsNullOrWhiteSpace(pack) ? "Custom" : pack,
            Category = category,
            WallProto = wallProto,
            FloorProto = floorProto,
            DoorProto = doorProto,
            GlassDoorProto = glassDoorProto,
            FillColor = $"{fillColor.A},{fillColor.R},{fillColor.G},{fillColor.B}",
            LineColor = $"{lineColor.A},{lineColor.R},{lineColor.G},{lineColor.B}",
            Priority = priority
        };

        var type = new CustomRoomType(data);
        _types[type.Name] = type;

        if (SelectedType == oldName)
            SelectedType = newName;

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    public void DeleteCustomType(string name)
    {
        var type = _types.Values.FirstOrDefault(t => t.IsCustom && t.Name == name);
        if (type == null) return;

        _types.Remove(name);

        if (SelectedType == name)
            SelectedType = _types.Keys.FirstOrDefault() ?? "General";

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    /// <summary>
    /// Плоский список категорий (как раньше) — категория -> типы, без учёта набора.
    /// Оставлен для мест, где важна только категория (например, диалог выбора категории при импорте одиночного типа).
    /// </summary>
    public Dictionary<string, List<RoomType>> GetCategories()
    {
        return _types.Values
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Трёхуровневая иерархия: Набор -> Категория -> Типы.
    /// </summary>
    public Dictionary<string, Dictionary<string, List<RoomType>>> GetPackCategories()
    {
        var result = new Dictionary<string, Dictionary<string, List<RoomType>>>();

        foreach (var type in _types.Values)
        {
            if (!result.TryGetValue(type.Pack, out var categories))
            {
                categories = new Dictionary<string, List<RoomType>>();
                result[type.Pack] = categories;
            }

            if (!categories.TryGetValue(type.Category, out var list))
            {
                list = new List<RoomType>();
                categories[type.Category] = list;
            }

            list.Add(type);
        }

        return result;
    }

    public List<string> GetPackNames()
    {
        return _types.Values.Select(t => t.Pack).Distinct().OrderBy(p => p).ToList();
    }

    public void SelectType(string typeName)
    {
        if (_types.ContainsKey(typeName))
        {
            SelectedType = typeName;
            OnTypeChanged?.Invoke();
        }
    }

    public RoomType GetRoomType(string? typeName = null)
    {
        var key = typeName ?? SelectedType;
        return _types.TryGetValue(key, out var type) ? type : _types["General"];
    }

    public void ApplyTypeToRoom(Room room, string? typeName = null)
    {
        var type = GetRoomType(typeName);
        room.WallProto = type.WallProto;
        room.FloorProto = type.FloorProto;
        room.DoorProto = type.DoorProto;
        room.GlassDoorProto = type.GlassDoorProto;
        room.FillColor = type.FillColor;
        room.LineColor = type.LineColor;
        room.RoomType = type.Name;
        room.Priority = type.Priority;
    }

    // ==================== ЭКСПОРТ ====================

    public void ExportType(string typeName, string filePath)
    {
        var type = GetRoomType(typeName);
        if (type == null) return;

        string categoryColor = "255,136,136,136";
        if (_categoryColors != null && _categoryColors.TryGetValue(type.Category, out var color))
        {
            categoryColor = $"{color.A},{color.R},{color.G},{color.B}";
        }

        var data = new ExportData
        {
            Type = "Single",
            Name = type.Name,
            Pack = type.Pack,
            Category = type.Category,
            CategoryColor = categoryColor,
            WallProto = type.WallProto,
            FloorProto = type.FloorProto,
            DoorProto = type.DoorProto,
            GlassDoorProto = type.GlassDoorProto,
            FillColor = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
            LineColor = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
            Priority = type.Priority
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Экспорт всех типов заданной категории внутри конкретного набора
    /// (пара pack+category нужна, чтобы не перепутать одноимённые категории из разных наборов).
    /// </summary>
    public void ExportCategory(string pack, string categoryName, string filePath)
    {
        var types = _types.Values.Where(t => t.Pack == pack && t.Category == categoryName).ToList();
        if (types.Count == 0)
        {
            MessageBox.Show($"Категория '{categoryName}' в наборе '{pack}' пуста или не найдена");
            return;
        }

        string categoryColor = "255,136,136,136";
        if (_categoryColors != null && _categoryColors.TryGetValue(categoryName, out var color))
        {
            categoryColor = $"{color.A},{color.R},{color.G},{color.B}";
        }

        var dataList = types.Select(type => new ExportData
        {
            Type = "Category",
            Name = type.Name,
            Pack = type.Pack,
            Category = type.Category,
            CategoryColor = categoryColor,
            WallProto = type.WallProto,
            FloorProto = type.FloorProto,
            DoorProto = type.DoorProto,
            GlassDoorProto = type.GlassDoorProto,
            FillColor = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
            LineColor = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
            Priority = type.Priority
        }).ToList();

        var json = JsonSerializer.Serialize(dataList, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Экспорт всего набора целиком — все категории и все типы внутри него.
    /// </summary>
    public void ExportPack(string pack, string filePath)
    {
        var types = _types.Values.Where(t => t.Pack == pack).ToList();
        if (types.Count == 0)
        {
            MessageBox.Show($"Набор '{pack}' пуст или не найден");
            return;
        }

        var dataList = types.Select(type =>
        {
            string categoryColor = "255,136,136,136";
            if (_categoryColors != null && _categoryColors.TryGetValue(type.Category, out var color))
            {
                categoryColor = $"{color.A},{color.R},{color.G},{color.B}";
            }

            return new ExportData
            {
                Type = "Pack",
                Name = type.Name,
                Pack = type.Pack,
                Category = type.Category,
                CategoryColor = categoryColor,
                WallProto = type.WallProto,
                FloorProto = type.FloorProto,
                DoorProto = type.DoorProto,
                GlassDoorProto = type.GlassDoorProto,
                FillColor = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
                LineColor = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
                Priority = type.Priority
            };
        }).ToList();

        var json = JsonSerializer.Serialize(dataList, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    // ==================== ИМПОРТ ====================

    private (int imported, int skipped) ImportEntries(List<ExportData> dataList)
    {
        int imported = 0;
        int skipped = 0;

        foreach (var data in dataList)
        {
            if (_types.ContainsKey(data.Name))
            {
                skipped++;
                continue;
            }

            var customData = new CustomRoomTypeData
            {
                Name = data.Name,
                Pack = string.IsNullOrWhiteSpace(data.Pack) ? "Custom" : data.Pack,
                Category = data.Category,
                WallProto = data.WallProto,
                FloorProto = data.FloorProto,
                DoorProto = data.DoorProto,
                GlassDoorProto = data.GlassDoorProto,
                FillColor = data.FillColor,
                LineColor = data.LineColor,
                Priority = data.Priority
            };

            var type = new CustomRoomType(customData);
            _types[type.Name] = type;
            imported++;
        }

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
        return (imported, skipped);
    }

    public void ImportType(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<ExportData>(json);
        if (data == null) return;

        if (_types.ContainsKey(data.Name))
        {
            MessageBox.Show($"Тип с именем '{data.Name}' уже существует!");
            return;
        }

        var customData = new CustomRoomTypeData
        {
            Name = data.Name,
            Pack = string.IsNullOrWhiteSpace(data.Pack) ? "Custom" : data.Pack,
            Category = data.Category,
            WallProto = data.WallProto,
            FloorProto = data.FloorProto,
            DoorProto = data.DoorProto,
            GlassDoorProto = data.GlassDoorProto,
            FillColor = data.FillColor,
            LineColor = data.LineColor,
            Priority = data.Priority
        };

        var type = new CustomRoomType(customData);
        _types[type.Name] = type;

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
        MessageBox.Show($"Тип '{type.Name}' импортирован в набор '{type.Pack}', категорию '{type.Category}'!");
    }

    public void ImportCategory(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dataList = JsonSerializer.Deserialize<List<ExportData>>(json);
        if (dataList == null || dataList.Count == 0) return;

        var (imported, skipped) = ImportEntries(dataList);

        if (skipped > 0)
            MessageBox.Show($"Импортировано: {imported}, пропущено (уже есть): {skipped}");
        else
            MessageBox.Show($"Импортировано: {imported}");
    }

    /// <summary>
    /// Импорт целого набора (файл, созданный ExportPack). Формат идентичен категории —
    /// каждая запись сама несёт свои Pack и Category, поэтому переиспользуем ту же логику.
    /// </summary>
    public void ImportPack(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dataList = JsonSerializer.Deserialize<List<ExportData>>(json);
        if (dataList == null || dataList.Count == 0) return;

        var (imported, skipped) = ImportEntries(dataList);

        if (skipped > 0)
            MessageBox.Show($"Набор импортирован!\nИмпортировано: {imported}, пропущено (уже есть): {skipped}");
        else
            MessageBox.Show($"Набор импортирован!\nИмпортировано: {imported}");
    }

    public List<string> GetAllTypeNames()
    {
        return _types.Keys.ToList();
    }

    public bool TypeExists(string typeName)
    {
        return _types.ContainsKey(typeName);
    }
}