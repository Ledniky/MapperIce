// Models/PipeSettings.cs
namespace MapperIce.Models;

public class PipeSettings
{
    public string Layer { get; set; } = "Distra";
    public Color Color { get; set; } = Color.FromArgb(255, 0, 85, 204);
    public string DisplayName { get; set; } = "Distra";
    public bool HasColor { get; set; } = true; // Добавляем флаг
    public string HexColor => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}{Color.A:X2}";
    
    // Прототип вентиляции, ставящейся на концах труб этого слоя при экспорте:
    // "GasVentPump" (подача), "GasVentScrubber" (вывод) или "None" — концы вообще
    // без вентиляции, экспортируются как обычная прямая труба (см. YAMLGenerator.
    // GeneratePipesGrouped). Для "Util" не используется — утилизация экспортируется
    // отдельным путём.
    public string VentProto { get; set; } = "GasVentScrubber";

    public static Dictionary<string, PipeSettings> DefaultLayers = new()
    {
        // Цвета сверены с эталонным экспортом (AtmosPipeColor в игре): Distra —
        // #0055CCFF, Waste — #990000FF, оба полностью непрозрачные
        ["Distra"] = new PipeSettings { Layer = "Distra", Color = Color.FromArgb(255, 0, 85, 204), DisplayName = "Distra", HasColor = true, VentProto = "GasVentPump" },
        ["Waste"] = new PipeSettings { Layer = "Waste", Color = Color.FromArgb(255, 153, 0, 0), DisplayName = "Waste", HasColor = true, VentProto = "GasVentScrubber" },
        // L1 (Normal) — по умолчанию без законцовки: конец трубы экспортируется
        // как обычная прямая труба, без вентиляции/скруббера
        ["Normal"] = new PipeSettings { Layer = "Normal", Color = Color.FromArgb(180, 200, 200, 200), DisplayName = "Normal", HasColor = false, VentProto = "None" },
        ["Util"] = new PipeSettings { Layer = "Util", Color = Color.FromArgb(180, 100, 150, 50), DisplayName = "Утилизация", HasColor = true }
    };
}