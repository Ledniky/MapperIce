// Services/PipeBuilder.cs
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

    /// <summary>
    /// Начать рисование трубы
    /// </summary>
    public void StartDrawing(int x, int y)
    {
        _pipeStartPoint = (x, y);
        _pipeEndPoint = (x, y);
        _isDrawingPipe = true;
    }

    /// <summary>
    /// Обновить конечную точку (при движении мыши)
    /// </summary>
    public void UpdateEndPoint(int x, int y)
    {
        if (!_isDrawingPipe || _pipeStartPoint == null) return;
        _pipeEndPoint = (x, y);
    }

    /// <summary>
    /// Завершить рисование и построить трубы
    /// </summary>
    public List<(int x, int y)> FinishDrawing(Grid grid, string pipeType)
    {
        if (!_isDrawingPipe || _pipeStartPoint == null || _pipeEndPoint == null || grid == null)
        {
            ResetDrawing();
            return new List<(int x, int y)>();
        }

        var positions = CalculatePipePath(_pipeStartPoint.Value, _pipeEndPoint.Value);
        
        // Добавляем трубы
        foreach (var pos in positions)
        {
            AddPipe(grid, pos.x, pos.y, pipeType);
        }

        ResetDrawing();
        return positions;
    }

    /// <summary>
    /// Сбросить рисование
    /// </summary>
    public void ResetDrawing()
    {
        _pipeStartPoint = null;
        _pipeEndPoint = null;
        _isDrawingPipe = false;
    }

    /// <summary>
    /// Рассчитать путь трубы (сначала по Y, потом по X)
    /// </summary>
    private List<(int x, int y)> CalculatePipePath((int x, int y) start, (int x, int y) end)
    {
        var positions = new List<(int x, int y)>();
        
        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        // Сначала идем по Y
        int stepY = startY <= endY ? 1 : -1;
        for (int y = startY; y != endY + stepY; y += stepY)
        {
            positions.Add((startX, y));
        }

        // Потом идем по X (начиная со следующей позиции, чтобы не дублировать угол)
        int stepX = startX <= endX ? 1 : -1;
        int startXPos = startX + stepX;
        for (int x = startXPos; x != endX + stepX; x += stepX)
        {
            positions.Add((x, endY));
        }

        return positions;
    }

    /// <summary>
    /// Добавить трубу в позицию (toggle)
    /// </summary>
    public void AddPipe(Grid grid, int x, int y, string pipeType)
    {
        if (grid == null) return;

        // Проверяем, есть ли уже труба в этой позиции
        var existingPipe = grid.Entities.OfType<PipeEntity>()
            .FirstOrDefault(p => p.X == x && p.Y == y);

        if (existingPipe != null)
        {
            // Если труба уже есть - удаляем её (toggle)
            grid.Entities.Remove(existingPipe);
            return;
        }

        // Создаем новую трубу
        var pipe = new PipeEntity
        {
            X = x,
            Y = y,
            PipeType = pipeType
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
}