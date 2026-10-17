// Services/GridDivider.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Инструмент «Разделитель гридов». Пользователь накапливает область на гриде
/// (прямоугольники «Выделить»/«Вычесть» — хранятся в Grid.DividerAreas и перед
/// экспортом визуализируются цветными рамками). При экспорте в .yml каждая
/// область выносится из исходного грида в ОТДЕЛЬНЫЙ новый грид-слой, чтобы два
/// вложенных друг в друга грида одной карты не «слипались» и неслись в игру как
/// отдельные физические гриды.
///
/// Правила переноса в новый грид (из эффективной области):
///  - Сущности (MapEntity любого конкретного типа): чей ЦЕНТР (X,Y в тайлах, с
///    дробной частью) лежит ВНУТРИ эффективной области.
///  - Декали (PlacedDecal): чей центр внутри эффективной области.
///  - Тайлы пола (PlacedTile): чья целочисленная клетка внутри эффективной области.
///  - Комнаты (Room): всё, что ПЕРЕСЕКАЕТ область, переносится ЦЕЛИКОМ (с её
///    стенами/дверями/проходами/окнами и привязками), т.к. комнату нельзя разрезать
///    без потери логики узора декалей.
///  - Свободные двери (LooseDoors): по целой клетке внутри эффективной области.
/// Всё, что не попало в область, остаётся в исходном гриде.
/// </summary>
public static class GridDivider
{
    /// <summary>
    /// Возвращает содержимое грида (новые копии списков), принадлежащее эффективной
    /// области area. Используется генератором для выноса области в отдельный
    /// grid-entity при экспорте. Не изменяет исходный грид.
    /// </summary>
    public static Grid ExtractArea(Grid sourceGrid, GridDividerArea area, int newUid)
    {
        var newGrid = new Grid
        {
            Uid = newUid,
            Name = $"{sourceGrid.Name} (область)",
            Position = sourceGrid.Position,
            Color = sourceGrid.Color,
            IsStaticGrid = sourceGrid.IsStaticGrid,
            IsVisible = true,
            ExteriorWallProto = sourceGrid.ExteriorWallProto,
            ReplaceExteriorWalls = sourceGrid.ReplaceExteriorWalls
        };

        bool CenterIn(float x, float y)
        {
            int cx = (int)Math.Floor(x);
            int cy = (int)Math.Floor(y);
            return area.Contains(cx, cy);
        }

        // Комнаты, пересекающие область, — целиком
        foreach (var room in sourceGrid.Rooms)
        {
            if (RoomOverlapsArea(room, area))
                newGrid.Rooms.Add(room.Clone());
        }

        // Сущности по центру клетки
        foreach (var e in sourceGrid.Entities)
            if (CenterIn(e.X, e.Y)) newGrid.Entities.Add(e);

        // Декали по центру клетки
        foreach (var d in sourceGrid.Decals)
            if (CenterIn(d.X, d.Y)) newGrid.Decals.Add(d);

        // Тайлы пола по целой клетке
        foreach (var t in sourceGrid.Tiles)
            if (area.Contains(t.X, t.Y)) newGrid.Tiles.Add(t);

        // Свободные двери по целой клетке
        foreach (var d in sourceGrid.LooseDoors)
            if (area.Contains(d.X, d.Y)) newGrid.LooseDoors.Add(d);

        return newGrid;
    }

    /// <summary>
    /// Возвращает грид-«остаток»: всё содержимое sourceGrid, НЕ принадлежащее ни
    /// одной эффективной области. При пересечении области с комнатой комната
    /// целиком уходит в область, поэтому здесь комнаты, пересекающие любую область,
    /// не воспроизводятся. Используется генератором, чтобы после выноса областей
    /// исходный слой содержал только остаток. Не изменяет исходный грид.
    /// </summary>
    public static Grid ExtractRemainder(Grid sourceGrid, int newUid)
    {
        var newGrid = new Grid
        {
            Uid = newUid,
            Name = sourceGrid.Name,
            Position = sourceGrid.Position,
            Color = sourceGrid.Color,
            IsStaticGrid = sourceGrid.IsStaticGrid,
            IsVisible = true,
            ExteriorWallProto = sourceGrid.ExteriorWallProto,
            ReplaceExteriorWalls = sourceGrid.ReplaceExteriorWalls
        };

        bool InAnyArea(int x, int y)
        {
            foreach (var area in sourceGrid.DividerAreas)
                if (area.Contains(x, y)) return true;
            return false;
        }

        foreach (var room in sourceGrid.Rooms)
        {
            bool overlapsAny = sourceGrid.DividerAreas.Any(a => RoomOverlapsArea(room, a));
            if (!overlapsAny)
                newGrid.Rooms.Add(room.Clone());
        }

        foreach (var e in sourceGrid.Entities)
        {
            if (!InAnyArea((int)Math.Floor(e.X), (int)Math.Floor(e.Y)))
                newGrid.Entities.Add(e);
        }

        foreach (var d in sourceGrid.Decals)
        {
            if (!InAnyArea((int)Math.Floor(d.X), (int)Math.Floor(d.Y)))
                newGrid.Decals.Add(d);
        }

        foreach (var t in sourceGrid.Tiles)
        {
            if (!InAnyArea(t.X, t.Y))
                newGrid.Tiles.Add(t);
        }

        foreach (var d in sourceGrid.LooseDoors)
        {
            if (!InAnyArea(d.X, d.Y))
                newGrid.LooseDoors.Add(d);
        }

        return newGrid;
    }

    private static bool RoomOverlapsArea(Room room, GridDividerArea area)
    {
        foreach (var r in area.AddRects)
        {
            if (room.X < r.X + r.Width && room.X + room.Width > r.X &&
                room.Y < r.Y + r.Height && room.Y + room.Height > r.Y)
                return true;
        }
        return false;
    }
}