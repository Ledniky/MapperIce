// Models/AlarmSettings.cs
public class AlarmSettings
{
    public string Id { get; set; } = "AirAlarm";
    public string DisplayName { get; set; } = "Воздушная сигнализация";
    public string Icon { get; set; } = "🔊";
    public Color Color { get; set; } = Color.FromArgb(200, 255, 200, 100);
    public bool AutoLinkDevices { get; set; } = true; // Добавить

    public static Dictionary<string, AlarmSettings> DefaultAlarms = new()
    {
        ["AirAlarm"] = new AlarmSettings
        {
            Id = "AirAlarm",
            DisplayName = "Воздушная сигнализация",
            Icon = "🔊",
            Color = Color.FromArgb(200, 255, 200, 100),
            AutoLinkDevices = false  // ← по умолчанию true
        },
        ["FireAlarm"] = new AlarmSettings
        {
            Id = "FireAlarm",
            DisplayName = "Пожарная сигнализация",
            Icon = "🔥",
            Color = Color.FromArgb(200, 255, 100, 100),
            AutoLinkDevices = true  // ← по умолчанию true
        }
    };
}