// Forms/MainForm.MoveTool.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{




    // ===== Вспомогательная логика инструмента "Перемещение" =====


    private object? HitTestAt(int tileX, int tileY)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return null;

        // 1. Сущности (сигнализации, трубы, ферлоки, generic-прототипы) — как в Delete,
        // но с Math.Floor вместо усечения (int), иначе дробные/отрицательные координаты промахиваются
        var entity = grid.Entities.FirstOrDefault(e =>
            FloorToInt(e.X) == tileX && FloorToInt(e.Y) == tileY && IsObjectIncludedForMove(e));
        if (entity != null) return entity;

        // 2. Декали — тоже точечные объекты с дробными координатами, как и сущности
        var decal = grid.Decals.FirstOrDefault(d =>
            FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY && IsObjectIncludedForMove(d));
        if (decal != null) return decal;

        // 3. Вручную размещённые тайлы
        var tile = grid.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (tile != null && IsObjectIncludedForMove(tile)) return tile;

        // 4. Комната. Используем room.Contains, а НЕ ограничивающий прямоугольник —
        // иначе клик по вырезанной области (RemovedCells) всё равно выделял бы комнату,
        // хотя этот кусок комнате уже не принадлежит.
        var room = grid.Rooms.FirstOrDefault(r =>
            r.Contains(tileX, tileY) && IsObjectIncludedForMove(r));
        if (room != null) return room;

        return null;
    }


    private List<object> GatherObjectsInRect(int minX, int minY, int maxX, int maxY)
    {
        var grid = _map.ActiveGrid;
        var result = new List<object>();
        if (grid == null) return result;

        foreach (var entity in grid.Entities)
        {
            if (!IsObjectIncludedForMove(entity)) continue;
            if (entity.X >= minX && entity.X <= maxX && entity.Y >= minY && entity.Y <= maxY)
                result.Add(entity);
        }

        foreach (var decal in grid.Decals)
        {
            if (!IsObjectIncludedForMove(decal)) continue;
            if (decal.X >= minX && decal.X <= maxX && decal.Y >= minY && decal.Y <= maxY)
                result.Add(decal);
        }

        foreach (var tile in grid.Tiles)
        {
            if (!IsObjectIncludedForMove(tile)) continue;
            if (tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
                result.Add(tile);
        }

        foreach (var room in grid.Rooms)
        {
            if (!IsObjectIncludedForMove(room)) continue;
            // По реальным клеткам комнаты (room.Contains), а не по ограничивающему
            // прямоугольнику: если рамка захватывает только вырезанные клетки
            // (RemovedCells), комната не выделяется.
            if (!RoomAreaOverlapsBox(room, minX, minY, maxX, maxY))
                continue;
            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// true, если прямоугольник [minX..maxX]×[minY..maxY] пересекает хотя бы одну
    /// РЕАЛЬНУЮ клетку комнаты (ту, что возвращает room.Contains). Вырезанные
    /// клетки (RemovedCells) не считаются — рамка поверх выреза комнату не ловит.
    /// </summary>
    private static bool RoomAreaOverlapsBox(Room room, int minX, int minY, int maxX, int maxY)
    {
        int x0 = Math.Max(minX, room.X);
        int x1 = Math.Min(maxX, room.X + room.Width - 1);
        int y0 = Math.Max(minY, room.Y);
        int y1 = Math.Min(maxY, room.Y + room.Height - 1);

        if (x0 > x1 || y0 > y1) return false;

        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (room.Contains(x, y)) return true;

        return false;
    }





    private static (float x, float y) GetTargetPosition(object target)
    {
        return target switch
        {
            Room room => (room.X, room.Y),
            Door door => (door.X, door.Y),
            PlacedTile tile => (tile.X, tile.Y),
            PlacedDecal decal => (decal.X, decal.Y),
            MapEntity entity => (entity.X, entity.Y),
            _ => (0f, 0f)
        };
    }


    private static void MoveTarget(object target, float newX, float newY)
    {
        switch (target)
        {
            case Room room:
                int oldX = room.X;
                int oldY = room.Y;
                room.X = (int)Math.Round(newX);
                room.Y = (int)Math.Round(newY);
                // Сдвигаем RemovedCells вместе с комнатой, чтобы вырез оставался привязанным к ней
                if (oldX != room.X || oldY != room.Y)
                {
                    int dx = room.X - oldX;
                    int dy = room.Y - oldY;
                    var newRemovedCells = new HashSet<(int X, int Y)>();
                    foreach (var cell in room.RemovedCells)
                    {
                        newRemovedCells.Add((cell.X + dx, cell.Y + dy));
                    }
                    room.RemovedCells = newRemovedCells;

                    // Пометки прохода и окна — это части СТЕН комнаты, они обязаны
                    // ехать вместе с ней. Если их оставить на прежних координатах,
                    // на старом месте остаётся «проход в никуда» (пометка продолжает
                    // глушить стену соседней комнаты, TileBuilder не строит её там
                    // и после переноса), а на новом месте стена восстанавливается и
                    // проход пропадает из уже перенесённой комнаты.
                    room.Passages = room.Passages.Select(p => (p.X + dx, p.Y + dy)).ToList();
                    room.Windows = room.Windows.Select(w => (w.X + dx, w.Y + dy)).ToList();
                }
                break;
            case Door door:
                door.X = (int)Math.Round(newX);
                door.Y = (int)Math.Round(newY);
                break;
            case PlacedTile tile:
                tile.X = (int)Math.Round(newX);
                tile.Y = (int)Math.Round(newY);
                break;
            case PlacedDecal decal:
                // Декали — точечные объекты с дробными координатами, как MapEntity,
                // а не привязанные к целому тайлу (в отличие от PlacedTile)
                decal.X = newX;
                decal.Y = newY;
                break;
            case MapEntity entity:
                entity.X = newX;
                entity.Y = newY;
                break;
        }
    }

    private void BeginMoveDrag(Point mouseLocation)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return;

        _moveSnapshot.Clear();
        var alreadyAdded = new HashSet<object>();

        void AddSnapshot(object target)
        {
            if (!alreadyAdded.Add(target)) return;
            var pos = GetTargetPosition(target);
            _moveSnapshot.Add(new MoveSnapshotItem { Target = target, OrigX = pos.x, OrigY = pos.y });
        }

        foreach (var obj in _selectedObjects)
        {
            AddSnapshot(obj);

            // При перемещении комнаты вместе с ней должны сдвигаться её двери,
            // связанные с ними пожарные шлюзы и декали внутри комнаты
            // (иначе декали в области выреза останутся на месте)
            if (obj is Room room)
            {
                foreach (var door in room.Doors)
                {
                    AddSnapshot(door);

                    var firelock = grid.Entities.OfType<FirelockEntity>()
                        .FirstOrDefault(f => (int)f.X == door.X && (int)f.Y == door.Y);
                    if (firelock != null)
                        AddSnapshot(firelock);
                }

                // Перемещаем декали, находящиеся в пределах комнаты,
                // но НЕ те, что попали в RemovedCells (вырезанную область) —
                // они остаются на абсолютных координатах и не двигаются с комнатой
                foreach (var decal in grid.Decals)
                {
                    if (decal.X >= room.X && decal.X < room.X + room.Width &&
                        decal.Y >= room.Y && decal.Y < room.Y + room.Height)
                    {
                        int dx = FloorToInt(decal.X);
                        int dy = FloorToInt(decal.Y);
                        if (!room.RemovedCells.Contains((dx, dy)))
                        {
                            AddSnapshot(decal);
                        }
                    }
                }
            }
        }

        _moveDragStartWorld = GetPrecisePosition(mouseLocation);
        _moveDidMove = false;
        _isMovingSelection = true;
    }
}
