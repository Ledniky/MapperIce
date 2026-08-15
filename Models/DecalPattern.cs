// Models/DecalPattern.cs
namespace MapperIce.Models;

public enum DecalPosition
{
    SideN, SideS, SideE, SideW,
    OuterCornerNE, OuterCornerNW, OuterCornerSE, OuterCornerSW,
    InnerCornerNE, InnerCornerNW, InnerCornerSE, InnerCornerSW,
    DeadEndN, DeadEndS, DeadEndE, DeadEndW,
    Door
}

public enum DecalPackSource { Extracted, Custom }

/// <summary>
/// Пак — набор декалей одного визуального стиля. Теперь хранит и цвет (раньше цвет
/// жил в DecalLayer — перенесён сюда, т.к. по факту у одного стиля декалей всегда
/// один и тот же цвет, настраивать его на уровне отдельного слоя было избыточно).
/// </summary>
public class DecalPack
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Новый пак";
    public string Category { get; set; } = "Custom"; // имя папки/категории в дереве DecalPackDialog
    public Dictionary<DecalPosition, string> Positions { get; set; } = new();
    public string Color { get; set; } = "#FFFFFFFF";

    public DecalPackSource Source { get; set; } = DecalPackSource.Custom;

    public override string ToString() => Name;

    public DecalPack Clone()
    {
        return new DecalPack
        {
            Id = Id,
            Name = Name,
            Category = Category,
            Positions = new Dictionary<DecalPosition, string>(Positions),
            Color = Color,
            Source = Source
        };
    }
}

/// <summary>Один слой "бутерброда" — теперь только ссылка на пак + вкл/выкл, без своего цвета и своих позиций.</summary>
public class DecalLayer
{
    public string Name { get; set; } = "Слой";
    public string? SourcePackId { get; set; }
    public bool Enabled { get; set; } = true;

    public DecalLayer Clone()
    {
        return new DecalLayer { Name = Name, SourcePackId = SourcePackId, Enabled = Enabled };
    }
}

public class DecalRuleSet
{
    public List<DecalLayer> Layers { get; set; } = new();

    public DecalRuleSet Clone()
    {
        return new DecalRuleSet { Layers = Layers.Select(l => l.Clone()).ToList() };
    }
}

public enum DecalPatternMode { Auto, Manual }

public class ManualDecalArea
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DecalRuleSet Rule { get; set; } = new();

    public ManualDecalArea Clone()
    {
        return new ManualDecalArea { X = X, Y = Y, Width = Width, Height = Height, Rule = Rule.Clone() };
    }
}

/// <summary>Формат для экспорта/импорта одного пака в JSON-файл (по образцу ExportData у RoomType).</summary>
public class DecalPackExportData
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Color { get; set; } = "#FFFFFFFF";
    public Dictionary<string, string> Positions { get; set; } = new(); // ключ — DecalPosition.ToString()
}