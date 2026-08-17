// Services/DecalPatternBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Строит декали "Decal Rule" вдоль периметра комнаты (Auto, глубина всегда 1) или
/// внутри ручной области. Классификация — полноценный 8-направленный tile rule (как
/// в Unity RuleTile): для каждой клетки опрашиваются все 8 соседей (N/S/E/W и 4
/// диагонали), и по полной битовой маске выбирается позиция(и) декали.
///
/// ВАЖНО: в этом движке комната физически не различает "пол" и "стену" по прямоугольнику —
/// Room.Contains(x,y) верно и для клетки пола, и для клетки стены. Поэтому есть предикат
/// IsWallCell, объединяющий ОБА источника стен, которые знает TileBuilder:
/// 1) обычная граница комнаты (ортогональный сосед вне Room.Contains);
/// 2) диагональный "пинч" во внутреннем углу выемки (RoomSubtractor) — раньше
///    DecalPatternBuilder про него не знал и клал декаль прямо в клетку стены.
/// </summary>
public class DecalPatternBuilder
{
    private readonly DecalPackManager _packManager;

    public DecalPatternBuilder(DecalPackManager packManager)
    {
        _packManager = packManager;
    }
    // Подбираемые визуальные коэффициенты смещения декалей в углах (в долях тайла).
    // InnerCorner: 1.0f = смещение на ЦЕЛЫЙ тайл (центр соседней клетки пола по диагонали).
    //   Меньшие дробные значения (0.5f и т.п.) приземляют декаль на ГРАНИЦУ между клетками,
    //   а не на центр ни одной из них — визуально "подвешенное" нецелотайловое положение.
    // OuterCorner: положительное значение = сдвиг ОТ угла к центру комнаты;
    //   отрицательное — ближе к самому углу. Подбирается на глаз под текстуры пака.
    private const float InnerCornerOffset = 0f;
    private const float OuterCornerOffset = 0f;

    // Обычная граница комнаты: сама клетка входит в Room.Contains, но хотя бы один
    // ортогональный сосед — уже вне комнаты. Совпадает с TileBuilder.GetBoundaryWallProto.
    private static bool IsOrthogonalBoundaryWall(Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return false;

        var dirs = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        foreach (var (dx, dy) in dirs)
            if (!room.Contains(x + dx, y + dy)) return true;

        return false;
    }

    // Клетка-"пинч" во внутреннем углу выемки — зеркалит TileBuilder.GetConcaveCornerWallProto:
    // оба ортогональных соседа ещё "внутри" (room.Contains == true), но диагональный сосед
    // вырезан (RemovedCells) внутри прямоугольника комнаты.
    private static bool IsConcavePinchWall(Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return false;

        var diagonals = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
        foreach (var (dx, dy) in diagonals)
        {
            bool orthoOpen = room.Contains(x + dx, y) && room.Contains(x, y + dy);
            if (!orthoOpen) continue;

            bool diagonalForeign = !room.Contains(x + dx, y + dy);
            if (!diagonalForeign) continue;

            int dxi = x + dx, dyi = y + dy;
            bool diagonalInsideBounds = dxi >= room.X && dxi < room.X + room.Width &&
                                         dyi >= room.Y && dyi < room.Y + room.Height;
            if (diagonalInsideBounds) return true; // реально вырезанная диагональ — пинч
        }

        return false;
    }

    // Единая точка правды "эта клетка — стена?" для декального модуля — объединяет
    // оба источника стен из TileBuilder.
    private static bool IsWallCell(Room room, int x, int y)
    {
        return IsOrthogonalBoundaryWall(room, x, y) || IsConcavePinchWall(room, x, y);
    }

    // "Стена" с точки зрения соседа клетки-кандидата на декаль: физическая стена
    // (IsWallCell), выход за пределы комнаты, либо дверь — дверь геометрически может
    // не быть IsWallCell, но для трассировки узора тоже считается препятствием.
    private static bool IsObstacleSide(Grid grid, Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return true;
        if (IsWallCell(room, x, y)) return true;
        if (IsDoorAnywhereInGrid(grid, x, y)) return true;
        return false;
    }

    // Раньше проверялся только room.Doors ТЕКУЩЕЙ комнаты — дверь физически принадлежит
    // одной комнате (владельцу), поэтому соседняя комната у той же самой двери никогда
    // не видела её в своём списке и рисовала обычный Side/Corner вместо Door. Теперь
    // ищем дверь по ВСЕМУ гриду (все комнаты + LooseDoors), чтобы обе соседние комнаты
    // одинаково распознавали дверь на границе.
    private static bool IsDoorAnywhereInGrid(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
            if (r.Doors.Any(d => d.X == x && d.Y == y)) return true;
        return grid.LooseDoors.Any(d => d.X == x && d.Y == y);
    }

    /// <summary>Полная 8-направленная маска соседей клетки (x, y) внутри room.</summary>
    private struct NeighborMask
    {
        public bool N, S, E, W, NE, NW, SE, SW;
        public int OrthoCount => (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);
    }

    private static NeighborMask GetNeighborMask(Grid grid, Room room, int x, int y)
    {
        return new NeighborMask
        {
            N = IsObstacleSide(grid, room, x, y - 1),
            S = IsObstacleSide(grid, room, x, y + 1),
            E = IsObstacleSide(grid, room, x + 1, y),
            W = IsObstacleSide(grid, room, x - 1, y),
            NE = IsObstacleSide(grid, room, x + 1, y - 1),
            NW = IsObstacleSide(grid, room, x - 1, y - 1),
            SE = IsObstacleSide(grid, room, x + 1, y + 1),
            SW = IsObstacleSide(grid, room, x - 1, y + 1),
        };
    }

    public void RecalculateForRoom(Grid grid, Room room)
    {
        int ownerId = room.GetHashCode();
        grid.Decals.RemoveAll(d => d.PatternOwnerId == ownerId);

        if (room.DecalMode == DecalPatternMode.Auto)
        {
            grid.Decals.AddRange(BuildAutoPattern(grid, room));
        }
        else if (room.DecalMode == DecalPatternMode.Manual)
        {
            foreach (var area in room.ManualDecalAreas)
                grid.Decals.AddRange(BuildAreaPattern(grid, room, area, ownerId));
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
                grid.Decals.AddRange(BuildAutoPattern(grid, room));
            }
            else if (room.DecalMode == DecalPatternMode.Manual)
            {
                int ownerId = room.GetHashCode();
                foreach (var area in room.ManualDecalAreas)
                    grid.Decals.AddRange(BuildAreaPattern(grid, room, area, ownerId));
            }
        }
    }

    private List<PlacedDecal> BuildAutoPattern(Grid grid, Room room)
    {
        var result = new List<PlacedDecal>();
        int ownerId = room.GetHashCode();

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;
                if (IsWallCell(room, x, y)) continue; // сама клетка стены (включая пинч) — декали не ставим

                var mask = GetNeighborMask(grid, room, x, y);

                if (IsDoorAnywhereInGrid(grid, x, y - 1) || IsDoorAnywhereInGrid(grid, x, y + 1) ||
                    IsDoorAnywhereInGrid(grid, x + 1, y) || IsDoorAnywhereInGrid(grid, x - 1, y))
                {
                    AddDecalsForPosition(room.AutoDecalRule, room, x, y, DecalPosition.Door, ownerId, result);
                    continue;
                }

                foreach (var (position, cornerDir) in ClassifyPositions(mask))
                {
                    if (position is DecalPosition.InnerCornerNE or DecalPosition.InnerCornerNW
                        or DecalPosition.InnerCornerSE or DecalPosition.InnerCornerSW)
                    {
                        // Диагональ cornerDir физически стена/пинч. Сдвигаем декаль вглубь
                        // комнаты, НО только если клетка назначения реально пол — иначе
                        // (узкая выемка) декаль может улететь в клетку стены другого угла
                        int targetX = x + (int)Math.Round(-cornerDir.dx * InnerCornerOffset);
                        int targetY = y + (int)Math.Round(-cornerDir.dy * InnerCornerOffset);

                        bool targetIsFloor = room.Contains(targetX, targetY) && !IsWallCell(room, targetX, targetY);

                        if (targetIsFloor)
                        {
                            AddDecalsForPositionOffset(room.AutoDecalRule, room, x, y, position, ownerId, result,
                                -cornerDir.dx * InnerCornerOffset, -cornerDir.dy * InnerCornerOffset);
                        }
                        else
                        {
                            AddDecalsForPosition(room.AutoDecalRule, room, x, y, position, ownerId, result);
                        }
                    }
                    else if (position is DecalPosition.OuterCornerNE or DecalPosition.OuterCornerNW
                        or DecalPosition.OuterCornerSE or DecalPosition.OuterCornerSW)
                    {
                        AddDecalsForPositionOffset(room.AutoDecalRule, room, x, y, position, ownerId, result,
                            -cornerDir.dx * OuterCornerOffset, -cornerDir.dy * OuterCornerOffset);
                    }
                    else
                    {
                        AddDecalsForPosition(room.AutoDecalRule, room, x, y, position, ownerId, result);
                    }
                }
            }
        }

        return result;
    }



    /// <summary>
    /// Возвращает СПИСОК позиций для клетки (обычно один элемент, но для узкого коридора
    /// в 1 тайл — стены сразу с двух ПРОТИВОПОЛОЖНЫХ сторон, N+S или E+W — возвращает
    /// два элемента: эффект от одной стены и эффект от другой складываются в одной
    /// клетке, а не выбирается одна сторона произвольно/пропускается совсем.
    /// </summary>
    private static List<(DecalPosition position, (int dx, int dy) cornerDir)> ClassifyPositions(NeighborMask m)
    {
        var result = new List<(DecalPosition, (int dx, int dy))>();
        int ortho = m.OrthoCount;

        if (ortho == 3)
        {
            // Суффикс DeadEnd означает сторону СТЕНЫ-заглушки (куда упирается тупик),
            // а не открытую сторону прохода. !m.N значит "открыт север" => стена-заглушка
            // с юга => DeadEndS.
            if (!m.N) result.Add((DecalPosition.DeadEndS, (0, 0)));
            else if (!m.S) result.Add((DecalPosition.DeadEndN, (0, 0)));
            else if (!m.E) result.Add((DecalPosition.DeadEndW, (0, 0)));
            else result.Add((DecalPosition.DeadEndE, (0, 0)));
            return result;
        }

        if (ortho == 2)
        {
            // Смежные стены — обычный внешний угол (cornerDir — направление к углу)
            if (m.N && m.E) { result.Add((DecalPosition.OuterCornerNE, (1, -1))); return result; }
            if (m.N && m.W) { result.Add((DecalPosition.OuterCornerNW, (-1, -1))); return result; }
            if (m.S && m.E) { result.Add((DecalPosition.OuterCornerSE, (1, 1))); return result; }
            if (m.S && m.W) { result.Add((DecalPosition.OuterCornerSW, (-1, 1))); return result; }

            // Противоположные стены (узкий коридор в 1 тайл) — складываем оба эффекта:
            // и SideN, и SideS (или SideE+SideW) ставятся в одну и ту же клетку сразу
            if (m.N && m.S)
            {
                result.Add((DecalPosition.SideN, (0, 0)));
                result.Add((DecalPosition.SideS, (0, 0)));
                return result;
            }
            if (m.E && m.W)
            {
                result.Add((DecalPosition.SideE, (0, 0)));
                result.Add((DecalPosition.SideW, (0, 0)));
                return result;
            }
            return result;
        }

        if (ortho == 1)
        {
            if (m.N) result.Add((DecalPosition.SideN, (0, 0)));
            else if (m.S) result.Add((DecalPosition.SideS, (0, 0)));
            else if (m.E) result.Add((DecalPosition.SideE, (0, 0)));
            else result.Add((DecalPosition.SideW, (0, 0)));
            return result;
        }

        // ortho == 0 — все 4 ортогональные стороны открыты. Проверяем диагонали на InnerCorner
        // (в редких случаях может сработать сразу несколько диагоналей — складываем и их)
        if (m.NE) result.Add((DecalPosition.InnerCornerNE, (1, -1)));
        if (m.NW) result.Add((DecalPosition.InnerCornerNW, (-1, -1)));
        if (m.SE) result.Add((DecalPosition.InnerCornerSE, (1, 1)));
        if (m.SW) result.Add((DecalPosition.InnerCornerSW, (-1, 1)));

        return result;
    }

    private void AddDecalsForPosition(DecalRuleSet rule, Room room, int x, int y, DecalPosition position, int ownerId, List<PlacedDecal> result)
    {
        AddDecalsForPositionOffset(rule, room, x, y, position, ownerId, result, 0f, 0f);
    }

    private void AddDecalsForPositionOffset(DecalRuleSet rule, Room room, int x, int y, DecalPosition position, int ownerId, List<PlacedDecal> result, float offsetX, float offsetY)
    {
        if (rule == null) return;

        for (int i = 0; i < rule.Layers.Count; i++)
        {
            var layer = rule.Layers[i];
            if (!layer.Enabled) continue;
            if (string.IsNullOrEmpty(layer.SourcePackId)) continue;

            var pack = _packManager.GetById(layer.SourcePackId);
            if (pack == null) continue;
            if (!pack.Positions.TryGetValue(position, out var proto) || string.IsNullOrEmpty(proto)) continue;

            result.Add(new PlacedDecal
            {
                X = x + 0.5f + offsetX,
                Y = y + 0.5f + offsetY,
                Proto = proto,
                Color = pack.Color,
                Rotation = 0,
                Cleanable = false,
                PatternOwnerId = ownerId,
                PatternLayerIndex = i
            });
        }
    }

    private List<PlacedDecal> BuildAreaPattern(Grid grid, Room room, ManualDecalArea area, int ownerId)
    {
        var result = new List<PlacedDecal>();

        for (int x = area.X; x < area.X + area.Width; x++)
        {
            for (int y = area.Y; y < area.Y + area.Height; y++)
            {
                if (!room.Contains(x, y)) continue;
                if (IsWallCell(room, x, y)) continue;

                var mask = GetNeighborMask(grid, room, x, y);

                foreach (var (position, cornerDir) in ClassifyPositions(mask))
                {
                    if (position is DecalPosition.InnerCornerNE or DecalPosition.InnerCornerNW
                        or DecalPosition.InnerCornerSE or DecalPosition.InnerCornerSW)
                    {
                        int targetX = x + (int)Math.Round(-cornerDir.dx * InnerCornerOffset);
                        int targetY = y + (int)Math.Round(-cornerDir.dy * InnerCornerOffset);

                        bool targetIsFloor = room.Contains(targetX, targetY) && !IsWallCell(room, targetX, targetY);

                        if (targetIsFloor)
                        {
                            AddDecalsForPositionOffset(area.Rule, room, x, y, position, ownerId, result,
                                -cornerDir.dx * InnerCornerOffset, -cornerDir.dy * InnerCornerOffset);
                        }
                        else
                        {
                            AddDecalsForPosition(area.Rule, room, x, y, position, ownerId, result);
                        }
                    }
                    else if (position is DecalPosition.OuterCornerNE or DecalPosition.OuterCornerNW
                        or DecalPosition.OuterCornerSE or DecalPosition.OuterCornerSW)
                    {
                        AddDecalsForPositionOffset(area.Rule, room, x, y, position, ownerId, result,
                            -cornerDir.dx * OuterCornerOffset, -cornerDir.dy * OuterCornerOffset);
                    }
                    else
                    {
                        AddDecalsForPosition(area.Rule, room, x, y, position, ownerId, result);
                    }
                }
            }
        }

        return result;
    }
}