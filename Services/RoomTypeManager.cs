using MapperIce.Models;
using System.Reflection;
using System.Text.Json;

namespace MapperIce.Services;

public class RoomTypeManager
{
    public string SelectedType { get; private set; } = "General";
    public event Action? OnTypeChanged;

    private Dictionary<string, RoomType> _types = new();
    private Dictionary<string, List<RoomType>> _categories = new();
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
                    if (!_categories.ContainsKey(instance.Category))
                        _categories[instance.Category] = new List<RoomType>();
                    _categories[instance.Category].Add(instance);
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
                if (!_categories.ContainsKey(type.Category))
                    _categories[type.Category] = new List<RoomType>();
                _categories[type.Category].Add(type);
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
        // Проверяем встроенные типы через свойство Priority
        if (_types.TryGetValue(typeName, out var type))
            return type.Priority;

        return 0;
    }

    public void CreateCustomType(string name, string category, string wallProto, string floorProto,
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
        if (!_categories.ContainsKey(type.Category))
            _categories[type.Category] = new List<RoomType>();
        _categories[type.Category].Add(type);

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    public void EditCustomType(string oldName, string newName, string category, string wallProto,
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
        if (_categories.TryGetValue(oldType.Category, out var list))
        {
            list.Remove(oldType);
            if (list.Count == 0)
                _categories.Remove(oldType.Category);
        }

        var data = new CustomRoomTypeData
        {
            Name = newName,
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
        if (!_categories.ContainsKey(type.Category))
            _categories[type.Category] = new List<RoomType>();
        _categories[type.Category].Add(type);

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
        if (_categories.TryGetValue(type.Category, out var list))
        {
            list.Remove(type);
            if (list.Count == 0)
                _categories.Remove(type.Category);
        }

        if (SelectedType == name)
            SelectedType = _types.Keys.FirstOrDefault() ?? "General";

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    public Dictionary<string, List<RoomType>> GetCategories()
    {
        return _categories;
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

public void ExportType(string typeName, string filePath)
{
    var type = GetRoomType(typeName);
    if (type == null) return;

    // Получаем цвет категории из словаря (если есть)
    string categoryColor = "255,136,136,136"; // по умолчанию серый
    if (_categoryColors != null && _categoryColors.TryGetValue(type.Category, out var color))
    {
        categoryColor = $"{color.A},{color.R},{color.G},{color.B}";
    }

    var data = new ExportData
    {
        Type = "Single",
        Name = type.Name,
        Category = type.Category,
        CategoryColor = categoryColor,  // ← ДОБАВИТЬ
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
    public void ExportCategory(string categoryName, string filePath)
    {
        if (!_categories.TryGetValue(categoryName, out var types) || types.Count == 0)
        {
            MessageBox.Show($"Категория '{categoryName}' пуста или не найдена");
            return;
        }

        // Получаем цвет категории
        string categoryColor = "255,136,136,136";
        if (_categoryColors != null && _categoryColors.TryGetValue(categoryName, out var color))
        {
            categoryColor = $"{color.A},{color.R},{color.G},{color.B}";
        }

        var dataList = types.Select(type => new ExportData
        {
            Type = "Category",
            Name = type.Name,
            Category = type.Category,
            CategoryColor = categoryColor,  // ← ДОБАВИТЬ
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
        if (!_categories.ContainsKey(type.Category))
            _categories[type.Category] = new List<RoomType>();
        _categories[type.Category].Add(type);

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
        MessageBox.Show($"Тип '{type.Name}' импортирован!");
    }

    public void ImportCategory(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dataList = JsonSerializer.Deserialize<List<ExportData>>(json);
        if (dataList == null || dataList.Count == 0) return;

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
            if (!_categories.ContainsKey(type.Category))
                _categories[type.Category] = new List<RoomType>();
            _categories[type.Category].Add(type);
            imported++;
        }

        SaveCustomTypes();
        OnTypeChanged?.Invoke();

        if (skipped > 0)
            MessageBox.Show($"Импортировано: {imported}, пропущено (уже есть): {skipped}");
        else
            MessageBox.Show($"Импортировано: {imported}");
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