using System.Drawing;

namespace MapperIce.Services;

public class PipeTypeManager
{
    public string SelectedType { get; private set; } = "Distra";
    public event Action? OnTypeChanged;

    private readonly Dictionary<string, PipeType> _pipeTypes = new();

    public PipeTypeManager()
    {
        LoadPipeTypes();
    }

    private void LoadPipeTypes()
    {
        _pipeTypes["Distra"] = new PipeType
        {
            Name = "Distra",
            DisplayName = "Дистра",
            ProtoStraight = "GasPipeStraight",
            ProtoBend = "GasPipeBend",
            ProtoTJunction = "GasPipeTJunction",
            ProtoFourway = "GasPipeFourway",
            ProtoCap = "GasPipeCap",
            Color = Color.FromArgb(200, 200, 220, 255),
            IconPath = "GasPipeStraight"
        };

        _pipeTypes["Waste"] = new PipeType
        {
            Name = "Waste",
            DisplayName = "Вейст",
            ProtoStraight = "GasPipeWasteStraight",
            ProtoBend = "GasPipeWasteBend",
            ProtoTJunction = "GasPipeWasteTJunction",
            ProtoFourway = "GasPipeWasteFourway",
            ProtoCap = "GasPipeWasteCap",
            Color = Color.FromArgb(200, 255, 200, 200),
            IconPath = "GasPipeWasteStraight"
        };

        _pipeTypes["Normal"] = new PipeType
        {
            Name = "Normal",
            DisplayName = "Обычная",
            ProtoStraight = "GasPipeStraight",
            ProtoBend = "GasPipeBend",
            ProtoTJunction = "GasPipeTJunction",
            ProtoFourway = "GasPipeFourway",
            ProtoCap = "GasPipeCap",
            Color = Color.FromArgb(200, 200, 200, 200),
            IconPath = "GasPipeStraight"
        };
    }

    public PipeType GetPipeType(string? typeName = null)
    {
        var key = typeName ?? SelectedType;
        return _pipeTypes.TryGetValue(key, out var type) ? type : _pipeTypes["Distra"];
    }

    public void SelectType(string typeName)
    {
        if (_pipeTypes.ContainsKey(typeName))
        {
            SelectedType = typeName;
            OnTypeChanged?.Invoke();
        }
    }

    public List<string> GetTypeNames() => _pipeTypes.Keys.ToList();
    public List<PipeType> GetTypes() => _pipeTypes.Values.ToList();
}

public class PipeType
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ProtoStraight { get; set; } = "";
    public string ProtoBend { get; set; } = "";
    public string ProtoTJunction { get; set; } = "";
    public string ProtoFourway { get; set; } = "";
    public string ProtoCap { get; set; } = "";
    public Color Color { get; set; }
    public string IconPath { get; set; } = "";
}