// Services/WireTypeManager.cs
using System.Drawing;

namespace MapperIce.Services;

public class WireTypeManager
{
    public string SelectedType { get; private set; } = "HV";
    public event Action? OnTypeChanged;

    private readonly Dictionary<string, WireType> _wireTypes = new();

    public WireTypeManager()
    {
        LoadWireTypes();
    }

    private void LoadWireTypes()
    {
        _wireTypes["HV"] = new WireType
        {
            Name = "HV",
            DisplayName = "ВВ (высоковольтный)",
            Color = Color.FromArgb(220, 255, 140, 0) // оранжевый
        };

        _wireTypes["MV"] = new WireType
        {
            Name = "MV",
            DisplayName = "СВ (средневольтный)",
            Color = Color.FromArgb(220, 240, 220, 0) // жёлтый
        };

        _wireTypes["LV"] = new WireType
        {
            Name = "LV",
            DisplayName = "НВ (низковольтный)",
            Color = Color.FromArgb(220, 60, 200, 60) // зелёный
        };
    }

    public WireType GetWireType(string? typeName = null)
    {
        var key = typeName ?? SelectedType;
        return _wireTypes.TryGetValue(key, out var type) ? type : _wireTypes["HV"];
    }

    public void SelectType(string typeName)
    {
        if (_wireTypes.ContainsKey(typeName))
        {
            SelectedType = typeName;
            OnTypeChanged?.Invoke();
        }
    }

    public List<string> GetTypeNames() => _wireTypes.Keys.ToList();
    public List<WireType> GetTypes() => _wireTypes.Values.ToList();
}

public class WireType
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Color Color { get; set; }
}