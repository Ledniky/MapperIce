using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Services;

public class UndoManager
{
    private List<GridSnapshot> _history = new();
    private int _currentIndex = -1;
    public List<MapEntity> GenericEntities { get; set; } = new();

    // Убираем конструктор с параметрами - используем простой конструктор
    public UndoManager()
    {
    }

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
    public List<FirelockEntity> Firelocks { get; set; } = new();
    public List<AirAlarmEntity> AirAlarms { get; set; } = new();
    public List<FireAlarmEntity> FireAlarms { get; set; } = new();
    public List<MapEntity> GenericEntities { get; set; } = new();

    public GridSnapshot() { }

    public GridSnapshot(Grid grid)
    {
        Rooms = grid.Rooms.Select(r => r.Clone()).ToList();
        Doors = grid.Rooms.SelectMany(r => r.Doors)
            .Select(d => new Door { X = d.X, Y = d.Y, Proto = d.Proto })
            .ToList();
        Pipes = grid.Entities.OfType<PipeEntity>()
            .Select(p => new PipeEntity { X = p.X, Y = p.Y, PipeType = p.PipeType, IsEndpoint = p.IsEndpoint })
            .ToList();

        Firelocks = grid.Entities.OfType<FirelockEntity>()
            .Select(f => new FirelockEntity { X = f.X, Y = f.Y, Proto = f.Proto, IsGlass = f.IsGlass })
            .ToList();

        AirAlarms = grid.Entities.OfType<AirAlarmEntity>()
            .Select(a => new AirAlarmEntity { X = a.X, Y = a.Y, Rotation = a.Rotation })
            .ToList();
        FireAlarms = grid.Entities.OfType<FireAlarmEntity>()
            .Select(f => new FireAlarmEntity { X = f.X, Y = f.Y, Rotation = f.Rotation })
            .ToList();

        GenericEntities = grid.Entities
            .Where(e => e is not PipeEntity && e is not FirelockEntity &&
                        e is not AirAlarmEntity && e is not FireAlarmEntity)
            .Select(e => new MapEntity { Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid })
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
                PipeType = pipe.PipeType,
                IsEndpoint = pipe.IsEndpoint
            });
        }

        foreach (var firelock in Firelocks)
        {
            grid.Entities.Add(new FirelockEntity
            {
                X = firelock.X,
                Y = firelock.Y,
                Proto = firelock.Proto,
                IsGlass = firelock.IsGlass
            });
        }

        foreach (var alarm in AirAlarms)
        {
            grid.Entities.Add(new AirAlarmEntity { X = alarm.X, Y = alarm.Y, Rotation = alarm.Rotation });
        }
        foreach (var alarm in FireAlarms)
        {
            grid.Entities.Add(new FireAlarmEntity { X = alarm.X, Y = alarm.Y, Rotation = alarm.Rotation });
        }

        foreach (var entity in GenericEntities)
        {
            grid.Entities.Add(new MapEntity
            {
                Proto = entity.Proto,
                X = entity.X,
                Y = entity.Y,
                ParentGridUid = entity.ParentGridUid,
                Rotation = entity.Rotation
            });
        }
    }
}



