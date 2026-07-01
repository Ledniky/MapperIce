// Services/PipeBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

public class PipeBuilder
{
    private readonly PipeTypeManager _pipeTypeManager;

    public PipeBuilder(PipeTypeManager pipeTypeManager)
    {
        _pipeTypeManager = pipeTypeManager;
    }

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