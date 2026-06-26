namespace MapperIce.Models;

/// <summary>
/// Хранит все комнаты и управляет ими
/// </summary>
public class TileMap
{
    private List<Room> _rooms = new();

    public IReadOnlyList<Room> Rooms => _rooms;

    public void AddRoom(Room room)
    {
        _rooms.Add(room);
    }

    public void RemoveRoom(Room room)
    {
        _rooms.Remove(room);
    }

    public Room? GetRoomAt(int tileX, int tileY)
    {
        // Ищем с конца (чтобы найти верхнюю комнату при пересечении)
        for (int i = _rooms.Count - 1; i >= 0; i--)
        {
            var room = _rooms[i];
            if (tileX >= room.X && tileX < room.X + room.Width &&
                tileY >= room.Y && tileY < room.Y + room.Height)
            {
                return room;
            }
        }
        return null;
    }

    public void Clear()
    {
        _rooms.Clear();
    }
}