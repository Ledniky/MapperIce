using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Services;

public class UndoManager
{
    private Dictionary<int, List<GridSnapshot>> _histories = new();
    private Dictionary<int, int> _currentIndices = new();

    private List<GridSnapshot> GetCurrentHistory(int gridUid)
    {
        if (!_histories.TryGetValue(gridUid, out var history))
        {
            history = new List<GridSnapshot>();
            _histories[gridUid] = history;
            _currentIndices[gridUid] = -1;
        }
        return history;
    }

    private int GetCurrentIndex(int gridUid)
    {
        return _currentIndices.TryGetValue(gridUid, out var idx) ? idx : -1;
    }

    public void AddState(Grid grid)
    {
        if (grid == null) return;

        var history = GetCurrentHistory(grid.Uid);
        var currentIndex = GetCurrentIndex(grid.Uid);

        if (currentIndex < history.Count - 1)
        {
            history.RemoveRange(currentIndex + 1, history.Count - currentIndex - 1);
        }

        var snapshot = new GridSnapshot(grid);
        history.Add(snapshot);
        _currentIndices[grid.Uid] = history.Count - 1;
    }

    public bool CanUndo(int gridUid)
    {
        var history = GetCurrentHistory(gridUid);
        var currentIndex = GetCurrentIndex(gridUid);
        return currentIndex > 0;
    }

    public bool CanRedo(int gridUid)
    {
        var history = GetCurrentHistory(gridUid);
        var currentIndex = GetCurrentIndex(gridUid);
        return currentIndex < history.Count - 1;
    }

    public GridSnapshot Undo(int gridUid)
    {
        var history = GetCurrentHistory(gridUid);
        var currentIndex = GetCurrentIndex(gridUid);
        if (currentIndex <= 0) return history[currentIndex];
        _currentIndices[gridUid] = currentIndex - 1;
        return history[_currentIndices[gridUid]];
    }

    public GridSnapshot Redo(int gridUid)
    {
        var history = GetCurrentHistory(gridUid);
        var currentIndex = GetCurrentIndex(gridUid);
        if (currentIndex >= history.Count - 1) return history[currentIndex];
        _currentIndices[gridUid] = currentIndex + 1;
        return history[_currentIndices[gridUid]];
    }

    public void Clear()
    {
        _histories.Clear();
        _currentIndices.Clear();
    }
}

public class GridSnapshot
{
    public List<Room> Rooms { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<Door> LooseDoors { get; set; } = new();
    public List<PipeEntity> Pipes { get; set; } = new();
    public List<FirelockEntity> Firelocks { get; set; } = new();
    public List<AirAlarmEntity> AirAlarms { get; set; } = new();
    public List<FireAlarmEntity> FireAlarms { get; set; } = new();
    public List<MapEntity> GenericEntities { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();
    public List<PlacedDecal> Decals { get; set; } = new();


    public GridSnapshot() { }

    public GridSnapshot(Grid grid)
    {
        Rooms = grid.Rooms.Select(r => r.Clone()).ToList();
        Doors = grid.Rooms.SelectMany(r => r.Doors)
            .Select(d => new Door { X = d.X, Y = d.Y, Proto = d.Proto })
            .ToList();
        LooseDoors = grid.LooseDoors
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
            .Select(e => new MapEntity { Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid, Rotation = e.Rotation })
            .ToList();

        Tiles = grid.Tiles.Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto }).ToList();

        Decals = grid.Decals.Select(d => new PlacedDecal
        {
            X = d.X,
            Y = d.Y,
            Proto = d.Proto,
            Color = d.Color,
            Rotation = d.Rotation,
            Cleanable = d.Cleanable,
            PatternOwnerId = d.PatternOwnerId,
            PatternLayerIndex = d.PatternLayerIndex
        }).ToList();
    }

    public void RestoreTo(Grid grid)
    {
        if (grid == null) return;

        grid.Rooms.Clear();
        grid.Entities.Clear();
        grid.Tiles.Clear();
        grid.LooseDoors.Clear();
        grid.Decals.Clear();

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

        foreach (var door in LooseDoors)
        {
            grid.LooseDoors.Add(new Door { X = door.X, Y = door.Y, Proto = door.Proto });
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

        foreach (var tile in Tiles)
        {
            grid.Tiles.Add(new PlacedTile { X = tile.X, Y = tile.Y, Proto = tile.Proto });
        }

foreach (var decal in Decals)
        {
            grid.Decals.Add(new PlacedDecal
            {
                X = decal.X, Y = decal.Y, Proto = decal.Proto, Color = decal.Color, Rotation = decal.Rotation, Cleanable = decal.Cleanable,
                PatternOwnerId = decal.PatternOwnerId, PatternLayerIndex = decal.PatternLayerIndex
            });
        }
    }
}
