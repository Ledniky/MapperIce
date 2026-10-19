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
/// Клетка-проход (Room.Passages) ни в один из этих источников не попадает: стена
/// на ней снесена инструментом «Проход», пол там есть — для узора это обычная
/// клетка пола (и НЕ дверь: своей декали DoorN/S/E/W у прохода нет).
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

    // Клетка вычтена из ФОРМЫ ручной области («➖ Вычесть» -> RemovedCells, либо
    // прямоугольник Subtractions): лежит внутри габаритного прямоугольника области, но
    // частью области уже не является. В режиме «Вокруг стен» такая клетка считается стеной:
    // узор обводит получившийся вырез так же, как обводил бы настоящую стену комнаты.
    // Сама клетка при этом остаётся пустой — декалей она не получает.
    private static bool IsAreaHoleCell(ManualDecalArea? area, int x, int y)
    {
        if (area == null) return false;
        if (x < area.X || x >= area.X + area.Width || y < area.Y || y >= area.Y + area.Height) return false;
        return !area.Contains(x, y);
    }

    // Клетка-проход стеной не считается: инструмент «Проход» сносит на ней стену
    // (TileBuilder пропускает её в стадии 2, а пол под ней уже лежит со стадии 1),
    // поэтому для узора это обычная клетка пола, а не клетка периметра.
    // Клетка «Убрать стену» (Room.HiddenWalls) — наоборот: TileBuilder стену на ней
    // тоже не строит, но для узора стена остаётся на месте, и периметр обводится
    // так, будто она есть.
    private static bool IsWallCell(Grid grid, Room room, int x, int y)
    {
        if (IsPassageAnywhereInGrid(grid, x, y)) return false;
        if (IsHiddenWallAnywhereInGrid(grid, x, y)) return true;

        return IsOrthogonalBoundaryWall(room, x, y) || IsConcavePinchWall(room, x, y);
    }

/// <summary>Клетка помечена инструментом «Убрать стену»: стена на ней снесена
/// (TileBuilder её не строит), но для узора декалей она остаётся стеной.</summary>
private static bool IsHiddenWallAnywhereInGrid(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
        {
            if (r.HiddenWalls.Any(w => w.X == x && w.Y == y)) return true;
        }
        return false;
    }


        // Есть ли реальный пол (не стеновое кольцо) хоть у одной комнаты грида в этой
    // клетке — используется для клетки двери: если "за дверью" пол другой комнаты,
    // сторона считается открытой (как проход), а не тупиком.
    private static bool IsFloorOfAnyRoom(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
        {
            if (r.Contains(x, y) && !IsWallCell(grid, r, x, y)) return true;
        }
        return false;
    }

private static bool IsObstacleSide(Grid grid, Room room, int x, int y)
{
    if (!room.Contains(x, y)) return true;
    if (IsWallCell(grid, room, x, y)) return true;
    if (IsDoorAnywhereInGrid(grid, x, y)) return true;
    return false;
}

/// <summary>Дверь (обычная или «свободная») — но НЕ проход: проход считается
/// обычной клеткой пола, у него нет своей декали двери.</summary>
private static bool IsDoorAnywhereInGrid(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
        {
            if (r.Doors.Any(d => d.X == x && d.Y == y)) return true;
        }
        return grid.LooseDoors.Any(d => d.X == x && d.Y == y);
    }

/// <summary>Клетка помечена инструментом «Проход» у любой комнаты грида — стена на
/// ней снесена, значит для узора декалей это пол.</summary>
private static bool IsPassageAnywhereInGrid(Grid grid, int x, int y)
    {
        foreach (var r in grid.Rooms)
        {
            if (r.Passages.Any(p => p.X == x && p.Y == y)) return true;
        }
        return false;
    }

/// <summary>Есть ли за краем комнаты настоящий проходимый тайл: пол любой комнаты
/// (не её стеновое кольцо), ручной тайл-пол или дверь. Нужен только для клетки-прохода:
/// на ней край комнаты — не край узора, если за ним продолжается пол (коридор или
/// соседняя комната). Пустота и чужое стеновое кольцо сторону НЕ открывают, иначе узор
/// заезжал бы под чужую стену. Считается по модели грида, а не по TileGrid: пересчёт
/// декалей идёт ДО UpdateTileGrid, и тайловый грид в этот момент ещё устаревший.</summary>
private static bool HasWalkableTileBeyond(Grid grid, int x, int y)
    {
        // Дверь (в т.ч. стоящая на стеновом кольце) — проход, а не стена.
        if (IsDoorAnywhereInGrid(grid, x, y)) return true;

        foreach (var r in grid.Rooms)
        {
            if (r.Contains(x, y) && !IsWallCell(grid, r, x, y)) return true;
        }

        foreach (var tile in grid.Tiles)
        {
            if (tile.X == x && tile.Y == y) return true;
        }

        return false;
    }

    private struct NeighborMask
    {
        public bool N, S, E, W, NE, NW, SE, SW;
        public int OrthoCount => (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);
    }


private static NeighborMask GetNeighborMask(Grid grid, Room room, int x, int y, bool treatDoorAsWall, ManualDecalArea? holeArea = null)
{
    bool currentIsDoor = IsDoorAnywhereInGrid(grid, x, y);
    // Проход — дыра в стене комнаты: край комнаты на нём узором не считается краем.
    // Сторону закрывают только пустота за краем и чужая стена, а не прямоугольник комнаты.
    bool currentIsPassage = !currentIsDoor && IsPassageAnywhereInGrid(grid, x, y);

    bool Obstacle(int nx, int ny)
    {
        // Вычтенная из формы области клетка — виртуальная стена: узор ставится по её
        // краю (Side/Corner/DeadEnd) точно так же, как у настоящей стены комнаты.
        if (IsAreaHoleCell(holeArea, nx, ny))
            return true;

        if (currentIsDoor && !room.Contains(nx, ny))
            return !IsFloorOfAnyRoom(grid, nx, ny);

        if (currentIsPassage && !room.Contains(nx, ny))
            return !HasWalkableTileBeyond(grid, nx, ny);

        return treatDoorAsWall
            ? IsObstacleSide(grid, room, nx, ny)
            : IsObstacleSide(grid, room, nx, ny) && !IsDoorAnywhereInGrid(grid, nx, ny);
    }

    return new NeighborMask
    {
        N = Obstacle(x, y - 1),
        S = Obstacle(x, y + 1),
        E = Obstacle(x + 1, y),
        W = Obstacle(x - 1, y),
        NE = Obstacle(x + 1, y - 1),
        NW = Obstacle(x - 1, y - 1),
        SE = Obstacle(x + 1, y + 1),
        SW = Obstacle(x - 1, y + 1),
    };
}

public void RecalculateForRoom(Grid grid, Room room)
    {
        int ownerId = room.Id;

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

    /// <summary>
    /// Подчищает «тихих» сирот: декали без PatternOwnerId (null), которые на самом деле
    /// были сгенерированы Decal Rule в старых сохранениях (до появления PatternOwnerId),
    /// но при загрузке выглядят как ручные. Такая декаль почти всегда стоит по центру
    /// тайла (X/Y = 0.5) и совпадает с позицией, которую слой комнаты генерирует сейчас.
    /// Настоящие ручные декали с левой панели могут стоять в произвольной позиции
    /// (дробные координаты, вращение) — их не трогаем.
    /// </summary>
    public void PurgeLegacyOrphanDecals(Grid grid)
    {
        if (grid.Decals.Count == 0) return;
        if (grid.Rooms.Count == 0) return;

        // Набор «легальных» позиций (X, Y), которые декальрул бы сгенерировал.
        // Проще и надёжнее: декаль по центру тайла + её Proto присутствует хотя бы
        // в одной активной позиции какого-нибудь включённого слоя комнаты.
        var legalCenters = new HashSet<(int x, int y)>();
        var legalProtos = new HashSet<string>();

        foreach (var room in grid.Rooms)
        {
            for (int i = 0; i < room.AutoDecalRule.Layers.Count; i++)
            {
                var layer = room.AutoDecalRule.Layers[i];
                if (!layer.Enabled) continue;

                var pack = !string.IsNullOrEmpty(layer.SourcePackId)
                    ? _packManager.GetById(layer.SourcePackId)
                    : null;
                if (pack == null) continue;

                foreach (var proto in pack.Positions.Values)
                    if (!string.IsNullOrEmpty(proto))
                        legalProtos.Add(proto);

                // Auto: декали на периметре комнаты и на клетках узора.
                if (layer.Mode == DecalPatternMode.Auto)
                {
                    for (int x = room.X; x < room.X + room.Width; x++)
                    {
                        for (int y = room.Y; y < room.Y + room.Height; y++)
                        {
                            if (room.RemovedCells.Contains((x, y))) continue;
                            if (IsDoorAnywhereInGrid(grid, x, y)) continue;
                            if (IsWallCell(grid, room, x, y)) continue;
                            legalCenters.Add((x, y));
                        }
                    }
                }
                else
                {
                    foreach (var area in layer.ManualAreas)
                    {
                        for (int x = area.X; x < area.X + area.Width; x++)
                        {
                            for (int y = area.Y; y < area.Y + area.Height; y++)
                            {
                                if (area.Contains(x, y) && room.Contains(x, y))
                                    legalCenters.Add((x, y));
                            }
                        }
                    }
                }
            }
        }

        if (legalCenters.Count == 0 && legalProtos.Count == 0)
            return;

        grid.Decals.RemoveAll(d =>
            d.PatternOwnerId == null &&
            legalProtos.Contains(d.Proto) &&
            legalCenters.Contains(((int)Math.Floor(d.X), (int)Math.Floor(d.Y))));
    }

    /// <summary>Пересчёт для всех комнат грида — используется после массовых операций (delete area, undo/redo, load).</summary>
    public void RecalculateAll(Grid grid)
    {
        // Миграция старых файлов: сначала подчищаем «тихих» сирот без владельца.
        PurgeLegacyOrphanDecals(grid);

        grid.Decals.RemoveAll(d => d.PatternOwnerId != null);

        foreach (var room in grid.Rooms)
        {
            int ownerId = room.Id;

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
                if (IsDoorAnywhereInGrid(grid, x, y)) continue;
                if (IsWallCell(grid, room, x, y)) continue;

                var mask = GetNeighborMask(grid, room, x, y, layer.TreatDoorAsWall);
                AddDoorFrameDecals(grid, layer, layerIndex, room, x, y, ownerId, result);
                PlacePatternDecals(grid, layer, layerIndex, room, x, y, mask, IsPassageAnywhereInGrid(grid, x, y), ownerId, result);
            }
        }

        return result;
    }

    private List<PlacedDecal> BuildAreaPatternForLayer(Grid grid, Room room, ManualDecalArea area, DecalLayer layer, int layerIndex, int ownerId)
    {
        return area.Mode == ManualAreaMode.WallRegion
            ? BuildWallRegionPattern(grid, room, area, layer, layerIndex, ownerId)
            : BuildWallsOnlyPattern(grid, room, area, layer, layerIndex, ownerId);
    }

    /// <summary>Режим 1 «Область-стена»: вся область считается сплошной стеной, обрамление
    /// рисуется по форме прямоугольника на клетках области (внутри выделенной области).</summary>
private List<PlacedDecal> BuildWallRegionPattern(Grid grid, Room room, ManualDecalArea area, DecalLayer layer, int layerIndex, int ownerId)
{
    var result = new List<PlacedDecal>();

    for (int x = area.X; x < area.X + area.Width; x++)
    {
        for (int y = area.Y; y < area.Y + area.Height; y++)
        {
            if (!area.Contains(x, y)) continue;
            if (!room.Contains(x, y)) continue;
            if (IsDoorAnywhereInGrid(grid, x, y)) continue;
            if (IsWallCell(grid, room, x, y)) continue;
            // Потайлёвая вырезка декалей (✖): на отмеченных клетках декали не рисуются,
            // а соседние клетки строятся как обычно — эта клетка просто перестаёт получать
            // свою декаль, не перестраивая форму области.
            if (area.CutCells.Contains((x, y))) continue;

            PlaceWallRegionDecals(layer, layerIndex, room, area, x, y, ownerId, result);
        }
    }

    return result;
}

    /// <summary>Расстановка декалей для режима «Область-стена»: рисуется ТОЛЬКО периметр области.
    /// «Открытой» считается сторона, сосед по которой выходит за пределы прямоугольника области
    /// (настоящие стены рядом не гасят рендер — декали ставятся на тайлы у стены, но не под
    /// саму стену). Внутренние клетки области (со всех 4 ортогональных сторон в области) не
    /// получают декалей. Углы — OuterCorner, прямые стороны — Side в направлении открытой стороны.</summary>
    private void PlaceWallRegionDecals(DecalLayer layer, int layerIndex, Room room, ManualDecalArea area, int x, int y, int ownerId, List<PlacedDecal> result)
    {
        bool InArea(int nx, int ny) => area.Contains(nx, ny);

        bool openN = !InArea(x, y - 1);
        bool openS = !InArea(x, y + 1);
        bool openE = !InArea(x + 1, y);
        bool openW = !InArea(x - 1, y);
        int openCount = (openN ? 1 : 0) + (openS ? 1 : 0) + (openE ? 1 : 0) + (openW ? 1 : 0);

        // Внутренние углы ставятся для ЛЮБОЙ клетки (не только внутренней), если диагональный
        // сосед не входит в область, а обе прилегающие к нему ортогональные стороны открыты.
        // Это даёт внутренние углы и на клетках с уже стоящей декалью (Side/OuterCorner) —
        // например, у вогнутого угла, образованного вырезанной/вычтенной клеткой. ВАЖНО:
        // та же логика, что и в авто-режиме (PlacePatternDecals) — там углы собираются до
        // ортогональной классификации, поэтому авто-режим таких багов не даёт.
        void AddInner(DecalPosition pos, int nx, int ny, bool openA, bool openB)
        {
            if (!InArea(nx, ny) && !openA && !openB)
                AddDecalForLayerPosition(layer, layerIndex, room, x, y, pos, ownerId, result);
        }

        AddInner(DecalPosition.InnerCornerNE, x + 1, y - 1, openN, openE);
        AddInner(DecalPosition.InnerCornerNW, x - 1, y - 1, openN, openW);
        AddInner(DecalPosition.InnerCornerSE, x + 1, y + 1, openS, openE);
        AddInner(DecalPosition.InnerCornerSW, x - 1, y + 1, openS, openW);

        // Внутренняя клетка: ортогональные соседи все в области — дальше классификатора нет.
        if (openCount == 0)
            return;

        void Place(DecalPosition pos) => AddDecalForLayerPosition(layer, layerIndex, room, x, y, pos, ownerId, result);

        // Углы области: две смежные открытые стороны => внешний угол в открытую сторону.
        if (openCount == 2 && openN && openE) { Place(DecalPosition.OuterCornerNE); return; }
        if (openCount == 2 && openN && openW) { Place(DecalPosition.OuterCornerNW); return; }
        if (openCount == 2 && openS && openE) { Place(DecalPosition.OuterCornerSE); return; }
        if (openCount == 2 && openS && openW) { Place(DecalPosition.OuterCornerSW); return; }

        // Прямые стороны: Side направлена в сторону открытого соседа (смотрит наружу).
        if (openN) Place(DecalPosition.SideN);
        if (openS) Place(DecalPosition.SideS);
        if (openE) Place(DecalPosition.SideE);
        if (openW) Place(DecalPosition.SideW);
    }

    /// <summary>Режим 2 «Вокруг стен»: обычный узор вдоль настоящих стен комнаты, но выводятся
    /// только декали, попадающие внутрь области (без виртуальной стены по границе области).
    /// Клетки, вычтенные из формы области («➖ Вычесть», RemovedCells, и прямоугольники
    /// Subtractions), считаются стеной: узор обводит вырез, а не просто пропускает клетку.</summary>
private List<PlacedDecal> BuildWallsOnlyPattern(Grid grid, Room room, ManualDecalArea area, DecalLayer layer, int layerIndex, int ownerId)
{
    var result = new List<PlacedDecal>();

    for (int x = area.X; x < area.X + area.Width; x++)
    {
        for (int y = area.Y; y < area.Y + area.Height; y++)
        {
            if (!area.Contains(x, y)) continue;
            if (!room.Contains(x, y)) continue;
            if (room.RemovedCells.Contains((x, y))) continue;
            if (IsDoorAnywhereInGrid(grid, x, y)) continue;
            if (IsWallCell(grid, room, x, y)) continue;
            // Потайлёвая вырезка декалей (✖): на отмеченных клетках декали не рисуются,
            // а соседние клетки строятся как обычно — эта клетка просто перестаёт получать
            // свою декаль, не перестраивая форму области.
            if (area.CutCells.Contains((x, y))) continue;

            // holeArea: вычтенная из формы клетка выступает стеной для маски соседей.
            var mask = GetNeighborMask(grid, room, x, y, layer.TreatDoorAsWall, area);
            AddDoorFrameDecals(grid, layer, layerIndex, room, x, y, ownerId, result);
            PlacePatternDecals(grid, layer, layerIndex, room, x, y, mask, IsPassageAnywhereInGrid(grid, x, y), ownerId, result);
        }
    }

    return result;
}

    /// <summary>Декали двери DoorN/S/E/W ставятся на КЛЕТКУ-РАМКУ рядом с дверью (сама дверь
    /// была пропущена через continue, поэтому на двери декали нет).</summary>
    private void AddDoorFrameDecals(Grid grid, DecalLayer layer, int layerIndex, Room room, int x, int y, int ownerId, List<PlacedDecal> result)
    {
        if (layer.TreatDoorAsWall) return;

        if (IsDoorAnywhereInGrid(grid, x, y - 1))
            AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.DoorN, ownerId, result);
        if (IsDoorAnywhereInGrid(grid, x, y + 1))
            AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.DoorS, ownerId, result);
        if (IsDoorAnywhereInGrid(grid, x + 1, y))
            AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.DoorE, ownerId, result);
        if (IsDoorAnywhereInGrid(grid, x - 1, y))
            AddDecalForLayerPosition(layer, layerIndex, room, x, y, DecalPosition.DoorW, ownerId, result);
    }

    /// <summary>Расстановка декалей по классификации маски соседей (Side/Corner/DeadEnd/Inner).</summary>
    private void PlacePatternDecals(Grid grid, DecalLayer layer, int layerIndex, Room room, int x, int y, NeighborMask mask, bool isPassage, int ownerId, List<PlacedDecal> result)
    {
        foreach (var (position, cornerDir) in ClassifyPositions(mask, isPassage))
        {
            if (position is DecalPosition.InnerCornerNE or DecalPosition.InnerCornerNW
                or DecalPosition.InnerCornerSE or DecalPosition.InnerCornerSW)
            {
                int targetX = x + (int)Math.Round(-cornerDir.dx * InnerCornerOffset);
                int targetY = y + (int)Math.Round(-cornerDir.dy * InnerCornerOffset);

                bool targetIsFloor = room.Contains(targetX, targetY) && !IsWallCell(grid, room, targetX, targetY);

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


    /// <summary>
    /// Возвращает СПИСОК позиций для клетки (обычно один элемент, но для узкого коридора
    /// в 1 тайл — стены сразу с двух ПРОТИВОПОЛОЖНЫХ сторон, N+S или E+W — возвращает
    /// два элемента).
    /// </summary>
    /// <param name="suppressDeadEnd">true для клетки-прохода: тупиковую заглушку
    /// (DeadEnd) на проходе не ставим никогда — проход по определению не тупик, и в
    /// проходе шириной 1 тайл заглушка ставилась из-за того, что край комнаты считался
    /// концом узора.</param>
    private static List<(DecalPosition position, (int dx, int dy) cornerDir)> ClassifyPositions(NeighborMask m, bool suppressDeadEnd = false)
    {
        var result = new List<(DecalPosition, (int dx, int dy))>();
        int ortho = m.OrthoCount;

        // Внутренние углы — ставим по каждой диагонали независимо от ортогональной
        // классификации, НО только когда обе ортогональные стороны, прилегающие к этой
        // диагонали, открыты (пол, не стена). Условие и есть "нет стены в 4 соседях
        // вокруг этого угла": если рядом с диагональю есть ортогональная стена, то это
        // уже внешний угол/тупик/прямая сторона, а не внутренний угол посреди пола.
        // Пример: стена только слева (W) — внутренние углы ставятся на диагоналях SE и NE
        // (прилегающие S/E и N/E открыты), а на SW/NW — нет (прилегает стена W).
        if (m.NE && !m.N && !m.E) result.Add((DecalPosition.InnerCornerNE, (1, -1)));
        if (m.NW && !m.N && !m.W) result.Add((DecalPosition.InnerCornerNW, (-1, -1)));
        if (m.SE && !m.S && !m.E) result.Add((DecalPosition.InnerCornerSE, (1, 1)));
        if (m.SW && !m.S && !m.W) result.Add((DecalPosition.InnerCornerSW, (-1, 1)));

        switch (ortho)
        {
            case 3:
                // На проходе тупиковую заглушку не ставим вовсе: если после открытия
                // стороны за краем комнаты ortho всё равно 3, лучшей позиции нет —
                // пусть клетка останется без декали, а не с «тупиком».
                if (suppressDeadEnd) break;
                if (!m.N) result.Add((DecalPosition.DeadEndS, (0, 0)));
                else if (!m.S) result.Add((DecalPosition.DeadEndN, (0, 0)));
                else if (!m.E) result.Add((DecalPosition.DeadEndW, (0, 0)));
                else result.Add((DecalPosition.DeadEndE, (0, 0)));
                break;

            case 2:
                if (m.N && m.E) { result.Add((DecalPosition.OuterCornerNE, (1, -1))); break; }
                if (m.N && m.W) { result.Add((DecalPosition.OuterCornerNW, (-1, -1))); break; }
                if (m.S && m.E) { result.Add((DecalPosition.OuterCornerSE, (1, 1))); break; }
                if (m.S && m.W) { result.Add((DecalPosition.OuterCornerSW, (-1, 1))); break; }

                if (m.N && m.S) { result.Add((DecalPosition.SideN, (0, 0))); result.Add((DecalPosition.SideS, (0, 0))); break; }
                if (m.E && m.W) { result.Add((DecalPosition.SideE, (0, 0))); result.Add((DecalPosition.SideW, (0, 0))); break; }
                break;

            case 1:
                if (m.N) result.Add((DecalPosition.SideN, (0, 0)));
                else if (m.S) result.Add((DecalPosition.SideS, (0, 0)));
                else if (m.E) result.Add((DecalPosition.SideE, (0, 0)));
                else result.Add((DecalPosition.SideW, (0, 0)));
                break;

            default:
                // ortho == 0 — клетка с ПУСТЫМИ ортогональными соседями: тут не добавляется
                // никакой ортогональной позиции, только внутренние углы (они уже собраны выше).
                break;
        }

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