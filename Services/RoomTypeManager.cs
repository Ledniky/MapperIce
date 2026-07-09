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

    private string _customTypesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce",
        "custom_types.json"
    );

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    // Приоритеты комнат (чем выше число, тем выше приоритет)
    private static readonly Dictionary<string, int> _typePriorities = new()
    {
        // Общие (самый низкий приоритет)
        { "General", 0 },
        { "Technical", 0 },
        { "BaseRoom", 0 },

        // ===== COMMON+ (10) =====
        { "Arrivals", 10 },
        { "Departures", 10 },
        { "ToolStorage", 10 },
        { "Cryo", 10 },
        { "AI", 10 },
        { "Satellite", 10 },
        { "Restaurant", 10 },
        { "KitchenBackroom", 10 },
        { "BarBackroom", 10 },
        { "ChapelMorgue", 10 },
        { "Maintenance", 10 },

        // ===== SERVICE+ (10) =====
        { "Library", 10 },
        { "Gym", 10 },
        { "Garden", 10 },
        { "Dorms", 10 },
        { "Toilets", 10 },
        { "LockerRoom", 10 },
        { "Arcade", 10 },
        { "Park", 10 },
        { "Courtroom", 10 },

        // ===== CARGO+ (10) =====
        { "CargoOffice", 10 },
        { "Mailroom", 10 },
        { "Recycling", 10 },

        // ===== COMMAND+ (10) =====
        { "ConferenceRoom", 10 },
        { "NTRep", 10 },
        { "BlueShield", 10 },

        // ===== MEDICAL+ (10) =====
        { "MedicalBreakRoom", 10 },
        { "Cryogenetics", 10 },
        { "OperatingTheatre", 10 },
        { "Paramedic", 10 },
        { "Psychologist", 10 },
        { "MedicalStorage", 10 },

        // ===== SCIENCE+ (10) =====
        { "Anomalistics", 10 },
        { "Robotics", 10 },
        { "Xenobiology", 10 },
        { "Toxins", 10 },
        { "RnD", 10 },
        { "TestingLab", 10 },

        // ===== ENGINEERING+ (10) =====
        { "GravityGenerator", 10 },
        { "Supermatter", 10 },
        { "Solars", 10 },
        { "Telecoms", 10 },
        { "Router", 10 },

        // ===== SECURITY+ (10) =====
        { "Interrogation", 10 },
        { "Permabrig", 10 },
        { "Execution", 10 },
        { "Checkpoint", 10 },
        { "SecurityPost", 10 },
        { "SecurityOffice", 10 },

        // Служебные
        { "External", 10 },
        { "Service", 10 },
        { "Cargo", 10 },
        { "Janitor", 10 },
        { "Chapel", 10 },
        { "Theatre", 10 },
        { "Lawyer", 10 },
        { "Kitchen", 10 },
        { "Bar", 10 },
        { "Hydroponics", 10 },

        // Средний приоритет
        { "Mining", 30 },
        { "Salvage", 40 },
        { "Atmospherics", 30 },
        { "Chemistry", 70 },
        { "Morgue", 30 },
        { "Virology", 60 },
        { "EVA", 140 },

        // Медицина и Наука
        { "Medical", 10 },
        { "Science", 10 },
        { "ResearchDirector", 50 },
        { "ChiefMedicalOfficer", 50 },

        // Инженерия
        { "Engineering", 10 },
        { "ChiefEngineer", 50 },

        // Безопасность
        { "Detective", 70 },
        { "Brig", 10 },
        { "Armory", 70 },
        { "Security", 100 },
        { "HeadOfSecurity", 100 },

        // Командование
        { "Command", 150 },
        { "HeadOfPersonnel", 150 },
        { "CentralCommand", 160 },
        { "Vault", 150 },

        // Высшее командование
        { "Captain", 200 },
        { "Bridge", 200 },

        // Антагонисты
        { "Syndicate", 250 },
        { "Nukeop", 300 },
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

            InitializeMinimalTypes();
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

    private void InitializeMinimalTypes()
    {
        var minimalTypes = new RoomType[]
        {
            // Существующие основные типы
            new General(),
            new Technical(),
            new Security(),
            new Medical(),
            new Command(),
            new Engineering(),
            new Science(),
            new Cargo(),
            new Service(),
            
            // ===== COMMON+ =====
            new Arrivals(),
            new Departures(),
            new ToolStorage(),
            new Cryo(),
            new AI(),
            new Satellite(),
            new Restaurant(),
            new KitchenBackroom(),
            new BarBackroom(),
            new ChapelMorgue(),
            new Maintenance(),
            
            // ===== SERVICE+ =====
            new Library(),
            new Gym(),
            new Garden(),
            new Dorms(),
            new Toilets(),
            new LockerRoom(),
            new Arcade(),
            new Park(),
            new Courtroom(),
            
            // ===== CARGO+ =====
            new CargoOffice(),
            new Mailroom(),
            new Recycling(),
            
            // ===== COMMAND+ =====
            new ConferenceRoom(),
            new NTRep(),
            new BlueShield(),
            
            // ===== MEDICAL+ =====
            new MedicalBreakRoom(),
            new Cryogenetics(),
            new OperatingTheatre(),
            new Paramedic(),
            new Psychologist(),
            new MedicalStorage(),
            
            // ===== SCIENCE+ =====
            new Anomalistics(),
            new Robotics(),
            new Xenobiology(),
            new Toxins(),
            new RnD(),
            new TestingLab(),
            
            // ===== ENGINEERING+ =====
            new GravityGenerator(),
            new Supermatter(),
            new Solars(),
            new Telecoms(),
            new Router(),
            
            // ===== SECURITY+ =====
            new Interrogation(),
            new Permabrig(),
            new Execution(),
            new Checkpoint(),
            new SecurityPost(),
            new SecurityOffice(),
        };

        foreach (var type in minimalTypes)
        {
            if (type.IsHidden) continue;
            _types[type.Name] = type;
            if (!_categories.ContainsKey(type.Category))
                _categories[type.Category] = new List<RoomType>();
            _categories[type.Category].Add(type);
        }

        if (!_types.ContainsKey(SelectedType))
            SelectedType = "General";
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
        // Проверяем встроенные типы
        if (_typePriorities.TryGetValue(typeName, out var priority))
            return priority;

        // Проверяем кастомные типы
        if (_types.TryGetValue(typeName, out var type) && type is CustomRoomType custom)
            return custom.Priority;

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
    }

    public void ExportType(string typeName, string filePath)
    {
        var type = GetRoomType(typeName);
        if (type == null) return;

        var data = new ExportData
        {
            Type = "Single",
            Name = type.Name,
            Category = type.Category,
            WallProto = type.WallProto,
            FloorProto = type.FloorProto,
            DoorProto = type.DoorProto,
            GlassDoorProto = type.GlassDoorProto,
            FillColor = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
            LineColor = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
            Priority = GetPriorityForType(type.Name)
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

        var dataList = types.Select(type => new ExportData
        {
            Type = "Category",
            Name = type.Name,
            Category = type.Category,
            WallProto = type.WallProto,
            FloorProto = type.FloorProto,
            DoorProto = type.DoorProto,
            GlassDoorProto = type.GlassDoorProto,
            FillColor = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
            LineColor = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
            Priority = GetPriorityForType(type.Name)
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