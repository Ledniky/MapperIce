// Models/PipeSettings.cs
namespace MapperIce.Models;

public class PipeSettings
{
    public string Layer { get; set; } = "Distra";
    public Color Color { get; set; } = Color.FromArgb(255, 0, 85, 204); // Синий
    public string DisplayName { get; set; } = "Distra";
    public string HexColor => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}{Color.A:X2}";
    
    public static Dictionary<string, PipeSettings> DefaultLayers = new()
    {
        ["Distra"] = new PipeSettings { Layer = "Distra", Color = Color.FromArgb(255, 0, 85, 204), DisplayName = "Distra" },
        ["Waste"] = new PipeSettings { Layer = "Waste", Color = Color.FromArgb(255, 153, 0, 0), DisplayName = "Waste" },
        ["Normal"] = new PipeSettings { Layer = "Normal", Color = Color.FromArgb(255, 0, 204, 68), DisplayName = "Normal" }
    };
}