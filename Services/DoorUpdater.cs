using MapperIce.Models;

namespace MapperIce.Services;

public class DoorUpdater
{
    private readonly RoomTypeManager _roomTypeManager;

    public DoorUpdater(RoomTypeManager roomTypeManager)
    {
        _roomTypeManager = roomTypeManager;
    }

    /// <summary>
    /// Находит комнату с максимальным приоритетом в указанной точке
    /// </summary>
    public Room? GetTopPriorityRoomAtPoint(Grid grid, int tileX, int tileY)
    {
        var roomsAtPoint = grid.Rooms
            .Where(r => tileX >= r.X && tileX < r.X + r.Width &&
                        tileY >= r.Y && tileY < r.Y + r.Height)
            .ToList();

        if (roomsAtPoint.Count == 0) return null;

        return roomsAtPoint
            .OrderByDescending(r => _roomTypeManager.GetPriorityForType(r.RoomType))
            .First();
    }

    /// <summary>
    /// Проверяет, находится ли точка на границе комнаты
    /// </summary>
    public bool IsOnRoomBorder(Room room, int tileX, int tileY)
    {
        return tileX == room.X ||
               tileX == room.X + room.Width - 1 ||
               tileY == room.Y ||
               tileY == room.Y + room.Height - 1;
    }

    /// <summary>
    /// Получает прототип двери для комнаты
    /// </summary>
    public string GetDoorProto(Room room, string doorType)
    {
        return doorType == "Airlock"
            ? room.DoorProto ?? "Airlock"
            : room.GlassDoorProto ?? "AirlockGlass";
    }

    /// <summary>
    /// Создаёт дверь в указанной точке от комнаты с максимальным приоритетом
    /// </summary>
    public bool TryCreateDoor(Grid grid, int tileX, int tileY, string doorType, out Door? createdDoor)
    {
        createdDoor = null;

        var topRoom = GetTopPriorityRoomAtPoint(grid, tileX, tileY);
        if (topRoom == null) return false;

        if (!IsOnRoomBorder(topRoom, tileX, tileY)) return false;

        if (topRoom.Doors.Any(d => d.X == tileX && d.Y == tileY)) return false;

        string doorProto = GetDoorProto(topRoom, doorType);

        createdDoor = new Door
        {
            X = tileX,
            Y = tileY,
            Proto = doorProto
        };

        topRoom.Doors.Add(createdDoor);
        return true;
    }

    /// <summary>
    /// Обновляет двери на границах комнаты - переносит двери в комнату с максимальным приоритетом
    /// </summary>

    public void UpdateDoorsOnRoomBoundary(Room room, Grid grid)
    {
        // Проверяем все двери в гриде
        var allDoors = grid.Rooms
            .SelectMany(r => r.Doors.Select(d => new { Room = r, Door = d }))
            .ToList();

        foreach (var item in allDoors)
        {
            // Находим все комнаты в точке двери
            var roomsAtPoint = grid.Rooms
                .Where(r => item.Door.X >= r.X && item.Door.X < r.X + r.Width &&
                            item.Door.Y >= r.Y && item.Door.Y < r.Y + r.Height)
                .ToList();

            if (roomsAtPoint.Count == 0) continue;

            // Находим комнату с максимальным приоритетом
            var topRoom = roomsAtPoint
                .OrderByDescending(r => _roomTypeManager.GetPriorityForType(r.RoomType))
                .First();

            // Если дверь уже принадлежит приоритетной комнате - пропускаем
            if (topRoom == item.Room) continue;

            // Проверяем, что точка находится на границе приоритетной комнаты
            if (!IsOnRoomBorder(topRoom, item.Door.X, item.Door.Y)) continue;

            // Удаляем дверь из текущей комнаты
            item.Room.Doors.Remove(item.Door);

            // Добавляем дверь в приоритетную комнату (если её там нет)
            if (!topRoom.Doors.Any(d => d.X == item.Door.X && d.Y == item.Door.Y))
            {
                string doorType = item.Door.Proto.Contains("Glass") ? "AirlockGlass" : "Airlock";
                string doorProto = GetDoorProto(topRoom, doorType);

                topRoom.Doors.Add(new Door
                {
                    X = item.Door.X,
                    Y = item.Door.Y,
                    Proto = doorProto
                });
            }
        }
    }

    /// <summary>
    /// Обновляет все двери на всём гриде
    /// </summary>
    public void UpdateAllDoors(Grid grid)
    {
        var allDoors = grid.Rooms
            .SelectMany(r => r.Doors.Select(d => new { Room = r, Door = d }))
            .ToList();

        foreach (var item in allDoors)
        {
            // Находим все комнаты в точке двери
            var roomsAtPoint = grid.Rooms
                .Where(r => item.Door.X >= r.X && item.Door.X < r.X + r.Width &&
                            item.Door.Y >= r.Y && item.Door.Y < r.Y + r.Height)
                .ToList();

            if (roomsAtPoint.Count == 0)
            {
                // Если дверь не принадлежит ни одной комнате - удаляем её
                item.Room.Doors.Remove(item.Door);
                continue;
            }

            // Находим комнату с максимальным приоритетом
            var topRoom = roomsAtPoint
                .OrderByDescending(r => _roomTypeManager.GetPriorityForType(r.RoomType))
                .First();

            // Если дверь уже принадлежит приоритетной комнате - пропускаем
            if (topRoom == item.Room) continue;

            // Проверяем, что точка находится на границе приоритетной комнаты
            if (!IsOnRoomBorder(topRoom, item.Door.X, item.Door.Y))
            {
                // Если точка не на границе - удаляем дверь
                item.Room.Doors.Remove(item.Door);
                continue;
            }

            // Удаляем дверь из текущей комнаты
            item.Room.Doors.Remove(item.Door);

            // Добавляем дверь в приоритетную комнату (если её там нет)
            if (!topRoom.Doors.Any(d => d.X == item.Door.X && d.Y == item.Door.Y))
            {
                string doorType = item.Door.Proto.Contains("Glass") ? "AirlockGlass" : "Airlock";
                string doorProto = GetDoorProto(topRoom, doorType);

                topRoom.Doors.Add(new Door
                {
                    X = item.Door.X,
                    Y = item.Door.Y,
                    Proto = doorProto
                });
            }
        }
    }

    /// <summary>
    /// Удаляет дверь в указанной точке
    /// </summary>
    public bool TryRemoveDoor(Grid grid, int tileX, int tileY)
    {
        foreach (var room in grid.Rooms)
        {
            var door = room.Doors.FirstOrDefault(d => d.X == tileX && d.Y == tileY);
            if (door != null)
            {
                room.Doors.Remove(door);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Проверяет, есть ли дверь в указанной точке
    /// </summary>
    public bool HasDoorAtPoint(Grid grid, int tileX, int tileY)
    {
        return grid.Rooms.Any(r => r.Doors.Any(d => d.X == tileX && d.Y == tileY));
    }
}