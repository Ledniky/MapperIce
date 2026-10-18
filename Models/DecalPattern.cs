// Models/DecalPattern.cs
namespace MapperIce.Models;

public enum DecalPosition
{
    SideN, SideS, SideE, SideW,
    OuterCornerNE, OuterCornerNW, OuterCornerSE, OuterCornerSW,
    InnerCornerNE, InnerCornerNW, InnerCornerSE, InnerCornerSW,
    DeadEndN, DeadEndS, DeadEndE, DeadEndW,
    DoorN, DoorS, DoorE, DoorW
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
    public string? Color { get; set; } = null;
    public DecalPatternMode Mode { get; set; } = DecalPatternMode.Auto;
    public List<ManualDecalArea> ManualAreas { get; set; } = new();

    // true — дверь на границе слоя считается обычной стеной: узор Side/Corner/
    // DeadEnd идёт по контуру без разрыва, отдельная направленная декаль двери
    // НЕ ставится. false (по умолчанию) — дверь получает свою декаль DoorN/S/E/W,
    // а контур стены строится в обход неё (сторона с дверью не в счёт).
    public bool TreatDoorAsWall { get; set; } = false;

    public DecalLayer Clone()
    {
        return new DecalLayer
        {
            Name = Name,
            SourcePackId = SourcePackId,
            Enabled = Enabled,
            Color = Color,
            Mode = Mode,
            ManualAreas = ManualAreas.Select(a => a.Clone()).ToList(),
            TreatDoorAsWall = TreatDoorAsWall
        };
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

/// <summary>Как рисуется ручная область слоя.</summary>
public enum ManualAreaMode
{
    /// <summary>Вся область считается сплошной стеной — обрамление рисуется по форме
    /// прямоугольника (как по сплошной стене), внутри выделенной области.</summary>
    WallRegion,
    /// <summary>Рисуется обычный узор вдоль настоящих стен комнаты, но выводятся только
    /// декали, попадающие внутрь области (без виртуальной стены по границе области).
    /// Клетки, вычтенные из формы области («➖ Вычесть» / Subtractions), для узора
    /// считаются стеной: вырез обводится, а не просто исчезает из области.</summary>
    WallsOnly
}

public class ManualDecalArea
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ManualAreaMode Mode { get; set; } = ManualAreaMode.WallRegion;

    // Вычитаемые прямоугольники (абсолютные координаты клеток). В старых
    // сохранениях поля нет — десериализуется как пустой список.
    public List<DecalAreaRect> Subtractions { get; set; } = new();

    // Клетки, в которых декали узора НЕ рисуются (потайлёвая вырезка декалей, «✖»).
    // В отличие от Subtractions/RemovedCells, вырезанная клетка НЕ перестаёт быть частью
    // области для расчёта формы: соседи просто пропускаются в декаль, а текущая клетка
    // не получает своей декали. Остальные декали области не перестраиваются.
    // Координаты абсолютные (в клетках карты), привязка к карте.
    public HashSet<(int X, int Y)> CutCells { get; set; } = new();

    // Потайлёвое изменение ФОРМЫ области (кнопка «➖ Вычесть»): клетки здесь вычитаются из
    // области целиком — они больше НЕ являются частью области ни для расчёта формы, ни для
    // вывода декалей. В отличие от CutCells (убирает только декаль, форма остаётся), эта клетка
    // перестаёт существовать в области: внешний контур/открытые стороны пересчитываются.
    // Координаты абсолютные (в клетках карты).
    public HashSet<(int X, int Y)> RemovedCells { get; set; } = new();

    /// <summary>Клетка входит в область: внутри основного прямоугольника и вне всех вычетов.</summary>
    public bool Contains(int x, int y)
    {
        if (x < X || x >= X + Width || y < Y || y >= Y + Height) return false;
        foreach (var s in Subtractions)
            if (s.Contains(x, y)) return false;
        if (RemovedCells.Contains((x, y))) return false;
        return true;
    }

    public ManualDecalArea Clone()
    {
        return new ManualDecalArea
        {
            X = X, Y = Y, Width = Width, Height = Height, Mode = Mode,
            Subtractions = Subtractions
                .Select(s => new DecalAreaRect { X = s.X, Y = s.Y, Width = s.Width, Height = s.Height })
                .ToList(),
            CutCells = new HashSet<(int X, int Y)>(CutCells),
            RemovedCells = new HashSet<(int X, int Y)>(RemovedCells)
        };
    }
}


/// <summary>Прямоугольник, вычитаемый из ручной области (для непрямоугольных форм).</summary>
public class DecalAreaRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public bool Contains(int x, int y) =>
        x >= X && x < X + Width && y >= Y && y < Y + Height;
}


/// <summary>Формат для экспорта/импорта одного пака в JSON-файл (по образцу ExportData у RoomType).</summary>
public class DecalPackExportData
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Color { get; set; } = "#FFFFFFFF";
    public Dictionary<string, string> Positions { get; set; } = new(); // ключ — DecalPosition.ToString()
}