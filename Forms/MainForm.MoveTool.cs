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

        // 4. Комната
        var room = grid.Rooms.FirstOrDefault(r =>
            tileX >= r.X && tileX < r.X + r.Width &&
            tileY >= r.Y && tileY < r.Y + r.Height);
        if (room != null && IsObjectIncludedForMove(room)) return room;

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
            bool overlaps = !(room.X + room.Width <= minX || room.X > maxX ||
                               room.Y + room.Height <= minY || room.Y > maxY);
            if (overlaps)
                result.Add(room);
        }

        return result;
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
                room.X = (int)Math.Round(newX);
                room.Y = (int)Math.Round(newY);
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

            // При перемещении комнаты вместе с ней должны сдвигаться её двери
            // и связанные с ними пожарные шлюзы (иначе они рассинхронизируются с новыми стенами)
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
            }
        }

        _moveDragStartWorld = GetPrecisePosition(mouseLocation);
        _moveDidMove = false;
        _isMovingSelection = true;
    }
}
