// Services/TileBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

public class TileBuilder
{
    private readonly RoomTypeManager _roomTypeManager;
    private readonly DoorUpdater _doorUpdater;
    
    private static readonly Dictionary<string, int> _wallPriority = new()
    {
        { "WallSolid", 0 },
        { "WallReinforced", 1 },
    };

    public TileBuilder(RoomTypeManager roomTypeManager, DoorUpdater doorUpdater)
    {
        _roomTypeManager = roomTypeManager;
        _doorUpdater = doorUpdater;
    }

    private int GetPriority(string wall) => _wallPriority.GetValueOrDefault(wall, 2);
    private string BestWall(string a, string b) => GetPriority(a) >= GetPriority(b) ? a : b;
private Room? GetRoomAt(List<Room> rooms, int x, int y) => 
        rooms.FirstOrDefault(r => r.Contains(x, y));


private string? GetBoundaryWallProto(Room room, List<Room> allRooms, int x, int y)
    {
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        bool isBoundary = false;
        string wall = room.WallProto;

        foreach (var (dx, dy) in directions)
        {
            int nx = x + dx, ny = y + dy;
            if (room.Contains(nx, ny)) continue; // сосед - та же комната, внутренняя сторона

            isBoundary = true;

            var neighborRoom = GetRoomAt(allRooms, nx, ny);
            if (neighborRoom != null && neighborRoom != room)
                wall = BestWall(wall, neighborRoom.WallProto);
        }

        return isBoundary ? wall : null;
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
    private string? GetConcaveCornerWallProto(Room room, List<Room> allRooms, int x, int y)
    {
        var diagonals = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        foreach (var (dx, dy) in diagonals)
        {
            bool orthoOpen = room.Contains(x + dx, y) && room.Contains(x, y + dy);
            if (!orthoOpen) continue;

            bool diagonalForeign = !room.Contains(x + dx, y + dy);
            if (!diagonalForeign) continue;

            string wall = room.WallProto;
            var neighborRoom = GetRoomAt(allRooms, x + dx, y + dy);
            if (neighborRoom != null && neighborRoom != room)
                wall = BestWall(wall, neighborRoom.WallProto);

            return wall; // одной подходящей диагонали достаточно
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

                    string? wall = GetBoundaryWallProto(room, allRooms, x, y);
                    if (wall != null)
                        tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
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

                    var existing = tileGrid.GetTile(x, y);
                    if (existing != null && existing.Content == TileContent.Wall) continue; // уже стена из прямого прохода

                    string? wall = GetConcaveCornerWallProto(room, allRooms, x, y);
                    if (wall != null)
                        tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }
        }


        // 3. ДВЕРИ (привязанные к комнатам)
        foreach (var room in allRooms)
        {
            foreach (var door in room.Doors)
            {
                var existingTile = tileGrid.GetTile(door.X, door.Y);
                bool hasFloor = existingTile != null && existingTile.Content == TileContent.Floor;
                
                tileGrid.SetTile(door.X, door.Y, TileContent.Door, door.Proto, room.RoomType, -1);
                
                var doorTile = tileGrid.GetTile(door.X, door.Y);
                if (doorTile != null)
                {
                    doorTile.HasFloorUnder = hasFloor;
                    if (hasFloor && existingTile != null)
                    {
                        doorTile.FloorProtoUnder = existingTile.ProtoId;
                    }
                }
            }
        }

        // 3б. "СВОБОДНЫЕ" ДВЕРИ (поставлены вне комнат, при снятом магните)
        foreach (var looseDoor in grid.LooseDoors)
        {
            var existingTile = tileGrid.GetTile(looseDoor.X, looseDoor.Y);
            bool hasFloor = existingTile != null && existingTile.Content == TileContent.Floor;

            tileGrid.SetTile(looseDoor.X, looseDoor.Y, TileContent.Door, looseDoor.Proto, null, -1);

            var doorTile = tileGrid.GetTile(looseDoor.X, looseDoor.Y);
            if (doorTile != null)
            {
                doorTile.HasFloorUnder = hasFloor;
                if (hasFloor && existingTile != null)
                {
                    doorTile.FloorProtoUnder = existingTile.ProtoId;
                }
            }
        }

        // 4. РУЧНЫЕ ТАЙЛЫ (переопределение пола в конкретных клетках, вставленные через PlacePrototype)
        foreach (var manualTile in grid.Tiles)
        {
            tileGrid.SetTile(manualTile.X, manualTile.Y, TileContent.Floor, manualTile.Proto, null, -1);
        }

        return tileGrid;
    }

    // Этот метод больше не используется, но оставляем для совместимости
    private string GetBestWallForExistingTile(TileGrid tileGrid, int x, int y)
    {
        var tile = tileGrid.GetTile(x, y);
        if (tile == null || tile.Content != TileContent.Wall)
            return "WallSolid";

        string currentWall = tile.ProtoId ?? "WallSolid";
        int currentPriority = GetPriority(currentWall);

        var neighbors = new[]
        {
            (x, y - 1),
            (x, y + 1),
            (x - 1, y),
            (x + 1, y)
        };

        string bestWall = currentWall;
        int bestPriority = currentPriority;

        foreach (var (nx, ny) in neighbors)
        {
            var neighbor = tileGrid.GetTile(nx, ny);
            if (neighbor != null && 
                neighbor.Content == TileContent.Wall && 
                neighbor.RoomUid != tile.RoomUid &&
                neighbor.RoomUid != -1)
            {
                string neighborWall = neighbor.ProtoId ?? "WallSolid";
                int neighborPriority = GetPriority(neighborWall);
                if (neighborPriority > bestPriority)
                {
                    bestPriority = neighborPriority;
                    bestWall = neighborWall;
                }
            }
        }

        return bestWall;
    }

    public string GetBestWallAt(TileGrid tileGrid, int x, int y)
    {
        var tile = tileGrid.GetTile(x, y);
        if (tile == null || tile.Content != TileContent.Wall)
            return "WallSolid";

        return GetBestWallForExistingTile(tileGrid, x, y);
    }

    public void UpdateTileGrid(Grid grid, TileGrid tileGrid)
    {
        BuildFromRooms(grid, tileGrid);
    }
}