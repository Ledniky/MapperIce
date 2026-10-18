// Services/Renderer.FullMapRender.cs
// Экспорт всей карты в PNG (без интерфейса): пол, стены, двери, декали и сущности,
// а также трубы/провода — как реальные спрайты из репозитория. Аналитические
// оверлеи (цвет комнат, связи сигнализаций, обзор камер, линии труб/проводов,
// сети) в экспорт не попадают — только сами объекты карты.
using MapperIce.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapperIce.Services;

public partial class Renderer
{
    /// <summary>
    /// Рендерит все видимые слои карты целиком в новый Bitmap (1 тайл = Constants.TILE_SIZE
    /// пикселей). Возвращает null, если на карте ничего нет.
    /// </summary>
    public Bitmap? RenderFullMapPng(MapData map)
    {
        if (map == null) return null;

        int tileSize = Constants.TILE_SIZE;

        var visibleGrids = map.Grids.Where(g => g.IsVisible).ToList();
        if (visibleGrids.Count == 0) return null;

        // Строим TileGrid для каждого видимого слоя (переиспользуем кэш, если он не устарел).
        var tileGrids = new Dictionary<int, TileGrid>();
        foreach (var grid in visibleGrids)
        {
            bool needsRebuild = !_tileGridCache.TryGetValue(grid.Uid, out var tg) ||
                                _dirtyTileGrids.Contains(grid.Uid);
            if (needsRebuild)
            {
                tg = _tileBuilder.BuildFromRooms(grid, tg);
                _tileGridCache[grid.Uid] = tg;
                _dirtyTileGrids.Remove(grid.Uid);
            }
            tileGrids[grid.Uid] = tg!;
        }

        // Собираем объединённую мировую область всей карты (с учётом автосмещения слоёв).
        const int marginPx = 32;
        float minWorldX = float.MaxValue, minWorldY = float.MaxValue;
        float maxWorldX = float.MinValue, maxWorldY = float.MinValue;

        bool anyContent = false;
        for (int gIdx = 0; gIdx < map.Grids.Count; gIdx++)
        {
            var grid = map.Grids[gIdx];
            if (!grid.IsVisible) continue;
            AdvanceBounds(grid, tileGrids[grid.Uid], gIdx, ref minWorldX, ref minWorldY, ref maxWorldX, ref maxWorldY, ref anyContent);
        }

        if (!anyContent) return null;

        // Всё смещаем в положительные координаты: верхний левый мировой угол -> отступ.
        float globalSpanX = (maxWorldX - minWorldX) * (tileSize + 0f);
        float globalSpanY = (maxWorldY - minWorldY) * (tileSize + 0f);
        int outWidth = Math.Max(1, (int)Math.Ceiling(globalSpanX) + marginPx * 2);
        int outHeight = Math.Max(1, (int)Math.Ceiling(globalSpanY) + marginPx * 2);

        var output = new Bitmap(outWidth, outHeight, PixelFormat.Format32bppArgb);
        // ToRect воспринимает viewOffset в ПИКСЕЛЯХ ((world + gridOffset) * tileSize - viewOffset),
        // поэтому мировые координаты верхнего левого угла умножаем на tileSize.
        var viewOffset = new PointF(minWorldX * tileSize - marginPx, minWorldY * tileSize - marginPx);

        using (var g = Graphics.FromImage(output))
        {
            g.InterpolationMode = _interpolationMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(BackgroundColor);

            // На время экспорта выключаем все аналитические оверлеи.
            bool savedHideRoom = HideRoomOverlay;
            bool savedPipes = ShowPipeOverlay;
            bool savedWires = ShowWires;
            bool savedAlarm = ShowAlarmConnections;
            bool savedCamera = ShowCameraAreas;
            bool savedLinks = ShowNetworkLinks;
            bool savedDivider = ShowDividerAreas;
            HideRoomOverlay = true;
            ShowPipeOverlay = false;   // концы труб к нам придём вручную как спрайты
            ShowWires = false;
            ShowAlarmConnections = false;
            ShowCameraAreas = false;
            ShowNetworkLinks = false;
            ShowDividerAreas = false;
            NetworkHighlightPositions = new HashSet<(int x, int y)>();

            try
            {
                for (int gIdx = 0; gIdx < map.Grids.Count; gIdx++)
                {
                    var grid = map.Grids[gIdx];
                    if (!grid.IsVisible) continue;

                    float layerOffsetY = Grid.GetLayerOffsetY(gIdx);
                    var gridOffset = new PointF(grid.Position.X, grid.Position.Y + layerOffsetY);

                    // Область рендера текущего слоя во всех мировых координатах — вся карта.
                    var visibleRect = BuildFullVisibleRect(viewOffset, outWidth, outHeight, tileSize, gridOffset);

                    // Весь tlener замысловатый рендер слоя (пол/стены/двери/декали/сущности),
                    // без оверлеев (флаги выше выключены).
                    DrawRenderLayer(g, tileGrids[grid.Uid], grid, tileSize, viewOffset, gridOffset, 1.0f, visibleRect);

                    // Трубы и провода — реальные спрайты из репозитория (композицию пол/стены
                    // они не перекрывают как линии-оверлеи — каждый узел рисуется своим спрайтом).
                    DrawPipeSprites(g, grid, tileSize, viewOffset, gridOffset, visibleRect);
                    DrawWireSprites(g, grid, tileSize, viewOffset, gridOffset, visibleRect);
                }
            }
            finally
            {
                HideRoomOverlay = savedHideRoom;
                ShowPipeOverlay = savedPipes;
                ShowWires = savedWires;
                ShowAlarmConnections = savedAlarm;
                ShowCameraAreas = savedCamera;
                ShowNetworkLinks = savedLinks;
                ShowDividerAreas = savedDivider;
                NetworkHighlightPositions = new HashSet<(int x, int y)>();
            }
        }

        return output;
    }

    /// <summary>
    /// Расширяет объединённую мировую область грида содержимым конкретного слоя:
    /// тайлы, комнаты, двери, сущности, декали.
    /// </summary>
    private static void AdvanceBounds(Grid grid, TileGrid tileGrid, int gIdx,
        ref float minWX, ref float minWY, ref float maxWX, ref float maxWY, ref bool any)
    {
        float offX = grid.Position.X;
        float offY = grid.Position.Y + Grid.GetLayerOffsetY(gIdx);

        foreach (var tile in tileGrid.GetAllTiles())
            ConsiderBound(tile.X + offX, tile.Y + offY + 1f, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
        foreach (var placed in grid.Tiles)
            ConsiderBound(placed.X + offX, placed.Y + offY + 1f, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
        foreach (var room in grid.Rooms)
        {
            ConsiderBound(room.X + offX, room.Y + offY, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
            ConsiderBound(room.X + room.Width + offX, room.Y + room.Height + offY, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
        }
        foreach (var door in grid.LooseDoors)
            ConsiderBound(door.X + offX, door.Y + offY + 1f, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
        foreach (var e in grid.Entities)
            ConsiderBound(e.X + offX, e.Y + offY + 1f, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
        foreach (var d in grid.Decals)
            ConsiderBound(d.X + offX, d.Y + offY + 1f, ref minWX, ref minWY, ref maxWX, ref maxWY, ref any);
    }

    private static void ConsiderBound(float wx, float wy,
        ref float minWX, ref float minWY, ref float maxWX, ref float maxWY, ref bool any)
    {
        any = true;
        if (wx < minWX) minWX = wx;
        if (wy < minWY) minWY = wy;
        if (wx > maxWX) maxWX = wx;
        if (wy > maxWY) maxWY = wy;
    }

    /// <summary>
    /// Видимая область рендера для экспорта: весь выходной bitmap без запаса (запасной
    /// тайл внутри марджина уже учтён в выходных размерах).
    /// </summary>
    private static RectangleF BuildFullVisibleRect(PointF viewOffset, int bufW, int bufH, int tileSize, PointF gridOffset)
    {
        float left = viewOffset.X / tileSize - gridOffset.X;
        float top = viewOffset.Y / tileSize - gridOffset.Y;
        float right = (viewOffset.X + bufW) / tileSize - gridOffset.X;
        float bottom = (viewOffset.Y + bufH) / tileSize - gridOffset.Y;
        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Рисует все трубы грида реальными спрайтами прототипов из репозитория (как при
    /// экспорте в YAML), а не линиями-оверлеями. Утилизация (Util) — DisposalPipe*,
    /// обычные слои — GasPipe*Alt1/Alt2 по топологии соседей.
    /// </summary>
    private void DrawPipeSprites(Graphics g, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        if (pipes.Count == 0) return;
        pipes = pipes.Where(p => IsPointVisible(p.X, p.Y, visibleRect)).ToList();
        if (pipes.Count == 0) return;

        foreach (var group in pipes.GroupBy(p => p.PipeType))
        {
            var groupList = group.ToList();
            foreach (var pipe in groupList)
            {
                var neighbors = YAMLGenerator.GetNeighbors(groupList, (int)pipe.X, (int)pipe.Y);

                if (pipe.PipeType == "Util")
                {
                    string proto = YAMLGenerator.GetDisposalProto(neighbors, pipe.UtilArrowRotation, pipe.CustomColor, out float rotation);
                    bool isMarked = pipe.HasFilterMarker || !string.IsNullOrEmpty(pipe.FilterLabel);
                    if (isMarked && proto == "DisposalPipe")
                        proto = "DisposalTagger";
                    DrawSingleEntity(g, proto, pipe.X, pipe.Y, rotation, tileSize, viewOffset, gridOffset);
                    continue;
                }

                string suffix = pipe.PipeType switch
                {
                    "Distra" => "Alt2",
                    "Waste" => "Alt1",
                    _ => ""
                };
                string protoName = YAMLGenerator.GetPipeProto(suffix, neighbors);
                float rot = YAMLGenerator.GetPipeRotation(neighbors);
                DrawSingleEntity(g, protoName, pipe.X, pipe.Y, rot, tileSize, viewOffset, gridOffset);
            }
        }
    }

    /// <summary>
    /// Рисует все провода грида реальными спрайтами кабелей (CableHV/CableMV/
    /// CableApcExtension) — как сущности, а не линии-оверлеи.
    /// </summary>
    private void DrawWireSprites(Graphics g, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        // Провода инструмента в экспорте PNG рисуются как раньше — обычным
        // статическим спрайтом прототипа (без подбора стейта по соседям).
        var wires = grid.Entities.OfType<WireEntity>()
            .Where(w => IsPointVisible(w.X, w.Y, visibleRect))
            .ToList();
        if (wires.Count == 0) return;

        foreach (var wire in wires)
        {
            string? proto = WireBuilder.GetCableProtoForWireType(wire.WireType);
            if (proto == null) continue;
            DrawSingleEntity(g, proto, wire.X, wire.Y, 0f, tileSize, viewOffset, gridOffset, cableSmoothing: false);
        }
    }
}