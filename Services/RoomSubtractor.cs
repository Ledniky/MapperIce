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
        RemoveThinWallSlivers(room);
        return true;
    }


    /// <summary>
    /// Полная обработка размещения новой комнаты поверх существующих: из существующих
    /// комнат вырезается только ВНУТРЕННЯЯ часть footprint'а newRoom — отступив на
    /// 1 тайл с каждой стороны. Внешнее кольцо в 1 тайл остаётся нетронутым у старой
    /// комнаты — здесь проходит общая стена. Общая стена между комнатами впритык
    /// строится в BuildFromRooms через WallDedupe (комната не плодит свою стену там,
    /// где сосед уже держит чужую), поэтому новая комната не расширяется в соседа и
    /// не "уворовывает" у него 1 тайл. Вырезание полного прямоугольника (без отступа)
    /// оставляло старую комнату без граничной клетки и создавало двойную стену.
    /// </summary>
    public static void ApplyForNewRoom(Grid grid, Room newRoom)
    {
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

        // Удаляем полностью "съеденные" комнаты вместе со всем, что они привнесли
        // на карту: пометками проходов/окон на своих клетках (в том числе у
        // соседей по общей стене — иначе там навсегда остаётся проход), оконными
        // сущностями и декалями узора (см. RoomCleanup.Remove)
        var roomsToPurge = grid.Rooms.Where(r => r.Width <= 0 || r.Height <= 0).ToList();
        foreach (var room in roomsToPurge)
        {
            RoomCleanup.Remove(grid, room);
        }

        return anyChanged;
    }

    /// <summary>
    /// "Восстановление" прямоугольной области в комнате — обратная операция к Subtract.
    /// Клетки из заданного прямоугольника удаляются из RemovedCells (становятся снова
    /// частью комнаты), а прямоугольник комнаты расширяется (ExpandBounds), чтобы
    /// включить восстановленные клетки.
    /// </summary>
    public static bool RestoreFromRoom(Room room, int cutX, int cutY, int cutW, int cutH)
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

        bool anyRestored = false;
        for (int x = iLeft; x < iRight; x++)
            for (int y = iTop; y < iBottom; y++)
            {
                if (room.RemovedCells.Remove((x, y)))
                    anyRestored = true;
            }

        if (anyRestored)
            ExpandBounds(room);

        return anyRestored;
    }


        /// <summary>
    /// true, если конкретная клетка cell уже принадлежит ДРУГОЙ комнате грида
    /// (не room) — то есть занята и расширяться в неё нельзя. Проверяется по
    /// одной клетке, а не по всему отрезку — так соседняя комната, вклинившаяся
    /// только в часть границы (в том числе в крайнюю клетку отрезка), не
    /// блокирует расширение целиком, а лишь "выключает" именно эту клетку.
    /// </summary>
    public static bool IsCellBlocked(Grid grid, Room room, (int x, int y) cell)
    {
        var other = grid.Rooms.FirstOrDefault(r => r != room && r.Contains(cell.x, cell.y));
        if (other == null) return false; // свободно

        // Занято другой комнатой — но если это ЛЮБАЯ её граничная (стеновая)
        // клетка, открытая хоть с одной стороны (неважно, с какой именно —
        // это может быть боковая стена соседней комнаты, а не обязательно
        // смотрящая точно в сторону нашего расширения), разрешаем расширение
        // туда: обе комнаты разделят одну физическую клетку под общую стену
        // (та же общая стена, что строится между комнатами впритык).
        // Блокируем только настоящую внутреннюю территорию (пол) чужой
        // комнаты — клетку, у которой нет ни одной открытой стороны.
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        var diagonals = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        // Ортогональная открытая сторона — обычная граница/стена.
        bool otherIsBoundary = directions.Any(d => other.HasWallOnSide(cell.x, cell.y, d.Item1, d.Item2));

        // Внутренний угол (вогнутая выемка другой комнаты): у клетки НЕТ открытой
        // ортогональной стороны (с четырёх сторон внутри другой комнаты), но по
        // диагонали к ней примыкает клетка ВНЕ другой комнаты (вырез/чужая комната).
        // Такой тайл тоже физически стена — на нём стоит внутренний угол, и в него
        // можно расширяться (общая стена), а не блокировать расширение как во
        // внутренний пол. Проверяем оба примыкающих ортогональных соседа по
        // диагонали, чтобы отличить "пинч"-угол от просто внутреннего пола.
        if (!otherIsBoundary)
        {
            foreach (var (dx, dy) in diagonals)
            {
                bool bothOrthoInside = other.Contains(cell.x + dx, cell.y) &&
                                       other.Contains(cell.x, cell.y + dy);
                if (!bothOrthoInside) continue;

                bool diagOutside = !other.Contains(cell.x + dx, cell.y + dy);
                if (diagOutside)
                {
                    otherIsBoundary = true;
                    break;
                }
            }
        }

        return !otherIsBoundary;
    }

    /// <summary>
    /// Сканирует край комнаты ВШИРЬ (вправо и влево относительно направления
    /// расширения dx,dy) от стартовой пограничной клетки (cellX, cellY), по одной
    /// клетке за шаг. Для каждой клетки края заново вычисляется "дополнительная
    /// проверка" занятости (IsCellBlocked): внешняя клетка, куда комната растёт,
    /// помечается либо как свободная/общая стена (green), либо как внутренний пол
    /// чужой комнаты (red — упёрлись). Обход по каждой из сторон останавливается,
    /// когда очередная внешняя клетка оказывается заблокированной либо когда край
    /// комнаты закончился. Возвращает упорядоченный список клеток ВМЕСТЕ с флагом
    /// блокировки, чтобы превью и фактическое применение опирались на один и тот же
    /// цикл.
    /// </summary>
    public static List<(int x, int y, bool blocked)> ExpandRunScanCells(Grid grid, Room room, int cellX, int cellY, int dx, int dy)
    {
        var result = new List<(int x, int y, bool blocked)>();
        if (!room.HasWallOnSide(cellX, cellY, dx, dy))
            return result;

        // Движение вширь вдоль оси, ПЕРПЕНДИКУЛЯРНОЙ направлению расширения
        int stepX = dy != 0 ? 1 : 0;
        int stepY = dx != 0 ? 1 : 0;

        // Стартовая клетка
        bool startBlocked = IsCellBlocked(grid, room, (cellX + dx, cellY + dy));
        result.Add((cellX + dx, cellY + dy, startBlocked));

        // Растём вправо и влево по отдельности
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int wx = cellX + sign * stepX;
            int wy = cellY + sign * stepY;

            // Пока следующая клетка стены комнаты ещё принадлежит краю расширения
            while (room.HasWallOnSide(wx, wy, dx, dy))
            {
                var newCell = (x: wx + dx, y: wy + dy);
                bool cellBlocked = IsCellBlocked(grid, room, newCell);
                result.Add((newCell.x, newCell.y, cellBlocked));
                // Уперлись во внутренний пол чужой комнаты — дальше по этой стороне не идём
                if (cellBlocked) break;

                wx += sign * stepX;
                wy += sign * stepY;
            }
        }

        return result;
    }

    /// <summary>
    /// Только свободные (не заблокированные) клетки из ExpandRunScanCells — то есть
    /// те, которые комната реально займёт при расширении.
    /// </summary>
    public static List<(int x, int y)> ExpandRunAddableCells(Grid grid, Room room, int cellX, int cellY, int dx, int dy)
    {
        return ExpandRunScanCells(grid, room, cellX, cellY, dx, dy)
            .Where(c => !c.blocked)
            .Select(c => (c.x, c.y))
            .ToList();
    }

    /// <summary>
    /// Применяет "умное" расширение комнаты на 1 тайл в направлении (dx, dy) от
    /// пограничной клетки (cellX, cellY), сканируя край ВШИРЬ по одной клетке —
    /// см. ExpandRunScanCells. Прямоугольник комнаты расширяется ровно настолько,
    /// чтобы вместить новые клетки; всё, что попало в новый прямоугольник, но не
    /// было частью старой формы и не входит в добавляемый отрезок, помечается как
    /// вырезанное (RemovedCells) — иначе узкий отрезок растянул бы прямоугольник
    /// на всю ширину новой строки/столбца.
    /// </summary>
    public static bool ExpandRoomBorder(Grid grid, Room room, int cellX, int cellY, int dx, int dy)
    {
        // Сканируем край ВШИРЬ по одной клетке, на каждом шаге заново пересчитывая
        // "дополнительную проверку" занятости (граница соседней комнаты — можно,
        // общая стена; внутренний пол чужой комнаты — стоп).
        var addableCells = ExpandRunAddableCells(grid, room, cellX, cellY, dx, dy);
        if (addableCells.Count == 0) return false;

        var oldCells = new HashSet<(int X, int Y)>();
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                if (room.Contains(x, y))
                    oldCells.Add((x, y));

        int minX = Math.Min(room.X, addableCells.Min(c => c.x));
        int minY = Math.Min(room.Y, addableCells.Min(c => c.y));
        int maxX = Math.Max(room.X + room.Width - 1, addableCells.Max(c => c.x));
        int maxY = Math.Max(room.Y + room.Height - 1, addableCells.Max(c => c.y));

        room.X = minX;
        room.Y = minY;
        room.Width = maxX - minX + 1;
        room.Height = maxY - minY + 1;

        var addableSet = new HashSet<(int X, int Y)>(addableCells);
        var removed = new HashSet<(int X, int Y)>();
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (!oldCells.Contains((x, y)) && !addableSet.Contains((x, y)))
                    removed.Add((x, y));
            }
        }

        room.RemovedCells = removed;
        return true;
    }

    /// <summary>
    /// Убирает "ленточные" остатки толщиной в 1 клетку, которые может оставить
    /// вырезание (по сути простая стена между ничем): клетка комнаты, у которой
    /// по обе стороны хотя бы по одной оси уже НЕТ комнаты — с одной стороны пусто,
    /// с другой тоже пусто (вырез/внешняя граница). Такая клетка не может быть
    /// частью никакого внутреннего пространства — это чисто стена, и она не нужна.
    /// Помечает такие клетки как вырезанные (RemovedCells) и поджимает границы.
    /// Повторяется, пока есть с чем работать: удаление клеток может "оголить"
    /// соседей, превратив их в такие же одноклеточные полоски.
    /// </summary>
    private static void RemoveThinWallSlivers(Room room)
    {
        bool changed;
        do
        {
            changed = false;
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (!room.Contains(x, y)) continue;

                    bool horizontalSandwich = !room.Contains(x - 1, y) && !room.Contains(x + 1, y);
                    bool verticalSandwich = !room.Contains(x, y - 1) && !room.Contains(x, y + 1);

                    if (horizontalSandwich || verticalSandwich)
                    {
                        room.RemovedCells.Add((x, y));
                        changed = true;
                    }
                }
            }
        } while (changed);

        ShrinkBounds(room);
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

    /// <summary>
    /// Расширяет прямоугольник комнаты, чтобы включить все клетки из RemovedCells.
    /// Вызывается после RestoreFromRoom — когда клетки возвращены в комнату,
    /// но прямоугольник комнаты ещё не расширен до их границ.
    /// </summary>
    private static void ExpandBounds(Room room)
    {
        if (room.RemovedCells.Count == 0) return;

        int minX = room.X;
        int maxX = room.X + room.Width - 1;
        int minY = room.Y;
        int maxY = room.Y + room.Height - 1;

        foreach (var cell in room.RemovedCells)
        {
            if (cell.X < minX) minX = cell.X;
            if (cell.X > maxX) maxX = cell.X;
            if (cell.Y < minY) minY = cell.Y;
            if (cell.Y > maxY) maxY = cell.Y;
        }

        int dx = minX - room.X;
        int dy = minY - room.Y;
        room.X += dx;
        room.Y += dy;

        int newRight = Math.Max(maxX + 1, room.X + room.Width);
        int newBottom = Math.Max(maxY + 1, room.Y + room.Height);
        room.Width = newRight - room.X;
        room.Height = newBottom - room.Y;
    }
}