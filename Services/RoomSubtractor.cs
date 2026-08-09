using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// "Вычитание" прямоугольной области из комнат. Комната НЕ расщепляется на
/// несколько Room-объектов — вырезанные клетки добавляются в Room.RemovedCells,
/// а прямоугольник комнаты подрезается с краёв (ShrinkBounds), если целая
/// крайняя строка/столбец вырезаны полностью целиком. Благодаря этому:
/// - вырезание целого крайнего столбца/строки даёт чистое сужение прямоугольника
///   (без расщепления и без фиктивной "дыры" во всю высоту);
/// - вырезание куска с краю или из глубины комнаты даёт настоящую L-образную
///   выемку с внутренним углом, комната остаётся одним объектом.
/// </summary>
public static class RoomSubtractor
{
    public static bool SubtractFromRoom(Room room, int cutX, int cutY, int cutW, int cutH)
    {
        int eRight = room.X + room.Width;
        int eBottom = room.Y + room.Height;
        int cRight = cutX + cutW;
        int cBottom = cutY + cutH;

        int iLeft = Math.Max(room.X, cutX);
        int iTop = Math.Max(room.Y, cutY);
        int iRight = Math.Min(eRight, cRight);
        int iBottom = Math.Min(eBottom, cBottom);

        if (iLeft >= iRight || iTop >= iBottom)
            return false; // пересечения нет

        for (int x = iLeft; x < iRight; x++)
            for (int y = iTop; y < iBottom; y++)
                room.RemovedCells.Add((x, y));

        ShrinkBounds(room);
        return true;
    }

    /// <summary>
    /// Применяет вычитание ко всем комнатам грида. Комнаты, полностью "съеденные"
    /// вычитанием, удаляются из грида. Двери, оказавшиеся вне новой формы комнаты,
    /// переносятся в LooseDoors — их дальше пересобирает DoorUpdater.RecalculateAllDoors.
    /// </summary>
    public static bool ApplyToGrid(Grid grid, int cutX, int cutY, int cutW, int cutH)
    {
        bool anyChanged = false;

        foreach (var room in grid.Rooms.ToList())
        {
            if (!SubtractFromRoom(room, cutX, cutY, cutW, cutH)) continue;

            anyChanged = true;

            var stranded = room.Doors.Where(d => !room.Contains(d.X, d.Y)).ToList();
            foreach (var door in stranded)
            {
                room.Doors.Remove(door);
                grid.LooseDoors.Add(door);
            }
        }

        grid.Rooms.RemoveAll(r => r.Width <= 0 || r.Height <= 0);

        return anyChanged;
    }

    private static void ShrinkBounds(Room room)
    {
        bool shrunk;
        do
        {
            shrunk = false;
            if (room.Width <= 0 || room.Height <= 0) return;

            if (IsColumnFullyRemoved(room, room.X))
            {
                RemoveColumnFromSet(room, room.X);
                room.X += 1;
                room.Width -= 1;
                shrunk = true;
                continue;
            }

            int rightCol = room.X + room.Width - 1;
            if (room.Width > 0 && IsColumnFullyRemoved(room, rightCol))
            {
                RemoveColumnFromSet(room, rightCol);
                room.Width -= 1;
                shrunk = true;
                continue;
            }

            if (IsRowFullyRemoved(room, room.Y))
            {
                RemoveRowFromSet(room, room.Y);
                room.Y += 1;
                room.Height -= 1;
                shrunk = true;
                continue;
            }

            int bottomRow = room.Y + room.Height - 1;
            if (room.Height > 0 && IsRowFullyRemoved(room, bottomRow))
            {
                RemoveRowFromSet(room, bottomRow);
                room.Height -= 1;
                shrunk = true;
                continue;
            }
        } while (shrunk && room.Width > 0 && room.Height > 0);

        if (room.Width <= 0 || room.Height <= 0)
        {
            room.Width = 0;
            room.Height = 0;
            room.RemovedCells.Clear();
        }
    }

    private static bool IsColumnFullyRemoved(Room room, int x)
    {
        for (int y = room.Y; y < room.Y + room.Height; y++)
            if (!room.RemovedCells.Contains((x, y)))
                return false;
        return true;
    }

    private static bool IsRowFullyRemoved(Room room, int y)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            if (!room.RemovedCells.Contains((x, y)))
                return false;
        return true;
    }

    private static void RemoveColumnFromSet(Room room, int x)
    {
        for (int y = room.Y; y < room.Y + room.Height; y++)
            room.RemovedCells.Remove((x, y));
    }

    private static void RemoveRowFromSet(Room room, int y)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            room.RemovedCells.Remove((x, y));
    }
}