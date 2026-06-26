using MapperIce.Models;
using System.Text.Json;

namespace MapperIce.Services;

public class RoomTypeManager
{
    public string SelectedType { get; private set; } = "BaseRoom";
    public event Action? OnTypeChanged;

    private Dictionary<string, RoomType> _types = new();
    private Dictionary<string, List<RoomType>> _categories = new();

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
        LoadVanillaTypes();
        LoadCustomTypes();
    }

    private void LoadVanillaTypes()
    {
        var vanillaTypes = new RoomType[]
        {
            new BaseRoom(),
            new Armory(),
            new Medical(),
            new Engineering(),
            new Security(),
            new Science(),
            new Cargo(),
            new Kitchen(),
            new Bar()
        };

        foreach (var type in vanillaTypes)
        {
            _types[type.Name] = type;
            if (!_categories.ContainsKey(type.Category))
                _categories[type.Category] = new List<RoomType>();
            _categories[type.Category].Add(type);
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
        catch { }
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
        catch { }
    }

    public void CreateCustomType(string name, string category, string wallProto, string floorProto, Color fillColor, Color lineColor)
    {
        var data = new CustomRoomTypeData
        {
            Name = name,
            Category = category,
            WallProto = wallProto,
            FloorProto = floorProto,
            FillColor = $"{fillColor.A},{fillColor.R},{fillColor.G},{fillColor.B}",
            LineColor = $"{lineColor.A},{lineColor.R},{lineColor.G},{lineColor.B}"
        };

        var type = new CustomRoomType(data);
        _types[type.Name] = type;
        if (!_categories.ContainsKey(type.Category))
            _categories[type.Category] = new List<RoomType>();
        _categories[type.Category].Add(type);

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
            SelectedType = "BaseRoom";

        SaveCustomTypes();
        OnTypeChanged?.Invoke();
    }

    public void EditCustomType(string oldName, string newName, string category, string wallProto, string floorProto, Color fillColor, Color lineColor)
    {
        var oldType = _types.Values.FirstOrDefault(t => t.IsCustom && t.Name == oldName);
        if (oldType == null) return;

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
            FillColor = $"{fillColor.A},{fillColor.R},{fillColor.G},{fillColor.B}",
            LineColor = $"{lineColor.A},{lineColor.R},{lineColor.G},{lineColor.B}"
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

    public RoomType GetType(string? typeName = null)
    {
        var key = typeName ?? SelectedType;
        return _types.TryGetValue(key, out var type) ? type : _types["BaseRoom"];
    }

    public void ApplyTypeToRoom(Room room, string? typeName = null)
    {
        var type = GetType(typeName);
        room.WallProto = type.WallProto;
        room.FloorProto = type.FloorProto;
        room.FillColor = type.FillColor;
        room.LineColor = type.LineColor;
        room.RoomType = type.Name;
    }
}

public class CustomRoomTypeData
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string FillColor { get; set; } = "128,230,230,230";
    public string LineColor { get; set; } = "255,200,200,200";
}

public class CustomRoomType : RoomType
{
    public CustomRoomTypeData Data { get; }

    public CustomRoomType(CustomRoomTypeData data)
    {
        Data = data;
    }

    public override string Name => Data.Name;
    public override string Category => Data.Category;
    public override string WallProto => Data.WallProto;
    public override string FloorProto => Data.FloorProto;
    public override Color FillColor => ParseColor(Data.FillColor);
    public override Color LineColor => ParseColor(Data.LineColor);
    public override bool IsCustom => true;

    private static Color ParseColor(string value)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 4)
                return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }
        catch { }
        return Color.FromArgb(128, 230, 230, 230);
    }
}