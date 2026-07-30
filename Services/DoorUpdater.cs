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

        // Уже есть дверь в этой позиции (в комнате или "свободная")
        if (grid.Rooms.Any(r => r.Doors.Any(d => d.X == x && d.Y == y)) ||
            grid.LooseDoors.Any(d => d.X == x && d.Y == y))
        {
            return false;
        }

        bool wantsGlass = doorType.Contains("Glass");

        // Комната с наивысшим приоритетом среди тех, что содержат клетку.
        // null, если клетка вне всех комнат — допустимо только при снятом магните.
        Room? targetRoom = GetBestRoomAt(grid, x, y);

        if (snapToGrid)
        {
            if (targetRoom == null) return false;

            bool isOnEdge = x == targetRoom.X || x == targetRoom.X + targetRoom.Width - 1 ||
                           y == targetRoom.Y || y == targetRoom.Y + targetRoom.Height - 1;

            if (!isOnEdge) return false;
        }

        // Прототип берём из настроек приоритетной комнаты, а не жёстко заданный извне —
        // так дверь визуально соответствует типу той комнаты, что "победила"
        string finalProto = doorType;
        if (targetRoom != null)
        {
            string roomProto = wantsGlass ? targetRoom.GlassDoorProto : targetRoom.DoorProto;
            if (!string.IsNullOrEmpty(roomProto))
                finalProto = roomProto;
        }

        newDoor = new Door { X = x, Y = y, Proto = finalProto };

        if (targetRoom != null)
            targetRoom.Doors.Add(newDoor);
        else
            grid.LooseDoors.Add(newDoor); // дверь вне комнат (снят магнит)

        AddFirelock(grid, x, y, finalProto);
        return true;
    }

    /// <summary>
    /// Комната с наивысшим приоритетом среди тех, что содержат клетку (x, y).
    /// При равном приоритете побеждает последняя добавленная (обычно "верхняя" по отрисовке).
    /// </summary>
    private Room? GetBestRoomAt(Grid grid, int x, int y)
    {
        var candidates = grid.Rooms
            .Where(r => x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height)
            .ToList();

        if (candidates.Count == 0) return null;

        int maxPriority = candidates.Max(r => r.Priority);
        return candidates.Last(r => r.Priority == maxPriority);
    }

    private void AddFirelock(Grid grid, int x, int y, string doorType)
    {
        if (grid == null) return;
        if (grid.Entities.OfType<FirelockEntity>().Any(e => (int)e.X == x && (int)e.Y == y))
            return;

        string firelockType = doorType.Contains("Glass") ? "FirelockGlass" : "Firelock";

        grid.Entities.Add(new FirelockEntity
        {
            X = x,
            Y = y,
            IsGlass = doorType.Contains("Glass"),
            Proto = firelockType
        });
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
                RemoveFirelockAt(grid, x, y);
                return true;
            }
        }

        var looseDoor = grid.LooseDoors.FirstOrDefault(d => d.X == x && d.Y == y);
        if (looseDoor != null)
        {
            grid.LooseDoors.Remove(looseDoor);
            RemoveFirelockAt(grid, x, y);
            return true;
        }

        return false;
    }

    private void RemoveFirelockAt(Grid grid, int x, int y)
    {
        var firelock = grid.Entities.OfType<FirelockEntity>()
            .FirstOrDefault(f => (int)f.X == x && (int)f.Y == y);
        if (firelock != null)
            grid.Entities.Remove(firelock);
    }

    public void UpdateAllDoors(Grid grid)
    {
        var allDoors = grid.Rooms.SelectMany(r => r.Doors).Concat(grid.LooseDoors).ToList();

        var oldFirelocks = grid.Entities.OfType<FirelockEntity>().ToList();
        foreach (var f in oldFirelocks)
            grid.Entities.Remove(f);

        foreach (var door in allDoors)
            AddFirelock(grid, door.X, door.Y, door.Proto);
    }

    private List<(int x, int y)> GetRoomPerimeterCells(Room room)
    {
        var cells = new HashSet<(int x, int y)>();

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            cells.Add((x, room.Y));
            cells.Add((x, room.Y + room.Height - 1));
        }
        for (int y = room.Y; y < room.Y + room.Height; y++)
        {
            cells.Add((room.X, y));
            cells.Add((room.X + room.Width - 1, y));
        }

        return cells.ToList();
    }

    private (Door? door, Room? owner) FindDoorAt(Grid grid, int x, int y)
    {
        foreach (var room in grid.Rooms)
        {
            var d = room.Doors.FirstOrDefault(dd => dd.X == x && dd.Y == y);
            if (d != null) return (d, room);
        }

        var loose = grid.LooseDoors.FirstOrDefault(dd => dd.X == x && dd.Y == y);
        if (loose != null) return (loose, null);

        return (null, null);
    }

    /// <summary>
    /// Вызывать при установке (или изменении) конкретной комнаты. Снимает и заново
    /// ставит на то же место все двери, оказавшиеся на территории (периметре) этой
    /// комнаты, привязывая каждую к тому владельцу, что сейчас приоритетнее на этой клетке.
    /// </summary>
    public void RecalculateDoorsInRoom(Grid grid, Room room)
    {
        if (grid == null || room == null) return;

        bool anyChanged = false;

        foreach (var (x, y) in GetRoomPerimeterCells(room))
        {
            var (existingDoor, currentOwner) = FindDoorAt(grid, x, y);
            if (existingDoor == null) continue; // двери тут нет — нечего пересчитывать

            // Снимаем дверь с текущего места
            if (currentOwner != null)
                currentOwner.Doors.Remove(existingDoor);
            else
                grid.LooseDoors.Remove(existingDoor);

            // Ставим её на то же место заново, уже к правильному владельцу
            bool isGlass = existingDoor.Proto.Contains("Glass");
            var bestRoom = GetBestRoomAt(grid, x, y);

            if (bestRoom != null)
            {
                string newProto = isGlass ? bestRoom.GlassDoorProto : bestRoom.DoorProto;
                if (!string.IsNullOrEmpty(newProto))
                    existingDoor.Proto = newProto;

                bestRoom.Doors.Add(existingDoor);
            }
            else
            {
                grid.LooseDoors.Add(existingDoor);
            }

            anyChanged = true;
        }

        if (anyChanged)
            UpdateAllDoors(grid); // прототип мог смениться (обычная/стекло) — пересоздаём шлюзы
    }

    /// <summary>
    /// Полный пересчёт всех дверей грида. Нужен для случаев, затрагивающих сразу
    /// несколько зон (удаление комнаты, удаление области, перемещение), где заранее
    /// не известно, территория каких комнат изменилась.
    /// </summary>
    public void RecalculateAllDoors(Grid grid)
    {
        if (grid == null) return;

        var doorsByPosition = new Dictionary<(int x, int y), Door>();

        foreach (var room in grid.Rooms)
            foreach (var door in room.Doors)
                doorsByPosition[(door.X, door.Y)] = door;

        foreach (var door in grid.LooseDoors)
            doorsByPosition[(door.X, door.Y)] = door;

        foreach (var room in grid.Rooms)
            room.Doors.Clear();
        grid.LooseDoors.Clear();

        foreach (var door in doorsByPosition.Values)
        {
            bool isGlass = door.Proto.Contains("Glass");
            var bestRoom = GetBestRoomAt(grid, door.X, door.Y);

            if (bestRoom != null)
            {
                string newProto = isGlass ? bestRoom.GlassDoorProto : bestRoom.DoorProto;
                if (!string.IsNullOrEmpty(newProto))
                    door.Proto = newProto;

                bestRoom.Doors.Add(door);
            }
            else
            {
                grid.LooseDoors.Add(door);
            }
        }

        UpdateAllDoors(grid);
    }






}