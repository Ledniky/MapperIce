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
        rooms.FirstOrDefault(r => x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

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

        // 1. ПОЛ (тайлы) - под всей комнатой
        foreach (var room in allRooms)
        {
            int roomUid = room.GetHashCode();

            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    tileGrid.SetTile(x, y, TileContent.Floor, room.FloorProto, room.RoomType, roomUid);
                }
            }
        }

        // 2. СТЕНЫ (тайлы для рендера) - на границах, кроме дверей
        foreach (var room in allRooms)
        {
            int roomUid = room.GetHashCode();

            // Верхняя стена
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                int y = room.Y;
                if (!doorPositions.Contains((x, y)))
                {
                    var neighbor = GetRoomAt(allRooms, x, y - 1);
                    string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                    tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }

            // Нижняя стена
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                int y = room.Y + room.Height - 1;
                if (!doorPositions.Contains((x, y)))
                {
                    var neighbor = GetRoomAt(allRooms, x, y + 1);
                    string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                    tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }

            // Левая стена (без углов)
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
            {
                int x = room.X;
                if (!doorPositions.Contains((x, y)))
                {
                    var neighbor = GetRoomAt(allRooms, x - 1, y);
                    string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                    tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }

            // Правая стена (без углов)
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
            {
                int x = room.X + room.Width - 1;
                if (!doorPositions.Contains((x, y)))
                {
                    var neighbor = GetRoomAt(allRooms, x + 1, y);
                    string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                    tileGrid.SetTile(x, y, TileContent.Wall, wall, room.RoomType, roomUid);
                }
            }
        }

        // Применяем приоритеты стен
        var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall).ToList();
        foreach (var tile in wallTiles)
        {
            string bestWall = GetBestWallForExistingTile(tileGrid, tile.X, tile.Y);
            tile.ProtoId = bestWall;
        }

        // 3. ДВЕРИ
        foreach (var room in allRooms)
        {
            foreach (var door in room.Doors)
            {
                // Проверяем, был ли здесь пол
                var existingTile = tileGrid.GetTile(door.X, door.Y);
                bool hasFloor = existingTile != null && existingTile.Content == TileContent.Floor;
                
                tileGrid.SetTile(door.X, door.Y, TileContent.Door, door.Proto, room.RoomType, -1);
                
                // Сохраняем информацию о поле под дверью
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

        return tileGrid;
    }

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