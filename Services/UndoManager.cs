using MapperIce.Models;
using MapperIce.Services; // <-- ДОБАВЛЕНО

namespace MapperIce.Services;

public class UndoManager
{
    private List<GridSnapshot> _history = new();
    private int _currentIndex = -1;

    public void AddState(Grid grid)
    {
        if (grid == null) return;

        if (_currentIndex < _history.Count - 1)
        {
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
        }

        var snapshot = new GridSnapshot(grid);
        _history.Add(snapshot);
        _currentIndex = _history.Count - 1;
    }

    public bool CanUndo => _currentIndex > 0;
    public bool CanRedo => _currentIndex < _history.Count - 1;

    public GridSnapshot Undo()
    {
        if (!CanUndo) return _history[_currentIndex];
        _currentIndex--;
        return _history[_currentIndex];
    }

    public GridSnapshot Redo()
    {
        if (!CanRedo) return _history[_currentIndex];
        _currentIndex++;
        return _history[_currentIndex];
    }

    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
    }
}

public class GridSnapshot
{
    public List<Room> Rooms { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<PipeEntity> Pipes { get; set; } = new();

    public GridSnapshot() { }

    public GridSnapshot(Grid grid)
    {
        Rooms = grid.Rooms.Select(r => r.Clone()).ToList();
        Doors = grid.Rooms.SelectMany(r => r.Doors)
            .Select(d => new Door { X = d.X, Y = d.Y, Proto = d.Proto })
            .ToList();
        Pipes = grid.Entities.OfType<PipeEntity>()
            .Select(p => new PipeEntity { X = p.X, Y = p.Y, PipeType = p.PipeType })
            .ToList();
    }

    public void RestoreTo(Grid grid)
    {
        if (grid == null) return;

        grid.Rooms.Clear();
        grid.Entities.Clear();

        foreach (var room in Rooms)
        {
            var newRoom = room.Clone();
            newRoom.Doors = Doors
                .Where(d => d.X >= room.X && d.X < room.X + room.Width &&
                           d.Y >= room.Y && d.Y < room.Y + room.Height)
                .Select(d => new Door { X = d.X, Y = d.Y, Proto = d.Proto })
                .ToList();
            grid.Rooms.Add(newRoom);
        }

        foreach (var pipe in Pipes)
        {
            grid.Entities.Add(new PipeEntity
            {
                X = pipe.X,
                Y = pipe.Y,
                PipeType = pipe.PipeType
            });
        }
    }
}