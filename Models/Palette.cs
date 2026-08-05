// Models/Palette.cs
namespace MapperIce.Models;

/// <summary>
/// Палитра цветов декалей ("- type: palette" в YAML) — используется в игровом
/// редакторе декалей для быстрого выбора одного из предустановленных цветов
/// вместо ручного подбора через RGBA.
/// </summary>
public class Palette
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, string> Colors { get; set; } = new();
}