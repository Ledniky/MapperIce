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

        // Если магнит включен - проверяем, что рядом есть стена
        if (snapToGrid)
        {
            var room = grid.Rooms.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width &&
                y >= r.Y && y < r.Y + r.Height);

            if (room == null) return false;

            // Проверяем, что позиция на границе комнаты
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

        // Находим комнату и добавляем дверь
        var targetRoom = grid.Rooms.FirstOrDefault(r =>
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height);

        if (targetRoom != null)
        {
            targetRoom.Doors.Add(newDoor);
            return true;
        }

        return false;
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
                return true;
            }
        }

        return false;
    }

    public void UpdateAllDoors(Grid grid)
    {
        // Пересоздаем все двери
        var allDoors = grid.Rooms.SelectMany(r => r.Doors).ToList();
        foreach (var door in allDoors)
        {
            // Проверяем, что дверь все еще на границе комнаты
            var room = grid.Rooms.FirstOrDefault(r =>
                door.X >= r.X && door.X < r.X + r.Width &&
                door.Y >= r.Y && door.Y < r.Y + r.Height);

            if (room == null)
            {
                // Если комнаты нет - удаляем дверь
                foreach (var r in grid.Rooms)
                {
                    r.Doors.Remove(door);
                }
            }
        }
    }
}