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
    public List<WireEntity> Wires { get; set; } = new();
    public List<FirelockEntity> Firelocks { get; set; } = new();
    public List<AirAlarmEntity> AirAlarms { get; set; } = new();
    public List<FireAlarmEntity> FireAlarms { get; set; } = new();
    public List<MapEntity> GenericEntities { get; set; } = new();
    /// <summary>
    /// «Привязки» — авто-объекты из свойств комнаты (голопад, шкафчик, скафандр,
    /// кровать, камера). Хранятся отдельно от GenericEntities, потому что тип
    /// сущности здесь значим: рендер и экспорт ищут привязки проходами
    /// OfType&lt;HolopadEntity&gt;()/OfType&lt;CameraEntity&gt;() и т.п., и после отката
    /// привязка не должна превратиться в обычный MapEntity (иначе пропадают
    /// оранжевые маркеры).
    /// </summary>
    public List<MapEntity> AutoObjects { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();
    public List<PlacedDecal> Decals { get; set; } = new();

    /// <summary>
    /// Области инструмента «Разделитель гридов». Хранятся отдельно от комнат,
    /// потому что это самостоятельный слой выделения: без них Ctrl+Z не отменял
    /// добавление/вычитание клеток области.
    /// </summary>
    public List<GridDividerArea> DividerAreas { get; set; } = new();


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
            .Select(p => new PipeEntity
            {
                X = p.X,
                Y = p.Y,
                PipeType = p.PipeType,
                IsEndpoint = p.IsEndpoint,
                CustomColor = p.CustomColor,
                VentProto = p.VentProto,
                EndpointType = p.EndpointType,
                UtilArrowRotation = p.UtilArrowRotation,
                HasFilterMarker = p.HasFilterMarker,
                FilterLabel = p.FilterLabel
            })
            .ToList();

        Wires = grid.Entities.OfType<WireEntity>()
            .Select(w => new WireEntity { X = w.X, Y = w.Y, WireType = w.WireType })
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
            .Where(e => e is not PipeEntity && e is not WireEntity && e is not FirelockEntity &&
                        e is not AirAlarmEntity && e is not FireAlarmEntity &&
                        !IsAutoObject(e))
            .Select(e => new MapEntity { Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid, Rotation = e.Rotation, PipeLayer = e.PipeLayer,
                                         NetworkLinks = e.NetworkLinks.Select(l => new EntityNetworkLink { TargetX = l.TargetX, TargetY = l.TargetY, SourcePort = l.SourcePort, TargetPort = l.TargetPort }).ToList() })
            .ToList();

        AutoObjects = grid.Entities
            .Where(IsAutoObject)
            .Select(CloneAutoObject)
            .ToList();

        Tiles = grid.Tiles.Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto }).ToList();

        DividerAreas = grid.DividerAreas.Select(a => a.Clone()).ToList();

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
        grid.DividerAreas.Clear();

        foreach (var area in DividerAreas)
            grid.DividerAreas.Add(area.Clone());

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
                IsEndpoint = pipe.IsEndpoint,
                CustomColor = pipe.CustomColor,
                VentProto = pipe.VentProto,
                EndpointType = pipe.EndpointType,
                UtilArrowRotation = pipe.UtilArrowRotation,
                HasFilterMarker = pipe.HasFilterMarker,
                FilterLabel = pipe.FilterLabel
            });
        }

        foreach (var wire in Wires)
        {
            grid.Entities.Add(new WireEntity
            {
                X = wire.X,
                Y = wire.Y,
                WireType = wire.WireType
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
                Rotation = entity.Rotation,
                PipeLayer = entity.PipeLayer,
                NetworkLinks = entity.NetworkLinks.Select(l => new EntityNetworkLink { TargetX = l.TargetX, TargetY = l.TargetY, SourcePort = l.SourcePort, TargetPort = l.TargetPort }).ToList()
            });
        }

        foreach (var autoObject in AutoObjects)
        {
            grid.Entities.Add(CloneAutoObject(autoObject));
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

    /// <summary>«Привязка» — авто-объект из свойств комнаты (см. AutoObjects).</summary>
    private static bool IsAutoObject(MapEntity e) =>
        e is HolopadEntity or LockerEntity or SuitStorageEntity or BedEntity or CameraEntity or DresserEntity or IntercomEntity or FaxMachineEntity;

    /// <summary>
    /// Копия сущности с сохранением КОНКРЕТНОГО типа. Общий путь через
    /// new MapEntity {...} для привязок не годится: рендер и экспорт различают их
    /// именно по типу (OfType&lt;CameraEntity&gt;() и т.п.), поэтому после отката
    /// привязка обязана остаться, например, CameraEntity, а не стать MapEntity.
    /// Новый тип привязки достаточно добавить в этот switch и в IsAutoObject.
    /// </summary>
    private static MapEntity CloneAutoObject(MapEntity e) => e switch
    {
        HolopadEntity h => new HolopadEntity
        {
            Proto = h.Proto, X = h.X, Y = h.Y, Rotation = h.Rotation, HolopadProto = h.HolopadProto
        },
        LockerEntity l => new LockerEntity
        {
            Proto = l.Proto, X = l.X, Y = l.Y, Rotation = l.Rotation, LockerProto = l.LockerProto
        },
        SuitStorageEntity s => new SuitStorageEntity
        {
            Proto = s.Proto, X = s.X, Y = s.Y, Rotation = s.Rotation, SuitStorageProto = s.SuitStorageProto
        },
        BedEntity b => new BedEntity
        {
            Proto = b.Proto, X = b.X, Y = b.Y, Rotation = b.Rotation,
            BedProto = b.BedProto, BedsheetProto = b.BedsheetProto
        },
        CameraEntity c => new CameraEntity
        {
            Proto = c.Proto, X = c.X, Y = c.Y, Rotation = c.Rotation, CameraProto = c.CameraProto
        },
        DresserEntity d => new DresserEntity
        {
            Proto = d.Proto, X = d.X, Y = d.Y, Rotation = d.Rotation, DresserProto = d.DresserProto
        },
        IntercomEntity i => new IntercomEntity
        {
            Proto = i.Proto, X = i.X, Y = i.Y, Rotation = i.Rotation, IntercomProto = i.IntercomProto
        },
        FaxMachineEntity f => new FaxMachineEntity
        {
            Proto = f.Proto, X = f.X, Y = f.Y, Rotation = f.Rotation,
            FaxProto = f.FaxProto, FaxName = f.FaxName
        },
        _ => new MapEntity
        {
            Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid, Rotation = e.Rotation,
            NetworkLinks = e.NetworkLinks
                .Select(l => new EntityNetworkLink { TargetX = l.TargetX, TargetY = l.TargetY, SourcePort = l.SourcePort, TargetPort = l.TargetPort })
                .ToList()
        }
    };
}
