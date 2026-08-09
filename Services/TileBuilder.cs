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


    /// <summary>
    /// Стена — это тайл, который может принадлежать только одной из двух соседних
    /// комнат, иначе между соприкасающимися комнатами получается двойная толщина
    /// (каждая ставит свою стену впритык к чужой). На общей границе с другой
    /// комнатой стену ставит только сторона, "смотрящая" вправо или вниз
    /// (dx==1 или dy==1) — противоположная сторона стену не ставит, её пол
    /// просто доходит вплотную до чужой стены. На настоящей внешней границе
    /// (соседа-комнаты нет вообще) правило не действует — стена ставится всегда.
    /// </summary>
    private string? GetBoundaryWallProto(Room room, List<Room> allRooms, int x, int y)
    {
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        bool needsWall = false;
        string wall = room.WallProto;

        foreach (var (dx, dy) in directions)
        {
            int nx = x + dx, ny = y + dy;
            if (room.Contains(nx, ny)) continue; // сосед — та же комната, эта сторона внутренняя

            var neighborRoom = GetRoomAt(allRooms, nx, ny);

            if (neighborRoom == null)
            {
                // Настоящая внешняя граница — стена нужна всегда
                needsWall = true;
            }
            else if (dx == 1 || dy == 1)
            {
                // Общая граница с другой комнатой — стену ставит только эта сторона
                needsWall = true;
                wall = BestWall(wall, neighborRoom.WallProto);
            }
            // dx == -1 или dy == -1 с соседней комнатой — стену не ставим,
            // здесь остаётся пол (уже проставлен отдельным проходом по полу)
        }

        return needsWall ? wall : null;
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