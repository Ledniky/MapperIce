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
///  - Комнаты (Room): всё, что ПЕРЕСЕКАЕТ область, переносится ЦЕЛИКОМ (с её
///    стенами/дверями/проходами/окнами и привязками), т.к. комнату нельзя разрезать
///    без потери логики узора декалей; комната попадает в тот кусок, с которым
///    пересекается.
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

        // Комната неделима: целиком уходит в тот кусок, с которым пересекается
        // первой своей клеткой (иначе её стены/двери продублировались бы).
        var roomsByComponent = new Dictionary<int, List<Room>>();
        foreach (var room in sourceGrid.Rooms)
        {
            int cid = -1;
            for (int y = room.Y; y < room.Y + room.Height && cid < 0; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    if (componentOf.TryGetValue((x, y), out int found))
                    {
                        cid = found;
                        break;
                    }
                }
            }

            if (cid < 0) continue;

            if (!roomsByComponent.TryGetValue(cid, out var list))
            {
                list = new List<Room>();
                roomsByComponent[cid] = list;
            }
            list.Add(room);
        }

        // Куда ушла каждая комната этой области (по Id) — чтобы её декали
        // поехали вместе с ней, даже если лежат вне выделенных клеток.
        var decalOwnerComponent = new Dictionary<int, int>();
        foreach (var (cid, rooms) in roomsByComponent)
            foreach (var room in rooms)
                decalOwnerComponent[room.Id] = cid;

        var allRoomIds = new HashSet<int>();
        foreach (var room in sourceGrid.Rooms)
            allRoomIds.Add(room.Id);

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

            bool CenterIn(float x, float y)
            {
                var cell = ((int)Math.Floor(x), (int)Math.Floor(y));
                return componentOf.TryGetValue(cell, out int cid) && cid == i;
            }

            if (roomsByComponent.TryGetValue(i, out var rooms))
                foreach (var room in rooms)
                    newGrid.Rooms.Add(room.Clone());

            // Сущности по центру клетки
            foreach (var e in sourceGrid.Entities)
                if (CenterIn(e.X, e.Y)) newGrid.Entities.Add(e);

            // Декали: принадлежащие уехавшей комнате — вместе с ней (даже вне
            // выделения), остальные — по центру клетки.
            foreach (var d in sourceGrid.Decals)
            {
                if (d.PatternOwnerId is int ownerId)
                {
                    if (decalOwnerComponent.TryGetValue(ownerId, out int ownerComponent))
                    {
                        if (ownerComponent == i) newGrid.Decals.Add(d);
                        continue;
                    }

                    // Комната этой декали уехала в другую область или осталась
                    // в остатке — здесь её не дублируем.
                    if (allRoomIds.Contains(ownerId)) continue;
                }

                if (CenterIn(d.X, d.Y)) newGrid.Decals.Add(d);
            }

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
    /// Комнаты, пересекающие любую область, целиком уходят в область и в остатке
    /// не участвуют. Не изменяет исходный грид.
    /// </summary>
    public static List<Grid> ExtractRemainderParts(Grid sourceGrid, TileBuilder tileBuilder)
    {
        bool InAnyArea(int x, int y)
        {
            foreach (var area in sourceGrid.DividerAreas)
                if (area.Contains(x, y)) return true;
            return false;
        }

        // Комнаты, пересекающие область, уходят в неё целиком — их клетки в
        // остатке не учитываются вовсе (иначе часть комнаты образовала бы
        // «фантомный» кусок без самой комнаты).
        var movedRooms = new HashSet<Room>();
        foreach (var room in sourceGrid.Rooms)
            if (sourceGrid.DividerAreas.Any(a => RoomOverlapsArea(room, a)))
                movedRooms.Add(room);

        // Id комнат, ушедших в выделение: их декали тоже уезжают с комнатой,
        // даже если стоят на клетках вне выделения.
        var movedRoomIds = new HashSet<int>();
        foreach (var room in movedRooms)
            movedRoomIds.Add(room.Id);

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

        foreach (var room in sourceGrid.Rooms)
            if (!movedRooms.Contains(room)) remainderGrid.Rooms.Add(room.Clone());

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

        // Компонент комнаты — по любой её клетке с тайлом (верхняя левая может
        // быть вырезана через RemovedCells).
        int ComponentOfRoom(Room room)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (componentOf.TryGetValue((x, y), out int cid))
                        return cid;
            return -1;
        }

        var grids = new List<Grid>(components.Count);

        // Вне областей нет ни одного тайла — оставляем один грид с тем, что есть
        // (обычно только сущности/декали). Чанков у него не будет, генератор
        // запишет chunks: {}.
        if (components.Count == 0)
        {
            var single = NewGrid(sourceGrid.Name);

            foreach (var room in sourceGrid.Rooms)
                if (!movedRooms.Contains(room)) single.Rooms.Add(room.Clone());

            foreach (var e in sourceGrid.Entities)
                if (!InAnyAreaAt(e.X, e.Y)) single.Entities.Add(e);

            foreach (var d in sourceGrid.Decals)
                if (!(d.PatternOwnerId is int ownerId && movedRoomIds.Contains(ownerId)) &&
                    !InAnyAreaAt(d.X, d.Y))
                    single.Decals.Add(d);

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

        var roomsByComponent = new Dictionary<int, List<Room>>();
        foreach (var room in sourceGrid.Rooms)
        {
            if (movedRooms.Contains(room)) continue;
            int cid = ComponentOfRoom(room);
            if (cid >= 0) AddItem(roomsByComponent, cid, room);
        }

        // Комнаты, оставшиеся в остатке: все их декали идут в тот же кусок,
        // в котором лежит комната (узор не должен разъезжаться по гридам).
        var decalOwnerComponent = new Dictionary<int, int>();
        foreach (var (cid, rooms) in roomsByComponent)
            foreach (var room in rooms)
                decalOwnerComponent[room.Id] = cid;

        var entitiesByComponent = new Dictionary<int, List<MapEntity>>();
        foreach (var e in sourceGrid.Entities)
        {
            if (InAnyAreaAt(e.X, e.Y)) continue;
            AddItem(entitiesByComponent, NearestComponent((int)Math.Floor(e.X), (int)Math.Floor(e.Y)), e);
        }

        var decalsByComponent = new Dictionary<int, List<PlacedDecal>>();
        foreach (var d in sourceGrid.Decals)
        {
            // Декали комнаты, ушедшей в выделение, уже лежат в гриде этой комнаты.
            if (d.PatternOwnerId is int movedOwnerId && movedRoomIds.Contains(movedOwnerId))
                continue;

            // Декаль комнаты, оставшейся в остатке, — вместе со своей комнатой.
            if (d.PatternOwnerId is int ownerId &&
                decalOwnerComponent.TryGetValue(ownerId, out int roomComponent))
            {
                AddItem(decalsByComponent, roomComponent, d);
                continue;
            }

            // Ручные декали (без комнаты-владельца) — по клетке/ближайшему куску.
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

    private static bool RoomOverlapsArea(Room room, GridDividerArea area)
    {
        int maxX = room.X + room.Width - 1;
        int maxY = room.Y + room.Height - 1;
        foreach (var (x, y) in area.Cells)
        {
            if (x >= room.X && x <= maxX && y >= room.Y && y <= maxY)
                return true;
        }
        return false;
    }
}