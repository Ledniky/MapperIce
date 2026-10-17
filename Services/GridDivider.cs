// Services/GridDivider.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Инструмент «Разделитель гридов». Пользователь накапливает область на гриде
/// (НОМЕРНЫЕ ТАЙЛЫ «Выделить»/«Вычесть» — хранятся в Grid.DividerAreas и перед
/// экспортом визуализируются цветной подсветкой). При экспорте в .yml выделенные
/// клетки выносятся из исходного грида в отдельные новые грид-слои: каждый
/// СВЯЗНЫЙ кусок (4-соседство) каждой области — свой грид, чтобы несоединённые
/// зелёные зоны не «слипались» и неслись в игру как отдельные физические гриды.
///
/// Правила переноса в новый грид (из эффективной области):
///  - Сущности (MapEntity любого конкретного типа): чей ЦЕНТР (X,Y в тайлах, с
///    дробной частью) лежит ВНУТРИ куска области.
///  - Декали (PlacedDecal): принадлежащие комнате (PatternOwnerId) уезжают
///    ВМЕСТЕ с этой комнатой, даже если их клетка вне выделения; остальные —
///    по центру клетки.
///  - Тайлы пола (PlacedTile): чья целочисленная клетка внутри куска области.
///  - Комнаты (Room): ДЕЛЯТСЯ по выделению. В каждый кусок попадает клон комнаты,
///    в котором оставлены только клетки этого куска (остальные вырезаны через
///    RemovedCells), поэтому пол/стены/двери/окна строятся по фактической части;
///    двери/проходы/окна вне части отбрасываются.
///  - Свободные двери (LooseDoors): по целой клетке внутри куска области.
/// Каждый связный кусок области становится отдельным гридом. Всё, что осталось вне
/// областей, тоже делится на связные куски (4-соседство клеток).
/// </summary>
public static class GridDivider
{
    /// <summary>
    /// Возвращает гриды для выделенной области: по одному на каждый СВЯЗНЫЙ кусок
    /// её клеток (4-соседство). Несоединённые зелёные зоны становятся разными
    /// гридами. Кусок без содержимого (только пустые клетки) не выносится.
    /// <paramref name="firstNumber"/> — с какого номера начинать подписи «(область N)».
    /// Не изменяет исходный грид.
    /// </summary>
    public static List<Grid> ExtractAreaParts(Grid sourceGrid, GridDividerArea area, int firstNumber = 1)
    {
        // Связные куски выделенных клеток (4-соседство: стороны, без диагоналей).
        var componentOf = new Dictionary<(int X, int Y), int>(area.Cells.Count);
        var components = new List<List<(int X, int Y)>>();

        foreach (var start in area.Cells.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (componentOf.ContainsKey(start)) continue;

            int id = components.Count;
            var cells = new List<(int X, int Y)>();
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            componentOf[start] = id;

            void Visit(int x, int y)
            {
                var next = (x, y);
                if (area.Contains(x, y) && !componentOf.ContainsKey(next))
                {
                    componentOf[next] = id;
                    queue.Enqueue(next);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                cells.Add(cell);

                Visit(cell.X + 1, cell.Y);
                Visit(cell.X - 1, cell.Y);
                Visit(cell.X, cell.Y + 1);
                Visit(cell.X, cell.Y - 1);
            }

            components.Add(cells);
        }

        var grids = new List<Grid>(components.Count);
        int number = firstNumber;

        for (int i = 0; i < components.Count; i++)
        {
            var newGrid = new Grid
            {
                Uid = 0,
                Name = $"{sourceGrid.Name} (область {number})",
                Position = sourceGrid.Position,
                Color = sourceGrid.Color,
                IsStaticGrid = sourceGrid.IsStaticGrid,
                IsVisible = true,
                ExteriorWallProto = sourceGrid.ExteriorWallProto,
                ReplaceExteriorWalls = sourceGrid.ReplaceExteriorWalls
            };

            bool InComponent(int x, int y) =>
                componentOf.TryGetValue((x, y), out int cid) && cid == i;

            bool CenterIn(float x, float y) =>
                InComponent((int)Math.Floor(x), (int)Math.Floor(y));

            // Комната ДЕЛИТСЯ по выделению: в этот грид попадает только та её
            // часть, что лежит в данном куске; остальные её клетки вырезаются
            // через RemovedCells (пол, стены, двери строятся только по остатку).
            foreach (var room in sourceGrid.Rooms)
            {
                bool intersects = false;
                for (int y = room.Y; y < room.Y + room.Height && !intersects; y++)
                {
                    for (int x = room.X; x < room.X + room.Width; x++)
                    {
                        if (room.Contains(x, y) && InComponent(x, y))
                        {
                            intersects = true;
                            break;
                        }
                    }
                }

                if (!intersects) continue;
                newGrid.Rooms.Add(CloneRoomRegion(room, InComponent));
            }

            // Сущности по центру клетки
            foreach (var e in sourceGrid.Entities)
                if (CenterIn(e.X, e.Y)) newGrid.Entities.Add(e);

            // Декали по центру клетки: комната поделена, поэтому узор едет
            // ровно с той частью, на клетках которой он лежит
            foreach (var d in sourceGrid.Decals)
                if (CenterIn(d.X, d.Y)) newGrid.Decals.Add(d);

            // Тайлы пола по целой клетке
            foreach (var t in sourceGrid.Tiles)
                if (componentOf.TryGetValue((t.X, t.Y), out int cid) && cid == i)
                    newGrid.Tiles.Add(t);

            // Свободные двери по целой клетке
            foreach (var d in sourceGrid.LooseDoors)
                if (componentOf.TryGetValue((d.X, d.Y), out int cid) && cid == i)
                    newGrid.LooseDoors.Add(d);

            // Пустые выделенные клетки отдельный грид не создают.
            if (newGrid.Rooms.Count == 0 && newGrid.Entities.Count == 0 &&
                newGrid.Decals.Count == 0 && newGrid.Tiles.Count == 0 &&
                newGrid.LooseDoors.Count == 0)
                continue;

            number++;
            grids.Add(newGrid);
        }

        return grids;
    }

    /// <summary>
    /// Возвращает гриды-«остатки»: всё содержимое sourceGrid, НЕ принадлежащее ни
    /// одной эффективной области, разбитое на связные куски. Границы кусков —
    /// соседние (по стороне) реальные тайлы (пол/стена/дверь), которые строит
    /// TileBuilder; две группы тайлов, между которыми нет соседства, становятся
    /// двумя разными гридами.
    ///
    /// Сущности и декали чанков не дают, поэтому своего грида не образуют: они
    /// прикрепляются к тому куску, на клетке которого стоят, либо к ближайшему.
    /// Комнаты тоже делятся: в остаток попадают только их клетки вне выделения
    /// (и стены/двери/окна этих клеток). Не изменяет исходный грид.
    /// </summary>
    public static List<Grid> ExtractRemainderParts(Grid sourceGrid, TileBuilder tileBuilder)
    {
        bool InAnyArea(int x, int y)
        {
            foreach (var area in sourceGrid.DividerAreas)
                if (area.Contains(x, y)) return true;
            return false;
        }

        // Клетки, которые реально дают тайлы (пол/стена/дверь), — по ним и считаем
        // связность. Собираем остаток во временный грид и просим TileBuilder
        // построить настоящие тайлы: так учитываются вырезанные клетки комнат,
        // стены и проёмы, а не «идеальные» прямоугольники комнат.
        var remainderGrid = new Grid
        {
            Position = sourceGrid.Position,
            IsStaticGrid = sourceGrid.IsStaticGrid,
            ExteriorWallProto = sourceGrid.ExteriorWallProto,
            ReplaceExteriorWalls = sourceGrid.ReplaceExteriorWalls
        };

        // Комнаты тоже делятся: из них вырезаются все клетки, попавшие в выделение.
        foreach (var room in sourceGrid.Rooms)
            remainderGrid.Rooms.Add(CloneRoomRegion(room, (x, y) => !InAnyArea(x, y)));

        foreach (var t in sourceGrid.Tiles)
            if (!InAnyArea(t.X, t.Y)) remainderGrid.Tiles.Add(t);

        foreach (var d in sourceGrid.LooseDoors)
            if (!InAnyArea(d.X, d.Y)) remainderGrid.LooseDoors.Add(d);

        var remainderTiles = tileBuilder.BuildFromRooms(remainderGrid);

        var occupied = new HashSet<(int X, int Y)>();
        foreach (var tile in remainderTiles.GetAllTiles())
            occupied.Add((tile.X, tile.Y));

        // Связные куски (4-соседство: стороны, без диагоналей).
        var componentOf = new Dictionary<(int X, int Y), int>(occupied.Count);
        var components = new List<List<(int X, int Y)>>();

        foreach (var start in occupied.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (componentOf.ContainsKey(start)) continue;

            int id = components.Count;
            var cells = new List<(int X, int Y)>();
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            componentOf[start] = id;

            void Visit(int x, int y)
            {
                var next = (x, y);
                if (occupied.Contains(next) && !componentOf.ContainsKey(next))
                {
                    componentOf[next] = id;
                    queue.Enqueue(next);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                cells.Add(cell);

                Visit(cell.X + 1, cell.Y);
                Visit(cell.X - 1, cell.Y);
                Visit(cell.X, cell.Y + 1);
                Visit(cell.X, cell.Y - 1);
            }

            components.Add(cells);
        }

        Grid NewGrid(string name) => new Grid
        {
            Uid = 0,
            Name = name,
            Position = sourceGrid.Position,
            Color = sourceGrid.Color,
            IsStaticGrid = sourceGrid.IsStaticGrid,
            IsVisible = true,
            ExteriorWallProto = sourceGrid.ExteriorWallProto,
            ReplaceExteriorWalls = sourceGrid.ReplaceExteriorWalls
        };

        bool InAnyAreaAt(float x, float y) =>
            InAnyArea((int)Math.Floor(x), (int)Math.Floor(y));

        // Ближайший кусок для сущности/декали, стоящей вне тайлов (поиск
        // расширяющимися кольцами по Чебышёву).
        int NearestComponent(int x, int y)
        {
            if (componentOf.TryGetValue((x, y), out int cid)) return cid;

            const int maxRadius = 512;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (componentOf.TryGetValue((x + dx, y - r), out cid)) return cid;
                    if (componentOf.TryGetValue((x + dx, y + r), out cid)) return cid;
                }
                for (int dy = -r + 1; dy <= r - 1; dy++)
                {
                    if (componentOf.TryGetValue((x - r, y + dy), out cid)) return cid;
                    if (componentOf.TryGetValue((x + r, y + dy), out cid)) return cid;
                }
            }

            return 0;
        }

        var grids = new List<Grid>(components.Count);

        // Вне областей нет ни одного тайла — оставляем один грид с тем, что есть
        // (обычно только сущности/декали). Чанков у него не будет, генератор
        // запишет chunks: {}.
        if (components.Count == 0)
        {
            var single = NewGrid(sourceGrid.Name);

            foreach (var room in sourceGrid.Rooms)
                single.Rooms.Add(CloneRoomRegion(room, (x, y) => !InAnyArea(x, y)));

            foreach (var e in sourceGrid.Entities)
                if (!InAnyAreaAt(e.X, e.Y)) single.Entities.Add(e);

            foreach (var d in sourceGrid.Decals)
                if (!InAnyAreaAt(d.X, d.Y)) single.Decals.Add(d);

            foreach (var t in sourceGrid.Tiles)
                if (!InAnyArea(t.X, t.Y)) single.Tiles.Add(t);

            foreach (var d in sourceGrid.LooseDoors)
                if (!InAnyArea(d.X, d.Y)) single.LooseDoors.Add(d);

            grids.Add(single);
            return grids;
        }

        // Раскладываем содержимое по кускам за один проход.
        void AddItem<T>(Dictionary<int, List<T>> map, int key, T item)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<T>();
                map[key] = list;
            }
            list.Add(item);
        }

        // Комнаты делятся по кускам: на каждый компонент, в который попали её
        // клетки, создаётся клон комнаты с вырезанными «чужими» клетками.
        var roomsByComponent = new Dictionary<int, List<Room>>();
        foreach (var room in sourceGrid.Rooms)
        {
            var seenComponents = new HashSet<int>();

            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    if (!room.Contains(x, y)) continue;
                    if (!componentOf.TryGetValue((x, y), out int cid)) continue;
                    if (!seenComponents.Add(cid)) continue;

                    int component = cid;
                    var clone = CloneRoomRegion(room, (cx, cy) =>
                        !InAnyArea(cx, cy) &&
                        componentOf.TryGetValue((cx, cy), out int other) && other == component);

                    AddItem(roomsByComponent, component, clone);
                }
            }
        }

        var entitiesByComponent = new Dictionary<int, List<MapEntity>>();
        foreach (var e in sourceGrid.Entities)
        {
            if (InAnyAreaAt(e.X, e.Y)) continue;
            AddItem(entitiesByComponent, NearestComponent((int)Math.Floor(e.X), (int)Math.Floor(e.Y)), e);
        }

        var decalsByComponent = new Dictionary<int, List<PlacedDecal>>();
        foreach (var d in sourceGrid.Decals)
        {
            if (InAnyAreaAt(d.X, d.Y)) continue;
            AddItem(decalsByComponent, NearestComponent((int)Math.Floor(d.X), (int)Math.Floor(d.Y)), d);
        }

        var tilesByComponent = new Dictionary<int, List<PlacedTile>>();
        foreach (var t in sourceGrid.Tiles)
        {
            if (InAnyArea(t.X, t.Y)) continue;
            AddItem(tilesByComponent, NearestComponent(t.X, t.Y), t);
        }

        var doorsByComponent = new Dictionary<int, List<Door>>();
        foreach (var d in sourceGrid.LooseDoors)
        {
            if (InAnyArea(d.X, d.Y)) continue;
            AddItem(doorsByComponent, NearestComponent(d.X, d.Y), d);
        }

        // Один кусок — прежнее имя, несколько — нумеруем части.
        int number = 0;

        for (int i = 0; i < components.Count; i++)
        {
            bool hasContent =
                roomsByComponent.ContainsKey(i) || entitiesByComponent.ContainsKey(i) ||
                decalsByComponent.ContainsKey(i) || tilesByComponent.ContainsKey(i) ||
                doorsByComponent.ContainsKey(i);

            if (!hasContent) continue;

            number++;
            var newGrid = NewGrid(components.Count > 1
                ? $"{sourceGrid.Name} (часть {number})"
                : sourceGrid.Name);

            if (roomsByComponent.TryGetValue(i, out var rooms))
                foreach (var room in rooms)
                    newGrid.Rooms.Add(room.Clone());

            if (entitiesByComponent.TryGetValue(i, out var entities))
                newGrid.Entities.AddRange(entities);

            if (decalsByComponent.TryGetValue(i, out var decals))
                newGrid.Decals.AddRange(decals);

            if (tilesByComponent.TryGetValue(i, out var tiles))
                newGrid.Tiles.AddRange(tiles);

            if (doorsByComponent.TryGetValue(i, out var doors))
                newGrid.LooseDoors.AddRange(doors);

            grids.Add(newGrid);
        }

        return grids;
    }

    /// <summary>
    /// Клон комнаты, в котором оставлены только клетки, для которых
    /// <paramref name="keep"/> вернул true. Остальные клетки уходят в
    /// RemovedCells — TileBuilder не строит на них пол/стены/двери, поэтому
    /// каждая часть комнаты попадает в свой грид без дублей.
    /// Двери/проходы/окна вне оставленной части отбрасываются.
    /// </summary>
    private static Room CloneRoomRegion(Room room, Func<int, int, bool> keep)
    {
        var clone = room.Clone();

        for (int y = room.Y; y < room.Y + room.Height; y++)
        {
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                if (!room.Contains(x, y) || !keep(x, y))
                    clone.RemovedCells.Add((x, y));
            }
        }

        clone.Doors = room.Doors.Where(d => room.Contains(d.X, d.Y) && keep(d.X, d.Y)).ToList();
        clone.Passages = room.Passages.Where(p => room.Contains(p.X, p.Y) && keep(p.X, p.Y)).ToList();
        clone.Windows = room.Windows.Where(w => room.Contains(w.X, w.Y) && keep(w.X, w.Y)).ToList();

        // Линия разреза (сосед — клетка той же комнаты, ушедшая в другой грид).
        // На внутренних клетках разреза стену не ставим, чтобы пол остался
        // сплошным, как в оригинале. На клетках, которые уже были краем комнаты,
        // стену оставляем — иначе в наружном периметре получилась бы дыра.
        for (int y = room.Y; y < room.Y + room.Height; y++)
        {
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                if (!room.Contains(x, y) || !keep(x, y)) continue;

                bool isCut = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (room.Contains(nx, ny) && !keep(nx, ny))
                    {
                        isCut = true;
                        break;
                    }
                }

                if (!isCut) continue;

                bool onOuterBoundary = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!room.Contains(x + dx, y + dy))
                    {
                        onOuterBoundary = true;
                        break;
                    }
                }

                if (!onOuterBoundary && !clone.Passages.Contains((x, y)))
                    clone.Passages.Add((x, y));
            }
        }

        return clone;
    }
}