using MapperIce.Models;

namespace MapperIce.Services;

public class PipeBuilder
{
    private readonly PipeTypeManager _pipeTypeManager;
    private (int x, int y)? _pipeStartPoint = null;
    private (int x, int y)? _pipeEndPoint = null;
    private bool _isDrawingPipe = false;

    public PipeBuilder(PipeTypeManager pipeTypeManager)
    {
        _pipeTypeManager = pipeTypeManager;
    }

    public bool IsDrawing => _isDrawingPipe;
    public (int x, int y)? StartPoint => _pipeStartPoint;
    public (int x, int y)? EndPoint => _pipeEndPoint;

    public void StartDrawing(int x, int y)
    {
        _pipeStartPoint = (x, y);
        _pipeEndPoint = (x, y);
        _isDrawingPipe = true;
    }

    public void UpdateEndPoint(int x, int y)
    {
        if (!_isDrawingPipe || _pipeStartPoint == null) return;
        _pipeEndPoint = (x, y);
    }

    public List<(int x, int y)> FinishDrawing(Grid grid, string pipeType)
    {
        if (!_isDrawingPipe || _pipeStartPoint == null || _pipeEndPoint == null || grid == null)
        {
            ResetDrawing();
            return new List<(int x, int y)>();
        }

        var positions = CalculatePipePath(_pipeStartPoint.Value, _pipeEndPoint.Value);
        
        var validPositions = positions
            .Where(pos => HasFloorAt(grid, pos.x, pos.y))
            .ToList();

        if (validPositions.Count == 0)
        {
            ResetDrawing();
            return validPositions;
        }

        var firstPos = validPositions.First();
        var lastPos = validPositions.Last();

        // Проверяем концы
        if (!CanPlaceEndpoint(grid, firstPos.x, firstPos.y, pipeType) ||
            !CanPlaceEndpoint(grid, lastPos.x, lastPos.y, pipeType))
        {
            ResetDrawing();
            return new List<(int x, int y)>();
        }

        // Удаляем старые концы в этих позициях
        RemoveEndpoint(grid, firstPos.x, firstPos.y);
        RemoveEndpoint(grid, lastPos.x, lastPos.y);

        foreach (var pos in validPositions)
        {
            bool isEndpoint = pos.Equals(firstPos) || pos.Equals(lastPos);
            AddPipe(grid, pos.x, pos.y, pipeType, isEndpoint);
        }

        ResetDrawing();
        return validPositions;
    }

    public void ResetDrawing()
    {
        _pipeStartPoint = null;
        _pipeEndPoint = null;
        _isDrawingPipe = false;
    }

    private List<(int x, int y)> CalculatePipePath((int x, int y) start, (int x, int y) end)
    {
        var positions = new List<(int x, int y)>();
        
        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        int stepY = startY <= endY ? 1 : -1;
        for (int y = startY; y != endY + stepY; y += stepY)
        {
            positions.Add((startX, y));
        }

        int stepX = startX <= endX ? 1 : -1;
        int startXPos = startX + stepX;
        for (int x = startXPos; x != endX + stepX; x += stepX)
        {
            positions.Add((x, endY));
        }

        return positions;
    }

    private bool HasFloorAt(Grid grid, int x, int y)
    {
        if (grid == null) return false;

        foreach (var room in grid.Rooms)
        {
            if (x >= room.X && x < room.X + room.Width &&
                y >= room.Y && y < room.Y + room.Height)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanPlaceEndpoint(Grid grid, int x, int y, string pipeType)
    {
        var existing = grid.Entities
            .OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y && p.IsEndpoint);

        if (existing == null) return true;
        return existing.PipeType == pipeType;
    }

    private void RemoveEndpoint(Grid grid, int x, int y)
    {
        var endpoint = grid.Entities
            .OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y && p.IsEndpoint);

        if (endpoint != null)
            grid.Entities.Remove(endpoint);
    }

    public void AddPipe(Grid grid, int x, int y, string pipeType, bool isEndpoint = false)
    {
        if (grid == null) return;
        if (!HasFloorAt(grid, x, y)) return;

        // Проверяем, есть ли уже конец в этой позиции
        var existingEndpoint = grid.Entities
            .OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y && p.IsEndpoint);

        // Если ставим конец, а там уже есть конец другого типа - нельзя
        if (isEndpoint && existingEndpoint != null && existingEndpoint.PipeType != pipeType)
        {
            return;
        }

        // Если ставим конец, а там уже есть конец того же типа - обновляем
        if (isEndpoint && existingEndpoint != null && existingEndpoint.PipeType == pipeType)
        {
            existingEndpoint.PipeType = pipeType;
            return;
        }

        // Если ставим конец, а там есть труба (не конец) - удаляем трубу и ставим конец
        if (isEndpoint)
        {
            var existingPipe = grid.Entities
                .OfType<PipeEntity>()
                .FirstOrDefault(p => p.X == x && p.Y == y && !p.IsEndpoint);
            
            if (existingPipe != null)
            {
                grid.Entities.Remove(existingPipe);
            }
        }

        // Проверяем, есть ли уже такая труба (не конец) в этой позиции
        // Трубы могут накладываться друг на друга (разные слои)
        var existing = grid.Entities
            .OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y && p.PipeType == pipeType && !p.IsEndpoint);

        // Если такая труба уже есть - не добавляем дубликат
        if (!isEndpoint && existing != null)
        {
            return;
        }

        var pipe = new PipeEntity
        {
            X = x,
            Y = y,
            PipeType = pipeType,
            IsEndpoint = isEndpoint
        };

        grid.Entities.Add(pipe);
    }

    public void RemovePipe(Grid grid, int x, int y)
    {
        var pipe = grid.Entities.OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y);

        if (pipe != null)
            grid.Entities.Remove(pipe);
    }

    public bool HasPipe(Grid grid, int x, int y)
    {
        return grid.Entities.OfType<PipeEntity>()
            .Any(p => p.X == x && p.Y == y);
    }

    public PipeEntity? GetPipe(Grid grid, int x, int y)
    {
        return grid.Entities.OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y);
    }

    public List<PipeEntity> GetPipes(Grid grid)
    {
        return grid.Entities.OfType<PipeEntity>().ToList();
    }

    public List<string> GetPipeTypesAt(Grid grid, int x, int y)
    {
        return grid.Entities
            .OfType<PipeEntity>()
            .Where(p => p.X == x && p.Y == y)
            .Select(p => p.PipeType)
            .ToList();
    }

    public bool IsEndpoint(Grid grid, int x, int y)
    {
        return grid.Entities
            .OfType<PipeEntity>()
            .Any(p => p.X == x && p.Y == y && p.IsEndpoint);
    }
}