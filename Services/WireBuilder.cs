// Services/WireBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Топология кабелей строится так же, как у труб (PipeBuilder): явных
/// прототипов "угла"/"тройника"/"крестовины" нет — они читаются из соседства
/// клеток одного типа на этапе отрисовки (см. Renderer.DrawWireLinesBatch).
/// Концепция "конца провода" (аналог IsEndpoint у труб) кабелям не нужна —
/// подстанция/ЛКП ставятся отдельным инструментом на стык двух типов, а не
/// на конец сегмента.
/// </summary>
public class WireBuilder
{
    private readonly WireTypeManager _wireTypeManager;
    private (int x, int y)? _wireStartPoint = null;
    private (int x, int y)? _wireEndPoint = null;
    private bool _isDrawingWire = false;

    public WireBuilder(WireTypeManager wireTypeManager)
    {
        _wireTypeManager = wireTypeManager;
    }

    public bool IsDrawing => _isDrawingWire;
    public (int x, int y)? StartPoint => _wireStartPoint;
    public (int x, int y)? EndPoint => _wireEndPoint;

    public void StartDrawing(int x, int y)
    {
        _wireStartPoint = (x, y);
        _wireEndPoint = (x, y);
        _isDrawingWire = true;
    }

    public void UpdateEndPoint(int x, int y)
    {
        if (!_isDrawingWire || _wireStartPoint == null) return;
        _wireEndPoint = (x, y);
    }

    public List<(int x, int y)> FinishDrawing(Grid grid, string wireType)
    {
        if (!_isDrawingWire || _wireStartPoint == null || _wireEndPoint == null || grid == null)
        {
            ResetDrawing();
            return new List<(int x, int y)>();
        }

        var positions = CalculateWirePath(_wireStartPoint.Value, _wireEndPoint.Value);

        var validPositions = positions
            .Where(pos => HasFloorAt(grid, pos.x, pos.y))
            .ToList();

        foreach (var pos in validPositions)
            AddWire(grid, pos.x, pos.y, wireType);

        ResetDrawing();
        return validPositions;
    }

    public void ResetDrawing()
    {
        _wireStartPoint = null;
        _wireEndPoint = null;
        _isDrawingWire = false;
    }

    private List<(int x, int y)> CalculateWirePath((int x, int y) start, (int x, int y) end)
    {
        var positions = new List<(int x, int y)>();

        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        int stepY = startY <= endY ? 1 : -1;
        for (int y = startY; y != endY + stepY; y += stepY)
            positions.Add((startX, y));

        int stepX = startX <= endX ? 1 : -1;
        int startXPos = startX + stepX;
        for (int x = startXPos; x != endX + stepX; x += stepX)
            positions.Add((x, endY));

        return positions;
    }

    private bool HasFloorAt(Grid grid, int x, int y)
    {
        if (grid == null) return false;
        return grid.Rooms.Any(room => room.Contains(x, y));
    }

    public void AddWire(Grid grid, int x, int y, string wireType)
    {
        if (grid == null) return;
        if (!HasFloorAt(grid, x, y)) return;

        // На одной клетке — максимум один провод каждого типа (как трубы разных
        // типов могут сосуществовать на одной клетке). Повторная укладка того же
        // типа поверх уже существующего — no-op.
        var existing = grid.Entities
            .OfType<WireEntity>()
            .FirstOrDefault(w => (int)w.X == x && (int)w.Y == y && w.WireType == wireType);

        if (existing != null) return;

        grid.Entities.Add(new WireEntity { X = x, Y = y, WireType = wireType });
    }

    public void RemoveWire(Grid grid, int x, int y, string? wireType = null)
    {
        var wire = wireType == null
            ? grid.Entities.OfType<WireEntity>().FirstOrDefault(w => (int)w.X == x && (int)w.Y == y)
            : grid.Entities.OfType<WireEntity>().FirstOrDefault(w => (int)w.X == x && (int)w.Y == y && w.WireType == wireType);

        if (wire != null)
            grid.Entities.Remove(wire);
    }

    /// <summary>
    /// Используется инструментами PlaceSubstation/PlaceApc — проверяет, что на
    /// этой клетке физически лежит провод нужного типа, прежде чем разрешить
    /// поставить переходник (подстанцию/ЛКП).
    /// </summary>
    public bool HasWireOfType(Grid grid, int x, int y, string wireType)
    {
        return grid.Entities.OfType<WireEntity>()
            .Any(w => (int)w.X == x && (int)w.Y == y && w.WireType == wireType);
    }

    public List<WireEntity> GetWires(Grid grid)
    {
        return grid.Entities.OfType<WireEntity>().ToList();
    }
}