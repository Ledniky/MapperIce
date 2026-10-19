// Services/RoomCleanup.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Снос комнаты вместе со всем, что она привнесла на карту.
///
/// Убрать один Room недостаточно: часть связанных с комнатой объектов хранится
/// ВНЕ неё, и без уборки они остаются на карте «призраками»:
///
/// - пометки проходов и окон (Room.Passages / Room.Windows). У стены, общей для
///   двух комнат, пометку по клику получает только ПЕРВАЯ подходящая комната,
///   поэтому после удаления второй комнаты проход в её стене оставался навсегда:
///   TileBuilder пересобирает стены с нуля, но продолжал видеть пометку у соседа
///   и стену на этой клетке не восстанавливал;
/// - сущности окна и решётки (MapEntity в Grid.Entities) — они вообще не лежат в
///   Room, поэтому оставались висеть спрайтами там, где комнаты уже нет;
/// - декали, сгенерированные узором комнаты (PlacedDecal.PatternOwnerId ==
///   room.Id) — их координаты могут выходить за границы комнаты (периметр,
///   смещённые offset-декали), поэтому фильтр «декали внутри прямоугольника»
///   их не находил.
///
/// Двери и пожарные шлюзы отдельной уборки не требуют: двери уходят вместе с
/// комнатой, а шлюзы пересоздаёт DoorUpdater.RecalculateAllDoors по списку
/// оставшихся дверей.
/// </summary>
public static class RoomCleanup
{
    private static int FloorToInt(float v) => (int)Math.Floor(v);

    /// <summary>
    /// Снести комнату: пометки проходов/окон на её клетках (в том числе у соседей
    /// по общей стене), сущности окон и решёток, декали комнаты и саму комнату.
    /// </summary>
    /// <param name="doorUpdater">
    /// Если передан — заново раскладывает двери и пересоздаёт шлюзы. При удалении
    /// нескольких комнат подряд его лучше не передавать, а вызвать
    /// RecalculateAllDoors один раз в конце.
    /// </param>
    public static void Remove(Grid grid, Room room, DoorUpdater? doorUpdater = null)
    {
        if (grid == null || room == null) return;

        // Реальные клетки комнаты (без вырезанных) — по ним ищем «стенные» пометки
        // соседей и оконные сущности.
        var cells = new HashSet<(int X, int Y)>();
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                if (room.Contains(x, y))
                    cells.Add((x, y));

        // Снимок клеток-окон ВСЕХ комнат ДО удаления: пометка окна на общей стене
        // могла достаться соседу, и её сущность (окно + решётка) тоже должна уйти
        // вместе с удаляемой комнатой.
        var windowCells = new HashSet<(int X, int Y)>();
        foreach (var r in grid.Rooms)
            foreach (var w in r.Windows)
                windowCells.Add(w);

        // Пометки стен (проход/окно) у ВСЕХ комнат на клетках удаляемой: своя сама
        // уходит целиком, но такая же пометка на общей стене вполне может лежать
        // в списке соседа — и без чистки она остаётся «вечным проходом».
        foreach (var r in grid.Rooms)
        {
            r.Passages.RemoveAll(p => cells.Contains((p.X, p.Y)));
            r.HiddenWalls.RemoveAll(w => cells.Contains((w.X, w.Y)));
            r.Windows.RemoveAll(w => cells.Contains((w.X, w.Y)));
        }

        // Оконные сущности (окно + решётка): своих клеток-окон у комнаты больше не
        // будет (список уходит вместе с ней), поэтому берём снимок её собственных
        // пометок плюс клетки-окна соседей, попавшие внутрь удаляемой комнаты.
        var entityCells = new HashSet<(int X, int Y)>(room.Windows);
        foreach (var cell in cells)
            if (windowCells.Contains(cell))
                entityCells.Add(cell);

        if (entityCells.Count > 0)
            grid.Entities.RemoveAll(e => e.GetType() == typeof(MapEntity) &&
                                         entityCells.Contains((FloorToInt(e.X), FloorToInt(e.Y))));

        // Декали комнаты: и сгенерированные её узором (PatternOwnerId — такой ключ
        // ставит DecalPatternBuilder), и просто лежащие в её прямоугольнике.
        grid.Decals.RemoveAll(d => d.PatternOwnerId == room.Id);
        grid.Decals.RemoveAll(d => d.X >= room.X && d.X < room.X + room.Width &&
                                   d.Y >= room.Y && d.Y < room.Y + room.Height);

        grid.Rooms.Remove(room);
        doorUpdater?.RecalculateAllDoors(grid);
    }

    /// <summary>
    /// Убирает пометки проходов/окон, которые перестали принадлежать комнате:
    /// после подрезки прямоугольника (ShrinkBounds) или вырезания клеток
    /// (RemovedCells) пометка может остаться в клетке, которой у комнаты уже нет.
    /// Сама по себе такая пометка ничего не рисует, но TileBuilder собирает набор
    /// клеток-проходов/окон по ВСЕМ комнатам и по нему не строит стены — значит
    /// уцелевшая пометка навсегда «глушит» стену соседней комнаты (та же
    /// ситуация «проход не удаляется», только без удаления комнаты).
    /// </summary>
    public static bool PruneStaleWallFeatures(Room room)
    {
        if (room == null) return false;

        bool changed = room.Passages.RemoveAll(p => !room.Contains(p.X, p.Y)) > 0;
        changed |= room.HiddenWalls.RemoveAll(w => !room.Contains(w.X, w.Y)) > 0;
        changed |= room.Windows.RemoveAll(w => !room.Contains(w.X, w.Y)) > 0;
        return changed;
    }
}
