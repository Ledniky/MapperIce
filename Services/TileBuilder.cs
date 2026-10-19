// Services/TileBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

public class TileBuilder
{
    private readonly RoomTypeManager _roomTypeManager;
    private readonly DoorUpdater _doorUpdater;
    private readonly DrawDepthManager _drawDepthManager;
    
    private static readonly Dictionary<string, int> _wallPriority = new()
    {
        { "WallSolid", 0 },
        { "WallReinforced", 1 },
    };

    private static readonly (int dx, int dy)[] _wallDirs =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    public TileBuilder(RoomTypeManager roomTypeManager, DoorUpdater doorUpdater, DrawDepthManager? drawDepthManager = null)
    {
        _roomTypeManager = roomTypeManager;
        _doorUpdater = doorUpdater;
        _drawDepthManager = drawDepthManager ?? new DrawDepthManager();
    }

    private int GetPriority(string wall) => _wallPriority.GetValueOrDefault(wall, 2);
    private string BestWall(string a, string b) => GetPriority(a) >= GetPriority(b) ? a : b;

    private Room? GetRoomAt(List<Room> rooms, int x, int y) => 
        rooms.FirstOrDefault(r => r.Contains(x, y));


private string? GetBoundaryWallProto(Room room, int x, int y)
    {
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

        foreach (var (dx, dy) in directions)
        {
            if (!room.Contains(x + dx, y + dy))
                return room.WallProto; // стена всегда своя — прототип комнаты-владельца клетки
        }

        return null;
    }

    private bool IsEmptyTile(int nx, int ny, List<Room> allRooms, TileGrid tileGrid)
    {
        var tile = tileGrid.GetTile(nx, ny);
        bool hasSolidTile = tile != null && tile.Content != TileContent.Empty &&
                            tile.Content != TileContent.Pipe;
        if (hasSolidTile) return false; // пол/стена/дверь — не «пустой тайл»

        bool coveredByRoom = GetRoomAt(allRooms, nx, ny) != null;
        if (coveredByRoom) return false; // у соседа есть комната — не «пустой тайл»

        return true;
    }

    /// <summary>
    /// Проверяет, выходит ли клетка на ПОЛНОСТЬЮ пустой тайл. Проверяются 4 ближних
    /// и 4 диагональных тайла. Диагональ считается «пустотой» только если сам
    /// диагональный тайл пуст И хотя бы один из двух ортогональных соседей этого угла
    /// тоже выходит на пустоту — так общая стена между двумя смежными комнатами не
    /// ошибочно распознаётся как наружная из-за случайного пустого угла.
    /// </summary>
    private bool FacesEmptyTile(Room room, List<Room> allRooms, TileGrid tileGrid, int x, int y)
    {
        foreach (var (dx, dy) in _wallDirs)
        {
            int nx = x + dx, ny = y + dy;
            if (room.Contains(nx, ny)) continue; // внутренняя сторона
            if (IsEmptyTile(nx, ny, allRooms, tileGrid)) return true;
        }

        foreach (var (dx, dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
        {
            int nx = x + dx, ny = y + dy;
            if (room.Contains(nx, ny)) continue;
            if (!IsEmptyTile(nx, ny, allRooms, tileGrid)) continue;

            // Диагональ годится, если хотя бы один из двух ортогональных соседей
            // угла выходит на пустоту (глубоко наружу), а не примыкает к этой же
            // комнате — иначе внешний угол комнаты ложно стал бы «наружной стеной».
            bool northOrWest = IsEmptyTile(x + dx, y, allRooms, tileGrid);
            bool eastOrSouth = IsEmptyTile(x, y + dy, allRooms, tileGrid);
            if (northOrWest || eastOrSouth) return true;
        }

        return false;
    }

    /// <summary>
    /// Клетка-"пинч" во внутреннем (вогнутом) углу выемки: сама клетка ортогонально
    /// окружена комнатой с обеих сторон (поэтому GetBoundaryWallProto её пропускает
    /// и она остаётся полом), но один из диагональных соседей уже вне комнаты
    /// (вырезан RoomSubtractor'ом или это чужая комната). Без стены здесь между
    /// двумя ортогональными стенами угла образуется диагональная дыра толщиной 0 —
    /// через неё можно пройти по диагонали. Возвращаем proto стены для такой клетки,
    /// превращая угол в сплошной (толщина 1→2 на повороте), либо null, если клетка
    /// не является таким углом. Тот же диагональный признак уже используется в
    /// Renderer.DrawConcaveCornerConnectors и DecalPatternBuilder.TryAddInnerCorner.
    /// </summary>
private string? GetConcaveCornerWallProto(Room room, int x, int y)
    {
        var diagonals = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        foreach (var (dx, dy) in diagonals)
        {
            bool orthoOpen = room.Contains(x + dx, y) && room.Contains(x, y + dy);
            if (!orthoOpen) continue;

            bool diagonalForeign = !room.Contains(x + dx, y + dy);
            if (!diagonalForeign) continue;

            // Отличаем настоящий "пинч" (диагональ вырезана RoomSubtractor'ом изнутри
            // прямоугольника комнаты — там нужна стена, иначе дыра для прохода) от
            // естественного ВНЕШНЕГО угла (диагональ просто вне прямоугольника комнаты
            // вовсе, это нормальная граница, не вырез — там стены здесь быть не должно,
            // угол должен остаться полом для OuterCorner-декали).
            int dxi = x + dx, dyi = y + dy;
            bool diagonalInsideBounds = dxi >= room.X && dxi < room.X + room.Width &&
                                         dyi >= room.Y && dyi < room.Y + room.Height;
            if (!diagonalInsideBounds) continue; // естественная внешняя граница — не пинч

            return room.WallProto; // одной подходящей диагонали (реально вырезанной) достаточно
        }

        return null;
    }
    
        public TileGrid BuildFromRooms(Grid grid, TileGrid? existingGrid = null)
    {
        var tileGrid = existingGrid ?? new TileGrid(grid.Uid, grid.Name);
        tileGrid.Clear();
        tileGrid.GridUid = grid.Uid;
        tileGrid.GridName = grid.Name;
        tileGrid.Position = grid.Position;
        tileGrid.Color = grid.Color;
        tileGrid.IsVisible = grid.IsVisible;

        var allRooms = grid.Rooms;
        
        var doorPositions = new HashSet<(int x, int y)>();
        foreach (var room in allRooms)
        {
            foreach (var door in room.Doors)
            {
                doorPositions.Add((door.X, door.Y));
            }
        }

        var passagePositions = new HashSet<(int x, int y)>();
        foreach (var room in allRooms)
        {
            foreach (var passage in room.Passages)
            {
                passagePositions.Add((passage.X, passage.Y));
            }
        }

        var windowPositions = new HashSet<(int x, int y)>();
        foreach (var room in allRooms)
        {
            foreach (var window in room.Windows)
            {
                windowPositions.Add((window.X, window.Y));
            }
        }

        // Клетки, где стена убрана инструментом «Убрать стену»: тайл-стена не
        // строится (как у прохода), но DecalPatternBuilder продолжает считать
        // такую клетку стеной. В WallHidden входит пол комнаты со стадии 1.
        var hiddenWallPositions = new HashSet<(int x, int y)>();
        foreach (var room in allRooms)
        {
            foreach (var hidden in room.HiddenWalls)
            {
                hiddenWallPositions.Add((hidden.X, hidden.Y));
            }
        }


        // 1. ПОЛ (тайлы) - под всеми занятыми клетками комнаты, кроме вырезанных
        foreach (var room in allRooms)
        {
            int roomUid = room.GetHashCode();

            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (room.RemovedCells.Contains((x, y))) continue;
                    tileGrid.SetTile(x, y, TileContent.Floor, room.FloorProto, room.RoomType, roomUid);
                }
            }
        }

        // 2. СТЕНЫ (тайлы для рендера) - на границах, кроме дверей



        // комнаты (внешний периметр + края внутренних выемок после RoomSubtractor),
        // а не только по внешнему прямоугольнику — так вырез формирует внутренний
        // угол, а не расщепляет комнату
foreach (var room in allRooms)
        {
            int roomUid = room.GetHashCode();

            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (room.RemovedCells.Contains((x, y))) continue;
                    if (doorPositions.Contains((x, y))) continue;
                    if (passagePositions.Contains((x, y))) continue;
                    if (hiddenWallPositions.Contains((x, y))) continue;
                    if (windowPositions.Contains((x, y))) continue;

                    // ОБЩАЯ СТЕНА: если ВСЕ открытые стороны клетки уже "закрыты"
                    // стенами ДРУГИХ комнат в соседних клетках, свою стену не ставим —
                    // впритык-комнаты делят одну физическую стену. Это позволяет
                    // создавать комнату вплотную к другой, не расширяя её в соседа
                    // и не получая двойной стены. Если хоть одна сторона выходит на
                    // пустоту — стена кладётся как обычно (чтобы не терять углы/края).
                    bool hasAnyBoundary = false;
                    bool allBoundariesCovered = true;
                    foreach (var (dx, dy) in _wallDirs)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (room.Contains(nx, ny)) continue; // внутренняя сторона
                        hasAnyBoundary = true;
                        var nTile = tileGrid.GetTile(nx, ny);
                        bool covered = nTile != null && nTile.Content == TileContent.Wall && nTile.RoomUid != roomUid;
                        if (!covered) allBoundariesCovered = false;
                    }
                    if (!hasAnyBoundary) continue;                       // не граница вообще
                    if (allBoundariesCovered) continue;                  // общая стена — не дублируем

                    string? wall = GetBoundaryWallProto(room, x, y);
                    if (wall != null)
                    {
                        // «Наружная стена»: если на слое задан прототип наружной стены
                        // (настройки проекта), и данная клетка граничит с полностью
                        // пустым тайлом (нет ни пола, ни стены, ни другой комнаты;
                        // сущности из левого меню тайл «не занимают») — используем его
                        // вместо обычной стены комнаты.
                        if (grid.ReplaceExteriorWalls &&
                            !string.IsNullOrEmpty(grid.ExteriorWallProto) &&
                            FacesEmptyTile(room, allRooms, tileGrid, x, y))
                        {
                            wall = grid.ExteriorWallProto;
                        }
                        tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                    }
                }
            }
        }

        // 2б. ДИАГОНАЛЬНЫЕ ВНУТРЕННИЕ УГЛЫ — заполняем "пинч"-клетки после вычитания,
        // иначе на повороте выемки стена истончается до 0 по диагонали (см. комментарий
        // у GetConcaveCornerWallProto). Толщина стены на таких углах становится 2 вместо 0.
        foreach (var room in allRooms)
        {
            int roomUid = room.GetHashCode();

            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (room.RemovedCells.Contains((x, y))) continue;
                    if (doorPositions.Contains((x, y))) continue;
                    if (passagePositions.Contains((x, y))) continue;
                    if (hiddenWallPositions.Contains((x, y))) continue;
                    if (windowPositions.Contains((x, y))) continue;

                    var existing = tileGrid.GetTile(x, y);
                    if (existing != null && existing.Content == TileContent.Wall) continue; // уже стена из прямого прохода
                    string? wall = GetConcaveCornerWallProto(room, x, y);
                    if (wall != null)
                        tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }
        }



        // 2в. ОБЩАЯ СТЕНА — по приоритету, строго в пределах одного тайла от
        // территории комнаты: клетка стены принимает прототип соседней комнаты,
        // если та граничит с клеткой ПОЛОМ (обычная общая стена), либо если эта
        // клетка вообще входит в территорию обеих комнат (общий угловой тайл
        // двух комнат впритык — его записывает та комната, что обрабатывается
        // последней). Ориентироваться на соседнюю стену нельзя: клетка соседней
        // комнаты на 1 тайл в стороне уже не принадлежит приоритетной комнате,
        // и её укреплённая стена растеклась бы за конец её территории.
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall).ToList())
        {
            string ownWall = tile.ProtoId ?? "WallSolid";
            string bestWall = ownWall;

            foreach (var (dx, dy) in _wallDirs)
            {
                var neighborTile = tileGrid.GetTile(tile.X + dx, tile.Y + dy);
                if (neighborTile == null || neighborTile.Content != TileContent.Floor) continue;
                if (doorPositions.Contains((neighborTile.X, neighborTile.Y))) continue;
                if (passagePositions.Contains((neighborTile.X, neighborTile.Y))) continue;
                if (hiddenWallPositions.Contains((neighborTile.X, neighborTile.Y))) continue;
                if (windowPositions.Contains((neighborTile.X, neighborTile.Y))) continue;

                var neighborRoom = allRooms.FirstOrDefault(r => r.GetHashCode() == neighborTile.RoomUid)
                                   ?? GetRoomAt(allRooms, neighborTile.X, neighborTile.Y);
                if (neighborRoom == null || neighborRoom.GetHashCode() == tile.RoomUid) continue;

                bestWall = BestWall(bestWall, neighborRoom.WallProto);
            }

            // Клетка-стена, входящая в территорию нескольких комнат (общий
            // угловой тайл) — тип выбирается по приоритету среди ВСЕХ таких
            // комнат, независимо от того, какая записала клетку последней.
            foreach (var owner in allRooms)
            {
                if (owner.GetHashCode() == tile.RoomUid) continue;
                if (!owner.Contains(tile.X, tile.Y)) continue;

                bestWall = BestWall(bestWall, owner.WallProto);
            }

            if (bestWall != ownWall)
                tile.ProtoId = bestWall;
        }



        // 3. ДВЕРИ (привязанные к комнатам)
        foreach (var room in allRooms)
        {
            foreach (var door in room.Doors)
            {
                ApplyDoorTile(tileGrid, allRooms, door, room.RoomType);
            }
        }



        // 3б. "СВОБОДНЫЕ" ДВЕРИ (поставлены вне комнат, при снятом магните)
        foreach (var looseDoor in grid.LooseDoors)
        {
            ApplyDoorTile(tileGrid, allRooms, looseDoor, null);
        }

        // 4. РУЧНЫЕ ТАЙЛЫ (переопределение пола в конкретных клетках, вставленные через PlacePrototype)
        foreach (var manualTile in grid.Tiles)
        {
            // Дверь не затираем: ручной тайл становится полом ПОД ней
            var tileUnder = tileGrid.GetTile(manualTile.X, manualTile.Y);
            if (tileUnder != null && tileUnder.Content == TileContent.Door)
            {
                tileUnder.HasFloorUnder = true;
                tileUnder.FloorProtoUnder = manualTile.Proto;
                continue;
            }

            tileGrid.SetTile(manualTile.X, manualTile.Y, TileContent.Floor, manualTile.Proto, null, -1);
        }

        return tileGrid;
    }



private string? FindFloorProtoForDoor(List<Room> allRooms, int x, int y)
{
    // Сначала комната, в чей прямоугольник попадает клетка (включая вырезанные клетки)
    var own = allRooms.LastOrDefault(r =>
        x >= r.X && x < r.X + r.Width &&
        y >= r.Y && y < r.Y + r.Height);
    if (own != null) return own.FloorProto;

    // Иначе — пол любой соседней (по 4 сторонам) комнаты
    foreach (var (dx, dy) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
    {
        var neighbor = GetRoomAt(allRooms, x + dx, y + dy);
        if (neighbor != null) return neighbor.FloorProto;
    }

    return null;
}

private void ApplyDoorTile(TileGrid tileGrid, List<Room> allRooms, Door door, string? roomType)
{
    var existing = tileGrid.GetTile(door.X, door.Y);
    bool hasFloor;
    string? floorProto;

    if (existing != null && existing.Content == TileContent.Door)
    {
        // Дубликат двери в той же клетке — пол уже определён первой обработкой
        hasFloor = existing.HasFloorUnder;
        floorProto = existing.FloorProtoUnder;
    }
    else if (existing != null && existing.Content == TileContent.Floor)
    {
        hasFloor = true;
        floorProto = existing.ProtoId;
    }
    else
    {
        floorProto = FindFloorProtoForDoor(allRooms, door.X, door.Y);
        hasFloor = floorProto != null;
    }

    tileGrid.SetTile(door.X, door.Y, TileContent.Door, door.Proto, roomType, -1);

    var doorTile = tileGrid.GetTile(door.X, door.Y);
    if (doorTile != null)
    {
        doorTile.HasFloorUnder = hasFloor;
        doorTile.FloorProtoUnder = hasFloor ? floorProto : null;
    }
}



    public string GetBestWallAt(TileGrid tileGrid, int x, int y)
    {
        var tile = tileGrid.GetTile(x, y);
        if (tile == null || tile.Content != TileContent.Wall)
            return "WallSolid";

        // ProtoId уже содержит полностью разрешённую (с учётом соседних комнат/стен)
        // стену — она вычисляется один раз в BuildFromRooms, см. блок "2в"
        return tile.ProtoId ?? "WallSolid";
    }

    public void UpdateTileGrid(Grid grid, TileGrid tileGrid)
    {
        BuildFromRooms(grid, tileGrid);
    }
}