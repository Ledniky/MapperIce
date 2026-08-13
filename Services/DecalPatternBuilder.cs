// Services/DecalPatternBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Строит декали "Decal Rule" вдоль периметра комнаты (Auto, глубина всегда 1) или
/// внутри ручной области.
///
/// ВАЖНО: в этом движке комната физически не различает "пол" и "стену" по прямоугольнику —
/// Room.Contains(x,y) верно и для клетки пола, и для клетки стены (TileBuilder кладёт
/// Wall поверх той же координаты, что Floor, на внешнем кольце). Поэтому нужно два разных
/// предиката: IsWallCell — сама стена (кольцо), IsFloorRingCell — первый ряд ПОЛА внутрь
/// от стены, куда и нужно класть декали. Раньше оба предиката ошибочно совпадали, и декали
/// оказывались на клетках стен.
/// </summary>
public class DecalPatternBuilder
{
    // Физическая клетка стены: сама входит в Room.Contains, но хотя бы один ортогональный
    // сосед — уже вне комнаты. Совпадает с логикой TileBuilder.GetBoundaryWallProto.
    private static bool IsWallCell(Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return false;

        var dirs = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        foreach (var (dx, dy) in dirs)
            if (!room.Contains(x + dx, y + dy)) return true;

        return false;
    }

    // Первый ряд ПОЛА внутрь от стены — сама клетка не стена, но хотя бы один
    // ортогональный сосед является стеной. Именно сюда кладутся декали Auto-режима.
    private static bool IsFloorRingCell(Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return false;
        if (IsWallCell(room, x, y)) return false;

        var dirs = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        foreach (var (dx, dy) in dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (!room.Contains(nx, ny)) return true; // защитный случай, обычно недостижим для ring=1
            if (IsWallCell(room, nx, ny)) return true;
        }
        return false;
    }

    // ДОПУЩЕНИЕ (проговорено ранее как открытый вопрос): дверная клетка для целей
    // трассировки по-прежнему считается "стеной" геометрически (Room.Contains не отличает
    // дверь от стены) — просто отдельно проверяется, дверь ли это, чтобы выбрать позицию Door.
    private static bool IsDoorAt(Room room, int x, int y) => room.Doors.Any(d => d.X == x && d.Y == y);

    public void RecalculateForRoom(Grid grid, Room room)
    {
        int ownerId = room.GetHashCode();
        grid.Decals.RemoveAll(d => d.PatternOwnerId == ownerId);

        if (room.DecalMode == DecalPatternMode.Auto)
        {
            grid.Decals.AddRange(BuildAutoPattern(room));
        }
        else if (room.DecalMode == DecalPatternMode.Manual)
        {
            foreach (var area in room.ManualDecalAreas)
                grid.Decals.AddRange(BuildAreaPattern(room, area, ownerId));
        }
    }

    /// <summary>Пересчёт для всех комнат грида — используется после массовых операций (delete area, undo/redo, load).</summary>
    public void RecalculateAll(Grid grid)
    {
        grid.Decals.RemoveAll(d => d.PatternOwnerId != null);

        foreach (var room in grid.Rooms)
        {
            if (room.DecalMode == DecalPatternMode.Auto)
            {
                grid.Decals.AddRange(BuildAutoPattern(room));
            }
            else if (room.DecalMode == DecalPatternMode.Manual)
            {
                int ownerId = room.GetHashCode();
                foreach (var area in room.ManualDecalAreas)
                    grid.Decals.AddRange(BuildAreaPattern(room, area, ownerId));
            }
        }
    }

    private List<PlacedDecal> BuildAutoPattern(Room room)
    {
        var result = new List<PlacedDecal>();
        int ownerId = room.GetHashCode();

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;

                // Пропускаем саму клетку стены — декали идут строго на пол рядом с ней
                if (IsWallCell(room, x, y)) continue;
                if (!room.Contains(x, y)) continue;

                bool north = IsWallCell(room, x, y - 1);
                bool south = IsWallCell(room, x, y + 1);
                bool east = IsWallCell(room, x + 1, y);
                bool west = IsWallCell(room, x - 1, y);

                if (!north && !south && !east && !west)
                {
                    TryAddInnerCorner(room, x, y, ownerId, result);
                    continue; // глубокий интерьер без диагонального выреза — декали не нужны
                }

                var position = ClassifyPosition(north, south, east, west);
                if (position == null) continue;

                var doorPos = FindAdjacentDoorPosition(room, x, y, north, south, east, west);
                if (doorPos.HasValue)
                    AddDecalsForPosition(room.AutoDecalRule, room, x, y, DecalPosition.Door, ownerId, result);
                else
                    AddDecalsForPosition(room.AutoDecalRule, room, x, y, position.Value, ownerId, result);
            }
        }

        return result;
    }

    private (int x, int y)? FindAdjacentDoorPosition(Room room, int x, int y, bool n, bool s, bool e, bool w)
    {
        if (n && IsDoorAt(room, x, y - 1)) return (x, y - 1);
        if (s && IsDoorAt(room, x, y + 1)) return (x, y + 1);
        if (e && IsDoorAt(room, x + 1, y)) return (x + 1, y);
        if (w && IsDoorAt(room, x - 1, y)) return (x - 1, y);
        return null;
    }

    private static DecalPosition? ClassifyPosition(bool n, bool s, bool e, bool w)
    {
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

        if (count == 1)
        {
            if (n) return DecalPosition.SideN;
            if (s) return DecalPosition.SideS;
            if (e) return DecalPosition.SideE;
            return DecalPosition.SideW;
        }

        if (count == 2)
        {
            if (n && e) return DecalPosition.OuterCornerNE;
            if (n && w) return DecalPosition.OuterCornerNW;
            if (s && e) return DecalPosition.OuterCornerSE;
            if (s && w) return DecalPosition.OuterCornerSW;
            return null; // N+S или E+W (коридор в 1 тайл) — в прототипе не обрабатываем
        }

        if (count == 3)
        {
            if (!n) return DecalPosition.DeadEndN;
            if (!s) return DecalPosition.DeadEndS;
            if (!e) return DecalPosition.DeadEndE;
            return DecalPosition.DeadEndW;
        }

        return null; // count == 4 — полностью замкнутая клетка (узкий тупик), пропускаем
    }

    /// <summary>
    /// Внутренние (вогнутые) углы — клетка без ортогональных стен-соседей, но с диагональю,
    /// выходящей за пределы комнаты (тот же принцип, что Renderer.DrawConcaveCornerConnectors).
    /// </summary>
    private void TryAddInnerCorner(Room room, int x, int y, int ownerId, List<PlacedDecal> result)
    {
        var diagonals = new (int dx, int dy, DecalPosition pos)[]
        {
            (1, 1, DecalPosition.InnerCornerSE),
            (1, -1, DecalPosition.InnerCornerNE),
            (-1, 1, DecalPosition.InnerCornerSW),
            (-1, -1, DecalPosition.InnerCornerNW),
        };

        foreach (var (dx, dy, pos) in diagonals)
        {
            bool orthoOpen = room.Contains(x + dx, y) && room.Contains(x, y + dy);
            bool diagonalForeign = !room.Contains(x + dx, y + dy);
            if (orthoOpen && diagonalForeign)
            {
                AddDecalsForPosition(room.AutoDecalRule, room, x, y, pos, ownerId, result);
                return;
            }
        }
    }

private void AddDecalsForPosition(DecalRuleSet rule, Room room, int x, int y, DecalPosition position, int ownerId, List<PlacedDecal> result)
    {
        if (rule == null) return;

        for (int i = 0; i < rule.Layers.Count; i++)
        {
            var layer = rule.Layers[i];
            if (!layer.Enabled) continue;
            if (!layer.Positions.TryGetValue(position, out var proto) || string.IsNullOrEmpty(proto)) continue;

            result.Add(new PlacedDecal
            {
                // Renderer.DrawDecalsBatch трактует X/Y как ЦЕНТР тайла (ToRect с offset -0.5/-0.5),
                // как и ручная установка декали через _centerOffset (по умолчанию 0.5/0.5).
                // Без +0.5f декаль рисуется со сдвигом на пол-тайла вверх-влево от нужной клетки
                X = x + 0.5f, Y = y + 0.5f, Proto = proto, Color = layer.Color, Rotation = 0, Cleanable = false,
                PatternOwnerId = ownerId, PatternLayerIndex = i
            });
        }
    }
    private List<PlacedDecal> BuildAreaPattern(Room room, ManualDecalArea area, int ownerId)
    {
        var result = new List<PlacedDecal>();

        for (int x = area.X; x < area.X + area.Width; x++)
        {
            for (int y = area.Y; y < area.Y + area.Height; y++)
            {
                if (!room.Contains(x, y)) continue;
                if (IsWallCell(room, x, y)) continue;

                bool north = IsWallCell(room, x, y - 1);
                bool south = IsWallCell(room, x, y + 1);
                bool east = IsWallCell(room, x + 1, y);
                bool west = IsWallCell(room, x - 1, y);

                var position = ClassifyPosition(north, south, east, west);
                if (position == null) continue;

                AddDecalsForPosition(area.Rule, room, x, y, position.Value, ownerId, result);
            }
        }

        return result;
    }
}