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
    /// Если newRoom впритык (без нахлёста и без зазора) касается уже существующей
    /// комнаты по какой-либо стороне, "вдвигает" newRoom на 1 тайл в сторону этой
    /// комнаты — создавая намеренный нахлёст в 1 тайл, на котором держится общая
    /// стена (обе комнаты пишут Wall в одну и ту же физическую клетку TileGrid).
    /// Без этого две просто соприкасающиеся комнаты получают КАЖДАЯ свою стену
    /// в СВОЕЙ соседней клетке — визуально это выглядит как "двойная стена".
    /// Вызывать ДО RoomSubtractor.ApplyToGrid и ДО добавления newRoom в grid.Rooms.
    /// </summary>
    public static void SnapAdjacentOverlap(Grid grid, Room newRoom)
    {
        bool expandLeft = false, expandRight = false, expandUp = false, expandDown = false;

        foreach (var room in grid.Rooms)
        {
            bool yTouch = newRoom.Y < room.Y + room.Height && newRoom.Y + newRoom.Height > room.Y;
            bool xTouch = newRoom.X < room.X + room.Width && newRoom.X + newRoom.Width > room.X;

            // room слева от newRoom, впритык, без нахлёста
            if (yTouch && room.X + room.Width == newRoom.X)
                expandLeft = true;

            // room справа от newRoom, впритык
            if (yTouch && newRoom.X + newRoom.Width == room.X)
                expandRight = true;

            // room сверху от newRoom, впритык
            if (xTouch && room.Y + room.Height == newRoom.Y)
                expandUp = true;

            // room снизу от newRoom, впритык
            if (xTouch && newRoom.Y + newRoom.Height == room.Y)
                expandDown = true;
        }

        if (expandLeft) { newRoom.X -= 1; newRoom.Width += 1; }
        if (expandRight) { newRoom.Width += 1; }
        if (expandUp) { newRoom.Y -= 1; newRoom.Height += 1; }
        if (expandDown) { newRoom.Height += 1; }
    }

    

    /// <summary>
    /// Полная обработка размещения новой комнаты поверх существующих: сначала
    /// SnapAdjacentOverlap создаёт нахлёст в 1 тайл там, где комнаты просто
    /// впритык касаются (без пересечения), затем из существующих комнат
    /// вырезается только ВНУТРЕННЯЯ часть footprint'а newRoom — отступив на
    /// 1 тайл с каждой стороны. Внешнее кольцо в 1 тайл остаётся нетронутым
    /// у старой комнаты, и на этом кольце обе комнаты (старая и новая) пишут
    /// Wall в одну и ту же физическую клетку TileGrid — общая стена, а не
    /// две соседние. Вырезание полного прямоугольника (без отступа) оставляло
    /// старую комнату без граничной клетки и создавало двойную стену.
    /// </summary>
    public static void ApplyForNewRoom(Grid grid, Room newRoom)
    {
        SnapAdjacentOverlap(grid, newRoom);

        int cutX = newRoom.X;
        int cutW = newRoom.Width;
        if (newRoom.Width > 2)
        {
            cutX += 1;
            cutW -= 2;
        }
        else
        {
            // Слишком узкая комната (1-2 тайла) — отступать некуда, режем как есть.
            // Риск двойной стены остаётся только для таких тонких комнат
        }

        int cutY = newRoom.Y;
        int cutH = newRoom.Height;
        if (newRoom.Height > 2)
        {
            cutY += 1;
            cutH -= 2;
        }

        if (cutW <= 0 || cutH <= 0) return; // отступать некуда по обеим осям — ничего не режем

        ApplyToGrid(grid, cutX, cutY, cutW, cutH);
    }

    /// <summary>
    /// Применяет вычитание ко всем комнатам грида. Комнаты, полностью "съеденные"
    /// вычитанием, удаляются из грида. Двери, оказавшиеся вне новой формы комнаты,
    /// переносятся в LooseDoors — их дальше пересобирает DoorUpdater.RecalculateAllDoors.
    /// </summary>
    public static bool ApplyToGrid(Grid grid, int cutX, int cutY, int cutW, int cutH, int minOverlapDepth = 1)
    {
        bool anyChanged = false;

        foreach (var room in grid.Rooms.ToList())
        {
            int iLeft = Math.Max(room.X, cutX);
            int iTop = Math.Max(room.Y, cutY);
            int iRight = Math.Min(room.X + room.Width, cutX + cutW);
            int iBottom = Math.Min(room.Y + room.Height, cutY + cutH);

            int iw = iRight - iLeft;
            int ih = iBottom - iTop;

            if (iw < minOverlapDepth || ih < minOverlapDepth) continue;

            if (!SubtractFromRoom(room, cutX, cutY, cutW, cutH)) continue;

            anyChanged = true;

            var stranded = room.Doors.Where(d => !room.Contains(d.X, d.Y)).ToList();
            foreach (var door in stranded)
            {
                room.Doors.Remove(door);
                grid.LooseDoors.Add(door);
            }
        }

        // Удаляем полностью "съеденные" комнаты вместе с их декалями
        var roomsToPurge = grid.Rooms.Where(r => r.Width <= 0 || r.Height <= 0).ToList();
        foreach (var room in roomsToPurge)
        {
            var decalsToRemove = grid.Decals
                .Where(d => d.X >= room.X && d.X < room.X + room.Width &&
                            d.Y >= room.Y && d.Y < room.Y + room.Height)
                .ToList();
            foreach (var decal in decalsToRemove)
                grid.Decals.Remove(decal);

            grid.Rooms.Remove(room);
        }

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