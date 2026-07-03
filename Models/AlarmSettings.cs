// Models/AlarmSettings.cs
namespace MapperIce.Models;

public class AlarmSettings
{
    public string Id { get; set; } = "AirAlarm";
    public string DisplayName { get; set; } = "Воздушная сигнализация";
    public string Icon { get; set; } = "🔊";
    public Color Color { get; set; } = Color.FromArgb(200, 255, 200, 100);
    
    public static Dictionary<string, AlarmSettings> DefaultAlarms = new()
    {
        ["AirAlarm"] = new AlarmSettings { Id = "AirAlarm", DisplayName = "Воздушная сигнализация", Icon = "🔊", Color = Color.FromArgb(200, 255, 200, 100) },
        ["FireAlarm"] = new AlarmSettings { Id = "FireAlarm", DisplayName = "Пожарная сигнализация", Icon = "🔥", Color = Color.FromArgb(200, 255, 100, 100) }
    };
}