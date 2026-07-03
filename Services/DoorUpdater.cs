// Services/DoorUpdater.cs
using MapperIce.Models;

namespace MapperIce.Services;

public class DoorUpdater
{
    private readonly RoomTypeManager _roomTypeManager;

    public DoorUpdater(RoomTypeManager roomTypeManager)
    {
        _roomTypeManager = roomTypeManager;
    }

    public bool TryCreateDoor(Grid grid, int x, int y, string doorType, out Door? newDoor, bool snapToGrid = true)
    {
        newDoor = null;
        if (grid == null) return false;

        // Проверяем, есть ли уже дверь в этой позиции
        if (grid.Rooms.Any(r => r.Doors.Any(d => d.X == x && d.Y == y)))
        {
            return false;
        }

        if (snapToGrid)
        {
            var room = grid.Rooms.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width &&
                y >= r.Y && y < r.Y + r.Height);

            if (room == null) return false;

            bool isOnEdge = x == room.X || x == room.X + room.Width - 1 ||
                           y == room.Y || y == room.Y + room.Height - 1;

            if (!isOnEdge) return false;
        }

        // Создаем дверь
        newDoor = new Door
        {
            X = x,
            Y = y,
            Proto = doorType
        };

        var targetRoom = grid.Rooms.FirstOrDefault(r =>
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height);

        if (targetRoom != null)
        {
            targetRoom.Doors.Add(newDoor);
            
            // === ДОБАВЛЯЕМ ПОЖАРНЫЙ ШЛЮЗ ===
            AddFirelock(grid, x, y, doorType);
            
            return true;
        }

        return false;
    }

    private void AddFirelock(Grid grid, int x, int y, string doorType)
    {
        if (grid == null) return;

        if (grid.Entities.OfType<FirelockEntity>().Any(e => (int)e.X == x && (int)e.Y == y))
            return;

        string firelockType = doorType.Contains("Glass") ? "FirelockGlass" : "Firelock";

        var firelock = new FirelockEntity
        {
            X = x,
            Y = y,
            IsGlass = doorType.Contains("Glass"),
            Proto = firelockType
        };

        grid.Entities.Add(firelock);
    }

    public bool TryRemoveDoor(Grid grid, int x, int y)
    {
        if (grid == null) return false;

        foreach (var room in grid.Rooms)
        {
            var door = room.Doors.FirstOrDefault(d => d.X == x && d.Y == y);
            if (door != null)
            {
                room.Doors.Remove(door);
                
                // Удаляем пожарный шлюз
                var firelock = grid.Entities.OfType<FirelockEntity>()
                    .FirstOrDefault(f => (int)f.X == x && (int)f.Y == y);
                if (firelock != null)
                {
                    grid.Entities.Remove(firelock);
                }
                
                return true;
            }
        }

        return false;
    }

    public void UpdateAllDoors(Grid grid)
    {
        var allDoors = grid.Rooms.SelectMany(r => r.Doors).ToList();
        
        // Очищаем старые шлюзы
        var oldFirelocks = grid.Entities.OfType<FirelockEntity>().ToList();
        foreach (var f in oldFirelocks)
        {
            grid.Entities.Remove(f);
        }

        // Добавляем шлюзы для всех дверей
        foreach (var door in allDoors)
        {
            AddFirelock(grid, door.X, door.Y, door.Proto);
        }
    }
}