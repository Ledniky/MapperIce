// Models/GridDividerArea.cs
namespace MapperIce.Models;

/// <summary>
/// Область разделения грида, которую собирает инструмент «Разделитель гридов».
/// Состоит из прямоугольников «Выделить» (добавляются в область) и «Вычесть»
/// (вырезаются из области). Эффективная область = объединение AddRects минус
/// объединение SubRects. Хранится в модели (сохраняется в .ice-проект) и при
/// экспорте в .yml выносится в отдельный грид-слой.
/// </summary>
public class GridDividerArea
{
    /// <summary>Прямоугольники, добавленные инструментом «Выделить».</summary>
    public List<GridDividerRect> AddRects { get; set; } = new();

    /// <summary>Прямоугольники, добавленные инструментом «Вычесть».</summary>
    public List<GridDividerRect> SubRects { get; set; } = new();

    public bool IsEmpty => AddRects.Count == 0;

    /// <summary>
    /// true, если клетка (x,y) входит в эффективную область разделения:
    /// лежит хотя бы в одном AddRect и не вырезана ни одним SubRect.
    /// </summary>
    public bool Contains(int x, int y)
    {
        if (!InAny(AddRects, x, y)) return false;
        if (InAny(SubRects, x, y)) return false;
        return true;
    }

    private static bool InAny(List<GridDividerRect> rects, int x, int y)
    {
        foreach (var r in rects)
        {
            if (x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height)
                return true;
        }
        return false;
    }

    public GridDividerArea Clone()
    {
        return new GridDividerArea
        {
            AddRects = AddRects.Select(r => new GridDividerRect { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height }).ToList(),
            SubRects = SubRects.Select(r => new GridDividerRect { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height }).ToList()
        };
    }

    /// <summary>Границы эффективной области (минимальный охватывающий прямоугольник) либо null.</summary>
    public (int minX, int minY, int maxX, int maxY)? Bounds
    {
        get
        {
            if (AddRects.Count == 0) return null;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var r in AddRects)
            {
                minX = Math.Min(minX, r.X);
                minY = Math.Min(minY, r.Y);
                maxX = Math.Max(maxX, r.X + r.Width - 1);
                maxY = Math.Max(maxY, r.Y + r.Height - 1);
            }
            return (minX, minY, maxX, maxY);
        }
    }
}

/// <summary>Прямоугольник области разделения (полуинтервалы: [X, X+Width) x [Y, Y+Height)).</summary>
public class GridDividerRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}