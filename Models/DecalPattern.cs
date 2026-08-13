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

/// <summary>
/// Пак — переиспользуемый шаблон декалей одного визуального стиля (например "Brick").
/// Хранит только id прототипов по позициям, без цвета — цвет и порядок задаются на уровне слоя.
/// </summary>
public class DecalPack
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Новый пак";
    public Dictionary<DecalPosition, string> Positions { get; set; } = new();

    public DecalPack Clone()
    {
        return new DecalPack
        {
            Id = Id,
            Name = Name,
            Positions = new Dictionary<DecalPosition, string>(Positions)
        };
    }

    // Пример под кирпичный набор из задачи. Внутренние углы и дверь там не описаны —
    // оставлены пустыми, пользователь заполняет вручную под свои текстуры.
    public static DecalPack CreateBrickExample()
    {
        return new DecalPack
        {
            Name = "Brick (пример)",
            Positions = new Dictionary<DecalPosition, string>
            {
                [DecalPosition.SideN] = "BrickLineOverlayN",
                [DecalPosition.SideS] = "BrickLineOverlayS",
                [DecalPosition.SideE] = "BrickLineOverlayE",
                [DecalPosition.SideW] = "BrickLineOverlayW",
                [DecalPosition.OuterCornerNE] = "BrickCornerOverlayNE",
                [DecalPosition.OuterCornerNW] = "BrickCornerOverlayNW",
                [DecalPosition.OuterCornerSE] = "BrickCornerOverlaySE",
                [DecalPosition.OuterCornerSW] = "BrickCornerOverlaySW",
                [DecalPosition.DeadEndN] = "BrickEndOverlayN",
                [DecalPosition.DeadEndS] = "BrickEndOverlayS",
                [DecalPosition.DeadEndE] = "BrickEndOverlayE",
                [DecalPosition.DeadEndW] = "BrickEndOverlayW",
            }
        };
    }

    public static Dictionary<string, DecalPack> Examples =
        new[] { CreateBrickExample() }.ToDictionary(p => p.Id, p => p);
}

/// <summary>
/// Один слой "бутерброда" — как слой в фотошопе: свой цвет, своя позиция в стопке
/// (порядок в List&lt;DecalLayer&gt; = порядок отрисовки/экспорта, нижний слой первый),
/// свой набор proto по позициям (преднаполняется из пака, но каждая позиция редактируема).
/// </summary>
public class DecalLayer
{
    public string Name { get; set; } = "Слой";
    public string? SourcePackId { get; set; }
    public string Color { get; set; } = "#FFFFFFFF"; // формат как у PlacedDecal.Color
    public bool Enabled { get; set; } = true;
    public Dictionary<DecalPosition, string> Positions { get; set; } = new();

    public DecalLayer Clone()
    {
        return new DecalLayer
        {
            Name = Name,
            SourcePackId = SourcePackId,
            Color = Color,
            Enabled = Enabled,
            Positions = new Dictionary<DecalPosition, string>(Positions)
        };
    }
}

/// <summary>"Decal Rule" — весь бутерброд слоёв, применённый к комнате (Auto) или к одной ручной области (Manual).</summary>
public class DecalRuleSet
{
    public List<DecalLayer> Layers { get; set; } = new();

    public DecalRuleSet Clone()
    {
        return new DecalRuleSet { Layers = Layers.Select(l => l.Clone()).ToList() };
    }
}

public enum DecalPatternMode { None, Auto, Manual }

/// <summary>
/// Ручная область применения узора внутри комнаты. Прикреплена к комнате (пересчитываема),
/// но границы и глубина — на усмотрение автора. Свой независимый набор слоёв.
/// </summary>
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