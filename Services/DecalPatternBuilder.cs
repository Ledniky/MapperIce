// Services/DecalPatternBuilder.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Строит декали "Decal Rule" вдоль периметра комнаты (для слоёв в режиме Auto,
/// глубина всегда 1) или внутри ручных областей слоя (для слоёв в режиме Manual).
/// Режим теперь задаётся ПОСЛОЙНО (DecalLayer.Mode), а не для комнаты в целом —
/// в одной комнате один слой может идти по периметру автоматически, а другой
/// расставляться вручную своими прямоугольниками, независимо друг от друга.
///
/// Классификация — полноценный 8-направленный tile rule (как в Unity RuleTile):
/// для каждой клетки опрашиваются все 8 соседей (N/S/E/W и 4 диагонали), и по
/// полной битовой маске выбирается позиция(и) декали.
///
/// ВАЖНО: в этом движке комната физически не различает "пол" и "стену" по
/// прямоугольнику — Room.Contains(x,y) верно и для клетки пола, и для клетки
/// стены. Поэтому есть предикат IsWallCell, объединяющий ОБА источника стен,
/// которые знает TileBuilder:
/// 1) обычная граница комнаты (ортогональный сосед вне Room.Contains);
/// 2) диагональный "пинч" во внутреннем углу выемки (RoomSubtractor).
/// </summary>
public class DecalPatternBuilder
{
    private readonly DecalPackManager _packManager;

    public DecalPatternBuilder(DecalPackManager packManager)
    {
        _packManager = packManager;
    }

    // Подбираемые визуальные коэффициенты смещения декалей в углах (в долях тайла).
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

    // Клетка-"пинч" во внутреннем углу выемки — зеркалит TileBuilder.GetConcaveCornerWallProto.
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
            if (diagonalInsideBounds) return true;
        }

        return false;
    }

    private static bool IsWallCell(Room room, int x, int y)
    {
        return IsOrthogonalBoundaryWall(room, x, y) || IsConcavePinchWall(room, x, y);
    }

    private static bool IsObstacleSide(Grid grid, Room room, int x, int y)
    {
        if (!room.Contains(x, y)) return true;
        if (IsWallCell(room, x, y)) return true;
        if (IsDoorAnywhereInGrid(grid, x, y)) return true;
        return false;
    }

    private static bool IsDoorAnywhereInGrid(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
            if (r.Doors.Any(d => d.X == x && d.Y == y)) return true;
        return grid.LooseDoors.Any(d => d.X == x && d.Y == y);
    }

    // Для РУЧНОЙ области прямоугольник — это ГРАНИЦА тайлрула (виртуальная стена),
    // а не просто список клеток, которые нужно залить декалью. Сосед считается
    // "препятствием", если он вышел за пределы прямоугольника области — так вдоль
    // краёв прямоугольника получается кайма (Side/Corner/DeadEnd), в точности как
    // по периметру настоящей комнаты, а не одна и та же декаль на всех клетках подряд.
    // Настоящие стены комнаты и двери остаются препятствиями всегда, даже если
    // формально ещё внутри прямоугольника.
    private static bool IsObstacleForArea(Grid grid, Room room, ManualDecalArea area, int x, int y)
    {
        bool insideRect = x >= area.X && x < area.X + area.Width &&
                           y >= area.Y && y < area.Y + area.Height;
        if (!insideRect) return true;
        if (!room.Contains(x, y)) return true;
        if (IsWallCell(room, x, y)) return true;
        if (IsDoorAnywhereInGrid(grid, x, y)) return true;
        return false;
    }

    private static NeighborMask GetNeighborMaskForArea(Grid grid, Room room, ManualDecalArea area, int x, int y)
    {
        return new NeighborMask
        {
            N = IsObstacleForArea(grid, room, area, x, y - 1),
            S = IsObstacleForArea(grid, room, area, x, y + 1),
            E = IsObstacleForArea(grid, room, area, x + 1, y),
            W = IsObstacleForArea(grid, room, area, x - 1, y),
            NE = IsObstacleForArea(grid, room, area, x + 1, y - 1),
            NW = IsObstacleForArea(grid, room, area, x - 1, y - 1),
            SE = IsObstacleForArea(grid, room, area, x + 1, y + 1),
            SW = IsObstacleForArea(grid, room, area, x - 1, y + 1),
        };
    }

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

        for (int i = 0; i < room.AutoDecalRule.Layers.Count; i++)
        {
            var layer = room.AutoDecalRule.Layers[i];
            if (!layer.Enabled) continue;

            if (layer.Mode == DecalPatternMode.Auto)
            {
                grid.Decals.AddRange(BuildAutoPatternForLayer(grid, room, layer, i, ownerId));
            }
            else
            {
                foreach (var area in layer.ManualAreas)
                    grid.Decals.AddRange(BuildAreaPatternForLayer(grid, room, area, layer, i, ownerId));
            }
        }
    }

    /// <summary>Пересчёт для всех комнат грида — используется после массовых операций (delete area, undo/redo, load).</summary>
    public void RecalculateAll(Grid grid)
    {
        grid.Decals.RemoveAll(d => d.PatternOwnerId != null);

        foreach (var room in grid.Rooms)
        {
            int ownerId = room.GetHashCode();

            for (int i = 0; i < room.AutoDecalRule.Layers.Count; i++)
            {
                var layer = room.AutoDecalRule.Layers[i];
                if (!layer.Enabled) continue;

                if (layer.Mode == DecalPatternMode.Auto)
                {
                    grid.Decals.AddRange(BuildAutoPatternForLayer(grid, room, layer, i, ownerId));
                }
                else
                {
                    foreach (var area in layer.ManualAreas)
                        grid.Decals.AddRange(BuildAreaPatternForLayer(grid, room, area, layer, i, ownerId));
                }
            }
        }
    }

    private List<PlacedDecal> BuildAutoPatternForLayer(Grid grid, Room room, DecalLayer layer, int layerIndex, int ownerId)
    {
        var result = new List<PlacedDecal>();

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;
                if (IsWallCell(room, x, y)) continue;

                var mask = GetNeighborMask(grid, room, x, y);

                if (IsDoorAnywhereInGrid(grid, x, y - 1) || IsDoorAnywhereInGrid(grid, x, y + 1) ||
                    IsDoorAnywhereInGrid(grid, x + 1, y) || IsDoorAnywhereInGrid(grid, x - 1, y))
                {
                    AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.Door, ownerId, result);
                    continue;
                }

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
                            AddDecalForLayerPositionOffset(layer, layerIndex, room, x, y, position, ownerId, result,
                                -cornerDir.dx * InnerCornerOffset, -cornerDir.dy * InnerCornerOffset);
                        }
                        else
                        {
                            AddDecalForLayerPosition(layer, layerIndex, room, x, y, position, ownerId, result);
                        }
                    }
                    else if (position is DecalPosition.OuterCornerNE or DecalPosition.OuterCornerNW
                        or DecalPosition.OuterCornerSE or DecalPosition.OuterCornerSW)
                    {
                        AddDecalForLayerPositionOffset(layer, layerIndex, room, x, y, position, ownerId, result,
                            -cornerDir.dx * OuterCornerOffset, -cornerDir.dy * OuterCornerOffset);
                    }
                    else
                    {
                        AddDecalForLayerPosition(layer, layerIndex, room, x, y, position, ownerId, result);
                    }
                }
            }
        }

        return result;
    }

    private List<PlacedDecal> BuildAreaPatternForLayer(Grid grid, Room room, ManualDecalArea area, DecalLayer layer, int layerIndex, int ownerId)
    {
        var result = new List<PlacedDecal>();

        for (int x = area.X; x < area.X + area.Width; x++)
        {
            for (int y = area.Y; y < area.Y + area.Height; y++)
            {
                if (!room.Contains(x, y)) continue;
                if (IsWallCell(room, x, y)) continue;

                // Ключевое отличие от авто-режима по периметру комнаты: тут границей
                // "стен" для тайлрула служит сам прямоугольник area, а не реальные
                // стены комнаты — поэтому GetNeighborMaskForArea, а не GetNeighborMask.
                var mask = GetNeighborMaskForArea(grid, room, area, x, y);

                if (IsDoorAnywhereInGrid(grid, x, y - 1) || IsDoorAnywhereInGrid(grid, x, y + 1) ||
                    IsDoorAnywhereInGrid(grid, x + 1, y) || IsDoorAnywhereInGrid(grid, x - 1, y))
                {
                    AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.Door, ownerId, result);
                    continue;
                }

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
                            AddDecalForLayerPositionOffset(layer, layerIndex, room, x, y, position, ownerId, result,
                                -cornerDir.dx * InnerCornerOffset, -cornerDir.dy * InnerCornerOffset);
                        }
                        else
                        {
                            AddDecalForLayerPosition(layer, layerIndex, room, x, y, position, ownerId, result);
                        }
                    }
                    else if (position is DecalPosition.OuterCornerNE or DecalPosition.OuterCornerNW
                        or DecalPosition.OuterCornerSE or DecalPosition.OuterCornerSW)
                    {
                        AddDecalForLayerPositionOffset(layer, layerIndex, room, x, y, position, ownerId, result,
                            -cornerDir.dx * OuterCornerOffset, -cornerDir.dy * OuterCornerOffset);
                    }
                    else
                    {
                        AddDecalForLayerPosition(layer, layerIndex, room, x, y, position, ownerId, result);
                    }
                }
            }
        }

        return result;
    }


    /// <summary>
    /// Возвращает СПИСОК позиций для клетки (обычно один элемент, но для узкого коридора
    /// в 1 тайл — стены сразу с двух ПРОТИВОПОЛОЖНЫХ сторон, N+S или E+W — возвращает
    /// два элемента).
    /// </summary>
    private static List<(DecalPosition position, (int dx, int dy) cornerDir)> ClassifyPositions(NeighborMask m)
    {
        var result = new List<(DecalPosition, (int dx, int dy))>();
        int ortho = m.OrthoCount;

        if (ortho == 3)
        {
            if (!m.N) result.Add((DecalPosition.DeadEndS, (0, 0)));
            else if (!m.S) result.Add((DecalPosition.DeadEndN, (0, 0)));
            else if (!m.E) result.Add((DecalPosition.DeadEndW, (0, 0)));
            else result.Add((DecalPosition.DeadEndE, (0, 0)));
            return result;
        }

        if (ortho == 2)
        {
            if (m.N && m.E) { result.Add((DecalPosition.OuterCornerNE, (1, -1))); return result; }
            if (m.N && m.W) { result.Add((DecalPosition.OuterCornerNW, (-1, -1))); return result; }
            if (m.S && m.E) { result.Add((DecalPosition.OuterCornerSE, (1, 1))); return result; }
            if (m.S && m.W) { result.Add((DecalPosition.OuterCornerSW, (-1, 1))); return result; }

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

        if (m.NE) result.Add((DecalPosition.InnerCornerNE, (1, -1)));
        if (m.NW) result.Add((DecalPosition.InnerCornerNW, (-1, -1)));
        if (m.SE) result.Add((DecalPosition.InnerCornerSE, (1, 1)));
        if (m.SW) result.Add((DecalPosition.InnerCornerSW, (-1, 1)));

        return result;
    }

    private void AddDecalForLayerPosition(DecalLayer layer, int layerIndex, Room room, int x, int y, DecalPosition position, int ownerId, List<PlacedDecal> result)
    {
        AddDecalForLayerPositionOffset(layer, layerIndex, room, x, y, position, ownerId, result, 0f, 0f);
    }

    private void AddDecalForLayerPositionOffset(DecalLayer layer, int layerIndex, Room room, int x, int y, DecalPosition position, int ownerId, List<PlacedDecal> result, float offsetX, float offsetY)
    {
        if (string.IsNullOrEmpty(layer.SourcePackId)) return;

        var pack = _packManager.GetById(layer.SourcePackId);
        if (pack == null) return;
        if (!pack.Positions.TryGetValue(position, out var proto) || string.IsNullOrEmpty(proto)) return;

        result.Add(new PlacedDecal
        {
            X = x + 0.5f + offsetX,
            Y = y + 0.5f + offsetY,
            Proto = proto,
            Color = string.IsNullOrEmpty(layer.Color) ? pack.Color : layer.Color,
            Rotation = 0,
            Cleanable = false,
            PatternOwnerId = ownerId,
            PatternLayerIndex = layerIndex
        });
    }
}