// Models/GridDividerArea.cs
namespace MapperIce.Models;

/// <summary>
/// Область разделения грида, которую собирает инструмент «Разделитель гридов».
/// Хранит НАБОР КОНКРЕТНЫХ КЛЕТОК (тайлов), а не прямоугольники: «Выделить»
/// добавляет клетки пересечённого драгом прямоугольника, «Вычесть» удаляет их
/// по одной. Поэтому вырезание работает нормально — можно убрать любой тайл,
/// в том числе из середины ранее выделенной области.
/// Хранится в модели (сохраняется в .ice-проект) и при экспорте в .yml
/// выносится в отдельный грид-слой.
/// </summary>
public class GridDividerArea
{
    /// <summary>Выделенные клетки грида (в координатах грида, целые тайлы).</summary>
    public HashSet<(int X, int Y)> Cells { get; set; } = new();

    public bool IsEmpty => Cells.Count == 0;

    /// <summary>true, если клетка (x,y) входит в область.</summary>
    public bool Contains(int x, int y) => Cells.Contains((x, y));

    public void AddCell(int x, int y) => Cells.Add((x, y));

    /// <summary>Добавляет все клетки прямоугольника [x, x+w) × [y, y+h).</summary>
    public void AddRect(int x, int y, int width, int height)
    {
        for (int yy = y; yy < y + height; yy++)
            for (int xx = x; xx < x + width; xx++)
                Cells.Add((xx, yy));
    }

    /// <summary>
    /// Удаляет все клетки прямоугольника. Возвращает число реально удалённых
    /// клеток (для сообщения пользователю).
    /// </summary>
    public int RemoveRect(int x, int y, int width, int height)
    {
        int removed = 0;
        for (int yy = y; yy < y + height; yy++)
            for (int xx = x; xx < x + width; xx++)
                if (Cells.Remove((xx, yy))) removed++;
        return removed;
    }

    public GridDividerArea Clone()
    {
        return new GridDividerArea { Cells = new HashSet<(int X, int Y)>(Cells) };
    }

    /// <summary>Границы области (минимальный охватывающий прямоугольник) либо null.</summary>
    public (int minX, int minY, int maxX, int maxY)? Bounds
    {
        get
        {
            if (Cells.Count == 0) return null;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (x, y) in Cells)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            return (minX, minY, maxX, maxY);
        }
    }
}
