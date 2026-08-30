// Services/Renderer.cs
using MapperIce.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private readonly PrototypeIndexer? _indexer;
    private readonly TileBuilder _tileBuilder;
    private readonly PipeBuilder _pipeBuilder;
    private readonly string _rootPath = "";
    public bool HideRoomOverlay { get; set; } = false;
    private MapData? _currentMap;
    public bool ShowPipeOverlay { get; set; } = true;

    // Кэш текстур для ускорения рендеринга
    // Кэш текстур для ускорения рендеринга
    private readonly Dictionary<string, Image?> _textureCache = new();
    private readonly Dictionary<string, Rectangle> _sourceRectCache = new();
    private readonly Dictionary<string, string> _protoTextureDirCache = new();
    private readonly Dictionary<string, Size> _rsiFrameSizeCache = new();
    private readonly Dictionary<string, string> _protoStateNameCache = new();
    private readonly Dictionary<string, Dictionary<string, (int directions, int framesPerDirection)>> _rsiStateDirectionsCache = new();
    private readonly HashSet<int> _dirtyTileGrids = new();
    private readonly Dictionary<int, TileGrid> _tileGridCache = new();

    // Размеры тайлов в пикселях для кэширования
    private int _cachedTileSize = 0;

    // Интерполяция для масштабирования
    private readonly InterpolationMode _interpolationMode = InterpolationMode.NearestNeighbor;
    private AlarmNetwork? _currentNetwork;
    public bool ShowAlarmConnections { get; set; } = true;

    // Предпросмотр сигнализации
    private bool _showAlarmPreview = false;
    private int _previewX;
    private int _previewY;
    private float _previewRotation;
    private string _previewType = "";
    private bool _showEntityPreview = false;
    private float _previewEntityX;
    private float _previewEntityY;
    private float _previewEntityRotation;
    private string _previewEntityProto = "";
    private string? _previewDecalColor = null; // не null только когда превью — это декаль
    private List<object> _selection = new();
    private bool _showSelectionBox = false;
    private Point _selectionBoxStart;
    private Point _selectionBoxEnd;

    private (int x, int y, int w, int h)? _decalAreaEditRect = null;

    /// <summary>
    /// Проверяет, запрещён ли поворот для данного прототипа (noRot: true)
    /// или любого из его родителей.
    /// </summary>
    private bool IsPrototypeNoRotate(string protoId)
    {
        if (_indexer == null) return false;
        return _indexer.FindPrototypeNoRotate(protoId);
    }

    public void SetDecalAreaEditRect(int x, int y, int w, int h)
    {
        _decalAreaEditRect = (x, y, w, h);
    }

    public void ClearDecalAreaEditRect()
    {
        _decalAreaEditRect = null;
    }
    public void SetSelectionBox(Point start, Point end)
    {
        _selectionBoxStart = start;
        _selectionBoxEnd = end;
        _showSelectionBox = true;
    }

    public void ClearSelectionBox()
    {
        _showSelectionBox = false;
    }


    public void SetSelection(List<object> selection)
    {
        _selection = selection;
    }

    public Renderer(int width, int height, PrototypeIndexer? indexer, TileBuilder tileBuilder, PipeBuilder pipeBuilder)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
        _tileBuilder = tileBuilder;
        _pipeBuilder = pipeBuilder;
        if (_indexer != null)
            _rootPath = _indexer.GetRootPath();
    }

    public void Resize(int width, int height)
    {
        lock (_lock)
        {
            if (width > 0 && height > 0)
            {
                _buffer = new Bitmap(width, height);
                _cachedTileSize = 0;
            }
        }
    }

    public Bitmap Render(MapData map, float scale, PointF viewOffset, Room? currentRoom, string toolName)
    {
        _currentMap = map;

        lock (_lock)
        {
            if (_buffer.Width == 0 || _buffer.Height == 0) return _buffer;

            using var g = Graphics.FromImage(_buffer);

            g.InterpolationMode = _interpolationMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.Clear(Color.White);

            int tileSize = (int)(Constants.TILE_SIZE * scale);

            if (_cachedTileSize != tileSize)
            {
                _cachedTileSize = tileSize;
            }

            var visibleGrids = map.Grids.Where(g => g.IsVisible).ToList();

            // Индекс активного слоя для скрытия слоёв выше него
            int activeGridIndex = -1;
            if (map.ActiveGrid != null)
            {
                activeGridIndex = visibleGrids.FindIndex(g => g.Uid == map.ActiveGrid.Uid);
            }

            foreach (var grid in visibleGrids)
            {
                // Скрываем слои, идущие выше активного
                int gridIndex = visibleGrids.IndexOf(grid);
                if (gridIndex > activeGridIndex)
                    continue;

                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;

                // Смещение грида: Position + автоматическое смещение слоя по Y
                float layerOffsetY = Grid.GetLayerOffsetY(gridIndex);
                var gridOffset = new PointF(grid.Position.X, grid.Position.Y + layerOffsetY);

                bool needsRebuild = !_tileGridCache.TryGetValue(grid.Uid, out var tileGrid) ||
                                      _dirtyTileGrids.Contains(grid.Uid);
                if (needsRebuild)
                {
                    tileGrid = _tileBuilder.BuildFromRooms(grid, tileGrid);
                    _tileGridCache[grid.Uid] = tileGrid;
                    _dirtyTileGrids.Remove(grid.Uid);
                }

                // Видимая на экране область в мировых координатах — режем работу до того,
                // что реально видно, вместо полного прогона по всей карте на каждый кадр
                var visibleRect = GetVisibleWorldRect(tileSize, viewOffset, gridOffset);

                var floorTiles = tileGrid.GetTilesByContent(TileContent.Floor)
                    .Where(t => IsTileVisible(t.X, t.Y, visibleRect))
                    .ToList();
                var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall)
                    .Where(t => IsTileVisible(t.X, t.Y, visibleRect))
                    .ToList();
                var doorTiles = tileGrid.GetTilesByContent(TileContent.Door)
                    .Where(t => IsTileVisible(t.X, t.Y, visibleRect))
                    .ToList();

                DrawGrid(g, tileSize, viewOffset, gridOffset, opacity);

                DrawFloorTilesBatch(g, floorTiles, tileSize, viewOffset, gridOffset, opacity);

                var floorUnderDoors = doorTiles
                    .Where(t => t.HasFloorUnder && !string.IsNullOrEmpty(t.FloorProtoUnder))
                    .Select(t => new TileData
                    {
                        X = t.X,
                        Y = t.Y,
                        Content = TileContent.Floor,
                        ProtoId = t.FloorProtoUnder ?? "Plating"
                    })
                    .ToList();
                DrawFloorTilesBatch(g, floorUnderDoors, tileSize, viewOffset, gridOffset, opacity);

                var visibleDecals = grid.Decals
                    .Where(d => IsPointVisible(d.X, d.Y, visibleRect))
                    .OrderBy(d => d.Y)
                    .ToList();
                DrawDecalsBatch(g, visibleDecals, tileSize, viewOffset, gridOffset, opacity);

                DrawWallTilesBatch(g, wallTiles, tileGrid, tileSize, viewOffset, gridOffset, opacity);

                DrawDoorTilesBatch(g, doorTiles, tileSize, viewOffset, gridOffset);

                var firelocks = grid.Entities.OfType<FirelockEntity>()
                    .Where(f => IsPointVisible(f.X, f.Y, visibleRect))
                    .OrderBy(f => f.Y)
                    .ToList();
                DrawFirelocksBatch(g, firelocks, tileSize, viewOffset, gridOffset);

                var genericEntities = grid.Entities
                    .Where(e => e is not PipeEntity && e is not FirelockEntity &&
                                e is not AirAlarmEntity && e is not FireAlarmEntity)
                    .Where(e => IsPointVisible(e.X, e.Y, visibleRect))
                    .Select(e => new MapEntity { Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid, Rotation = e.Rotation })
                    .OrderBy(e => e.Y)
                    .ToList();
                DrawGenericEntitiesBatch(g, genericEntities, tileSize, viewOffset, gridOffset);

                if (!HideRoomOverlay)
                {
                    var rooms = grid.Rooms
                        .Where(r => RoomOverlapsRect(r, visibleRect))
                        .ToList();
                    DrawRoomFillsBatch(g, rooms, tileSize, viewOffset, gridOffset, opacity);

                    if (currentRoom != null && isActive)
                    {
                        if (toolName == "SubtractRoom")
                        {
                            DrawSubtractPreview(g, currentRoom, tileSize, viewOffset, gridOffset);
                        }
                        else if (toolName == "RestoreRoom")
                        {
                            DrawRestorePreview(g, currentRoom, tileSize, viewOffset, gridOffset);
                        }
                        else
                        {
                            DrawRoomFill(g, currentRoom, tileSize, viewOffset, gridOffset, 1.0f);
                            DrawRoomLine(g, currentRoom, tileSize, viewOffset, gridOffset, true, 1.0f);
                        }
                    }
                    else
                    {
                        DrawRoomLinesBatch(g, rooms, tileSize, viewOffset, gridOffset, false, opacity);
                    }
                }

                if (ShowPipeOverlay)
                {
                    var allPipes = _pipeBuilder.GetPipes(grid)
                        .Where(p => IsPointVisible(p.X, p.Y, visibleRect))
                        .OrderBy(p => p.Y)
                        .ToList();
                    DrawPipeLinesBatch(g, allPipes, tileSize, viewOffset, gridOffset);

                    if (allPipes.Count > 0)
                    {
                        DrawPipeDotsBatch(g, allPipes, tileSize, viewOffset, gridOffset);
                    }

                    if (_pipeBuilder.IsDrawing && _pipeBuilder.StartPoint.HasValue)
                    {
                        var start = _pipeBuilder.StartPoint.Value;
                        var end = _pipeBuilder.EndPoint ?? start;
                        var path = CalculatePipePath(start, end);
                        DrawTempPipePath(g, path, tileSize, viewOffset, gridOffset);
                    }
                }

                var airAlarms = grid.Entities.OfType<AirAlarmEntity>()
                    .Where(a => IsPointVisible(a.X, a.Y, visibleRect))
                    .OrderBy(a => a.Y)
                    .ToList();
                var fireAlarms = grid.Entities.OfType<FireAlarmEntity>()
                    .Where(a => IsPointVisible(a.X, a.Y, visibleRect))
                    .OrderBy(a => a.Y)
                    .ToList();

                DrawAlarmsBatch(g, airAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, gridOffset, "AirAlarm", Color.FromArgb(200, 255, 200, 100));
                DrawAlarmsBatch(g, fireAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, gridOffset, "FireAlarm", Color.FromArgb(200, 255, 100, 100));

                if (ShowAlarmConnections && ShowPipeOverlay && _currentNetwork != null)
                {
                    DrawAlarmConnections(g, _currentNetwork, tileSize, viewOffset, gridOffset, visibleRect);
                }

                // Рисуем стрелки направления у существующих сигнализаций — переиспользуем
                // уже отфильтрованные по видимой области airAlarms/fireAlarms (см. выше),
                // а не сканируем весь grid.Entities заново
                var visibleAlarmsForArrows = airAlarms.Cast<MapEntity>().Concat(fireAlarms).ToList();
                DrawAlarmDirectionArrows(g, visibleAlarmsForArrows, scale, viewOffset, gridOffset);
            }


            DrawInfo(g, scale, toolName, map);

            // Рисуем предпросмотр сигнализации под курсором
            DrawAlarmPreview(g, scale, viewOffset);

            // Рисуем предпросмотр размещаемого прототипа под курсором
            DrawEntityPreview(g, scale, viewOffset);
            DrawSelectionHighlight(g, scale, viewOffset);
            DrawSelectionBox(g);
            DrawDecalAreaEditOverlay(g, scale, viewOffset);

            return _buffer;
        }
    }

    #region Общие хелперы координат/отрисовки (новое — заменяют дублировавшуюся логику в *Batch методах)

    /// <summary>
    /// Экранный прямоугольник тайла tileSize×tileSize для мировой позиции (worldX, worldY).
    /// offsetTiles* сдвигает якорь на долю тайла: 0 — левый верхний угол клетки (как у пола/стен/дверей/огнешлюзов),
    /// -0.5 — центр клетки (как у декалей/generic-сущностей/превью, чьи X/Y — уже дробные мировые координаты).
    /// </summary>
    private static Rectangle ToRect(float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset,
        float offsetTilesX = 0f, float offsetTilesY = 0f)
    {
        float sx = (worldX + offsetTilesX + gridOffset.X) * tileSize - viewOffset.X;
        float sy = (worldY + offsetTilesY + gridOffset.Y) * tileSize - viewOffset.Y;
        return new Rectangle((int)sx, (int)sy, tileSize, tileSize);
    }


    /// <summary>
    /// Видимая на экране область в МИРОВЫХ координатах текущего грида (тайлы), с запасом
    /// в 2 тайла за краями экрана (чтобы объекты не "выскакивали" резко при малейшей
    /// прокрутке). Используется, чтобы не гонять полный цикл отрисовки (текстура,
    /// поворот, тонирование) по объектам, которых всё равно не видно — критично для
    /// карт с десятками тысяч тайлов/сущностей/декалей.
    /// </summary>
    private RectangleF GetVisibleWorldRect(int tileSize, PointF viewOffset, PointF gridOffset)
    {
        const float marginTiles = 2f;

        float left = viewOffset.X / tileSize - gridOffset.X - marginTiles;
        float top = viewOffset.Y / tileSize - gridOffset.Y - marginTiles;
        float right = (viewOffset.X + _buffer.Width) / tileSize - gridOffset.X + marginTiles;
        float bottom = (viewOffset.Y + _buffer.Height) / tileSize - gridOffset.Y + marginTiles;

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    // Тайл (целые X,Y, занимает клетку [x, x+1) x [y, y+1)) пересекается с видимой областью
    private static bool IsTileVisible(int x, int y, RectangleF visibleRect)
    {
        return x + 1 >= visibleRect.Left && x <= visibleRect.Right &&
               y + 1 >= visibleRect.Top && y <= visibleRect.Bottom;
    }

    // Точечный объект (декаль/сущность/труба/сигнализация — дробные мировые координаты центра)
    private static bool IsPointVisible(float x, float y, RectangleF visibleRect)
    {
        return x >= visibleRect.Left && x <= visibleRect.Right &&
               y >= visibleRect.Top && y <= visibleRect.Bottom;
    }

    // Прямоугольник комнаты пересекается с видимой областью (для заливки/обводки комнат)
    private static bool RoomOverlapsRect(Room room, RectangleF visibleRect)
    {
        return room.X < visibleRect.Right && room.X + room.Width > visibleRect.Left &&
               room.Y < visibleRect.Bottom && room.Y + room.Height > visibleRect.Top;
    }


    /// <summary>
    /// Выполняет draw() с временным поворотом g.Transform вокруг точки (cx, cy) на rotation радиан,
    /// затем гарантированно восстанавливает исходный Transform. При rotation == 0 поворот не применяется.
    /// </summary>
    private static void WithRotation(Graphics g, float cx, float cy, float rotation, Action draw)
    {
        if (rotation == 0)
        {
            draw();
            return;
        }

        var old = g.Transform;
        var matrix = new Matrix();
        matrix.RotateAt(rotation * 180 / (float)Math.PI, new PointF(cx, cy));
        g.Transform = matrix;
        try { draw(); }
        finally { g.Transform = old; }
    }

    // Стандартное разрешение одного RSI-кадра в игре: 32×32 пикселя = ровно 1 тайл.
    // Кадр, чей реальный пиксельный размер (src, взят из GetRsiFrameSize/meta.json
    // "size") кратен этому числу больше единицы, должен занимать несколько тайлов
    // на экране и намеренно "наползать" на соседние клетки — не сжиматься в одну.
    private const int RsiBaseTexelsPerTile = 32;

    private void DrawPreservingAspect(Graphics g, Image texture, Rectangle rect, Rectangle src)
    {
        // Пустой srcRect — ничего не рисуем (текстура не загрузилась)
        if (src.Width == 0 || src.Height == 0) return;

        // Масштаб считаем НЕЗАВИСИМО по каждой оси от реального размера кадра
        // относительно стандартных 32×32 px/тайл — а не от соотношения сторон
        // между собой (как было раньше). Благодаря этому:
        // - 32×32  → 1×1 тайл (без изменений, обычный случай);
        // - 32×64  → 1×2 тайла (как и раньше, высокие стены и т.п.);
        // - 64×64  → 2×2 тайла (раньше ошибочно сжимался в 1×1 — отсюда были
        //   вдвое уменьшенные лестницы и подобные крупные объекты);
        // - 64×128 → 2×4 тайла и т.д. — любой размер экстрагируется "как есть".
        float tilesWide = (float)src.Width / RsiBaseTexelsPerTile;
        float tilesHigh = (float)src.Height / RsiBaseTexelsPerTile;

        int drawW = Math.Max(1, (int)Math.Round(rect.Width * tilesWide));
        int drawH = Math.Max(1, (int)Math.Round(rect.Height * tilesHigh));

        // Спрайт центрируется на исходном тайле (том, куда фактически поставлен
        // объект) и может выходить за его границы во все стороны — точно так же,
        // как раньше это уже работало для высоких неквадратных спрайтов.
        int drawX = rect.X + (rect.Width - drawW) / 2;
        int drawY = rect.Y + (rect.Height - drawH) / 2;
        g.DrawImage(texture, new Rectangle(drawX, drawY, drawW, drawH), src, GraphicsUnit.Pixel);
    }
    /// <summary>
    /// Рисует текстуру прототипа в rect (с опциональным тонированием tint), либо, если текстуры
    /// нет, вызывает fallback(g, rect) — там уже своя заглушка (цвет/эмодзи/рамка), т.к. она у
    /// каждого типа объекта своя.
    /// </summary>
    private void DrawTexturedRect(Graphics g, string? protoId, Rectangle rect, ImageAttributes? tint, Action<Graphics, Rectangle>? fallback, float rotation = 0f)
    {
        Image? texture = GetOrLoadTexture(protoId ?? "");
        if (texture != null)
        {
            // Sprite.offset задан для южной (rotation=0) ориентации. Поворачиваем
            // вектор на текущий rotation сущности (юг=0°,восток=90°,север=180°,
            // запад=270° — та же конвенция угла, что и везде в проекте), затем
            // зеркалим Y при переводе в экранные координаты (игровой север = "вверх"
            // на экране = отрицательный screen Y — тот же game->screen флип, что и в
            // YAMLGenerator: posY = -Y + ...). При такой формуле "восток" на 90°
            // визуально уводит смещение в "запад" экрана и наоборот — так и должно
            // быть по факту наблюдаемого поведения игры.
            var (offX, offY) = _indexer?.GetSpriteOffset(protoId ?? "") ?? (0f, 0f);
            if (offX != 0f || offY != 0f)
            {
                float cosR = (float)Math.Cos(rotation);
                float sinR = (float)Math.Sin(rotation);
                float rotatedX = offX * cosR - offY * sinR;
                float rotatedY = offX * sinR + offY * cosR;

                int pixelDX = (int)Math.Round(rotatedX * rect.Width);
                int pixelDY = (int)Math.Round(-rotatedY * rect.Height);

                rect = new Rectangle(rect.X + pixelDX, rect.Y + pixelDY, rect.Width, rect.Height);
            }

            var src = GetSourceRect(protoId!, texture, rotation);

            void DoDraw()
            {
                if (tint != null)
                {
                    // With tint — use original behavior (full stretch for tinted overlays)
                    g.DrawImage(texture, rect, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, tint);
                }
                else
                {
                    DrawPreservingAspect(g, texture, rect, src);
                }
            }

            // У спрайтов с направленными строками (RSI: юг/север/восток/запад) поворот
            // уже "зашит" в выбор строки в GetSourceRect — доп. аффинный поворот тут
            // не нужен (иначе спрайт крутился бы дважды). А для однокадровых
            // прототипов (без направленных состояний) строка всегда одна и та же,
            // и без явного WithRotation колесо мыши визуально ничего не поворачивало —
            // отсюда и была "сломана" видимая ротация в редакторе.
            if (GetStateDirections(protoId!) >= 4)
            {
                DoDraw();
            }
            else
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                WithRotation(g, cx, cy, rotation, DoDraw);
            }
        }
        else
        {
            fallback?.Invoke(g, rect);
        }
    }

    #endregion

    #region Оптимизированные методы пакетной отрисовки

    private void DrawFloorTilesBatch(Graphics g, List<TileData> tiles, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (tiles.Count == 0) return;

        var fallbackColor = Color.FromArgb((int)(150 * opacity), 200, 200, 200);
        using var fallbackBrush = new SolidBrush(fallbackColor);

        // Сортируем по Y — тайлы ниже на экране рисуются первыми
        foreach (var tile in tiles.OrderBy(t => t.Y))
        {
            var rect = ToRect(tile.X, tile.Y, tileSize, viewOffset, gridOffset);
            var protoId = tile.ProtoId ?? "Plating";
            var texture = GetOrLoadTexture(protoId);
            var srcRect = texture != null ? GetSourceRect(protoId, texture, 0f) : Rectangle.Empty;

            if (texture != null)
                g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
            else
                g.FillRectangle(fallbackBrush, rect);
        }
    }

    private void DrawDecalsBatch(Graphics g, List<PlacedDecal> decals, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (decals == null || decals.Count == 0) return;

        // Сортируем по Y — декали ниже на экране рисуются первыми
        foreach (var decal in decals.OrderBy(d => d.Y))
        {
            string protoId = decal.Proto;

            // decal.X/Y — точная мировая координата (как у MapEntity), центрируем прямоугольник,
            // а не рисуем от угла как тайл — отсюда offsetTiles = -0.5
            var rect = ToRect(decal.X, decal.Y, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);
            float cx = rect.X + tileSize / 2f;
            float cy = rect.Y + tileSize / 2f;

            WithRotation(g, cx, cy, decal.Rotation, () =>
            {
                var tintAttrs = GetDecalTintAttributes(decal.Color);
                DrawTexturedRect(g, protoId, rect, tintAttrs, (gg, r) =>
                {
                    // Фолбэк-заглушку (нет текстуры) тоже красим в цвет декали, чтобы
                    // цвет был виден даже без спрайта
                    var fallbackColor = ParseDecalColor(decal.Color);
                    using var brush = new SolidBrush(Color.FromArgb(
                        (int)(160 * opacity),
                        fallbackColor.R, fallbackColor.G, fallbackColor.B));
                    gg.FillRectangle(brush, r);
                });
            });
        }
    }

    private void DrawWallTilesBatch(Graphics g, List<TileData> tiles, TileGrid tileGrid, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (tiles.Count == 0) return;

        // Сортируем по Y — стены ниже на экране рисуются первыми
        var sortedTiles = tiles.OrderBy(t => t.Y).ToList();

        // Кэш текстур по protoId — чтобы не грузить одну и ту же текстуру много раз
        var textureCache = new Dictionary<string, (Image? texture, Rectangle srcRect, bool isNonSquare)>();

        using var fallbackPen = new Pen(Color.Gray, 1);

        foreach (var tile in sortedTiles)
        {
            string wallProto = tile.ProtoId ?? "WallSolid";

            if (!textureCache.TryGetValue(wallProto, out var cached))
            {
                var texture = GetOrLoadTexture(wallProto);
                var srcRect = texture != null ? GetSourceRect(wallProto, texture, 0f) : Rectangle.Empty;
                var isNonSquare = texture != null && srcRect.Width > 0 && srcRect.Height > 0 && srcRect.Width != srcRect.Height;
                cached = (texture, srcRect, isNonSquare);
                textureCache[wallProto] = cached;
            }

            var rect = ToRect(tile.X, tile.Y, tileSize, viewOffset, gridOffset);

            if (cached.texture != null)
            {
                if (cached.isNonSquare)
                {
                    float ratio = (float)cached.srcRect.Width / cached.srcRect.Height;
                    int drawW = tileSize;
                    int drawH = Math.Max(1, (int)(tileSize / ratio));
                    int drawX = rect.X + (rect.Width - drawW) / 2;
                    int drawY = rect.Y + (rect.Height - drawH) / 2;
                    g.DrawImage(cached.texture, new Rectangle(drawX, drawY, drawW, drawH), cached.srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    g.DrawImage(cached.texture, rect, cached.srcRect, GraphicsUnit.Pixel);
                }
            }
            else
            {
                g.DrawRectangle(fallbackPen, rect);
            }
        }
    }

    private void DrawDoorTilesBatch(Graphics g, List<TileData> tiles, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (tiles.Count == 0) return;

        // Сортируем по Y — двери ниже на экране рисуются первыми
        var sortedTiles = tiles.OrderBy(t => t.Y).ToList();

        // Кэш текстур по protoId
        var textureCache = new Dictionary<string, (Image? texture, Rectangle srcRect, bool isNonSquare)>();

        foreach (var tile in sortedTiles)
        {
            string protoId = tile.ProtoId ?? "Airlock";

            if (!textureCache.TryGetValue(protoId, out var cached))
            {
                var texture = GetOrLoadTexture(protoId);
                var srcRect = texture != null ? GetSourceRect(protoId, texture, 0f) : Rectangle.Empty;
                var isNonSquare = texture != null && srcRect.Width > 0 && srcRect.Height > 0 && srcRect.Width != srcRect.Height;
                cached = (texture, srcRect, isNonSquare);
                textureCache[protoId] = cached;
            }

            var rect = ToRect(tile.X, tile.Y, tileSize, viewOffset, gridOffset);

            if (cached.texture != null)
            {
                if (cached.isNonSquare)
                {
                    float ratio = (float)cached.srcRect.Width / cached.srcRect.Height;
                    int drawW = tileSize;
                    int drawH = Math.Max(1, (int)(tileSize / ratio));
                    int drawX = rect.X + (rect.Width - drawW) / 2;
                    int drawY = rect.Y + (rect.Height - drawH) / 2;
                    g.DrawImage(cached.texture, new Rectangle(drawX, drawY, drawW, drawH), cached.srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    g.DrawImage(cached.texture, rect, cached.srcRect, GraphicsUnit.Pixel);
                }
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255));
                g.FillRectangle(brush, rect);
                using var pen = new Pen(Color.DarkBlue, 2);
                g.DrawRectangle(pen, rect);
                using var font = new Font("Segoe UI", 14);
                using var textBrush = new SolidBrush(Color.White);
                g.DrawString("🚪", font, textBrush, rect.X + 4, rect.Y + 2);
            }
        }
    }
    private void DrawFirelocksBatch(Graphics g, List<FirelockEntity> firelocks, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (firelocks.Count == 0) return;

        foreach (var firelock in firelocks)
        {
            var rect = ToRect(firelock.X, firelock.Y, tileSize, viewOffset, gridOffset);
            DrawTexturedRect(g, firelock.Proto, rect, null, (gg, r) =>
            {
                Color color = firelock.IsGlass ? Color.FromArgb(150, 100, 200, 255) : Color.FromArgb(200, 200, 100, 100);
                using var brush = new SolidBrush(color);
                gg.FillRectangle(brush, r);
                using var pen = new Pen(Color.Black, 1);
                gg.DrawRectangle(pen, r);

                using var font = new Font("Segoe UI", tileSize / 3, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.White);
                gg.DrawString("🔥", font, textBrush, r.X + tileSize / 4, r.Y + tileSize / 4);
            });
        }
    }

    private void DrawPipeLinesBatch(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        // Кэш цветов/пер по типу трубы
        var penCache = new Dictionary<string, Pen>();
        using var fallbackPen = new Pen(Color.Gray, 2);

        // Собираем все трубы в один плоский список и сортируем по Y — трубы ниже рисуются первыми
        var sortedPipes = pipes.OrderBy(p => p.Y).ToList();

        // Для каждого типа трубы строим словарь позиций (нужен для поиска соседей)
        var pipeDicts = new Dictionary<string, Dictionary<(float x, float y), PipeEntity>>();
        foreach (var pipe in pipes)
        {
            if (!pipeDicts.TryGetValue(pipe.PipeType, out var dict))
            {
                dict = new Dictionary<(float x, float y), PipeEntity>();
                pipeDicts[pipe.PipeType] = dict;
            }
            dict[(pipe.X, pipe.Y)] = pipe;
        }

        foreach (var pipe in sortedPipes)
        {
            if (!penCache.TryGetValue(pipe.PipeType, out var pen))
            {
                pen?.Dispose();
                var color = GetPipeColor(pipe.PipeType);
                pen = new Pen(color, Math.Max(2, tileSize / 10));
                penCache[pipe.PipeType] = pen;
            }

            float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            var pipeDict = pipeDicts[pipe.PipeType];
            var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            foreach (var (dx, dy) in directions)
            {
                var key = (pipe.X + dx, pipe.Y + dy);
                if (pipeDict.ContainsKey(key))
                {
                    float nx = (key.Item1 + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                    float ny = (key.Item2 + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
                    g.DrawLine(pen, cx, cy, nx, ny);
                }
            }
        }

        foreach (var p in penCache.Values)
            p?.Dispose();
    }

    private void DrawPipeDotsBatch(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        float dotSize = Math.Max(4, tileSize / 6);

        // Сортируем по Y — трубы ниже на экране рисуются первыми
        var sortedPipes = pipes.OrderBy(p => p.Y).ToList();

        // Кэш кистей по типу трубы
        var brushCache = new Dictionary<string, (SolidBrush brush, Pen pen)>();

        foreach (var pipe in sortedPipes)
        {
            if (!brushCache.TryGetValue(pipe.PipeType, out var cached))
            {
                var color = GetPipeDotColor(pipe.PipeType);
                var brush = new SolidBrush(color);
                var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
                cached = (brush, borderPen);
                brushCache[pipe.PipeType] = cached;
            }

            float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            g.FillEllipse(cached.brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
            g.DrawEllipse(cached.pen, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
        }

        // Освобождаем ресурсы
        foreach (var cached in brushCache.Values)
        {
            cached.brush.Dispose();
            cached.pen.Dispose();
        }
    }

    /// <summary>
    /// ОБОБЩЁННЫЙ МЕТОД для отрисовки сигнализации
    /// </summary>
    private void DrawAlarmsBatch(Graphics g, List<MapEntity> alarms, int tileSize, PointF viewOffset, PointF gridOffset, string protoId, Color bgColor)
    {
        if (alarms.Count == 0) return;

        foreach (var entity in alarms)
        {
            var rect = ToRect(entity.X, entity.Y, tileSize, viewOffset, gridOffset);

            float rotation = entity switch
            {
                AirAlarmEntity a => a.Rotation,
                FireAlarmEntity f => f.Rotation,
                _ => 0f
            };

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            Image? texture = GetOrLoadTexture(protoId);
            if (texture != null)
            {
                WithRotation(g, cx, cy, rotation, () =>
                {
                    var srcRect = GetSourceRect(protoId, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                });
            }
            else
            {
                using var brush = new SolidBrush(bgColor);
                g.FillRectangle(brush, rect);
                using var pen = new Pen(Color.Black, 1);
                g.DrawRectangle(pen, rect);

                string icon = protoId == "AirAlarm" ? "🔊" : "🔥";
                using var font = new Font("Segoe UI", tileSize / 2, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.Black);
                g.DrawString(icon, font, textBrush, rect.X + tileSize / 4, rect.Y + tileSize / 4);

                using var arrowPen = new Pen(Color.Red, 2);
                float radius = tileSize / 2 - 4;
                g.DrawLine(arrowPen, cx, cy, cx + (float)Math.Cos(rotation) * radius, cy + (float)Math.Sin(rotation) * radius);
            }
        }
    }

    private void DrawRoomFillsBatch(Graphics g, List<Room> rooms, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (rooms.Count == 0) return;

        foreach (var room in rooms)
        {
            DrawRoomFill(g, room, tileSize, viewOffset, gridOffset, opacity);
        }
    }

    private void DrawRoomLinesBatch(Graphics g, List<Room> rooms, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        if (rooms.Count == 0) return;

        foreach (var room in rooms)
        {
            DrawRoomLine(g, room, tileSize, viewOffset, gridOffset, false, opacity);
        }
    }



    private void DrawTempPipePath(Graphics g, List<(int x, int y)> path, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (path.Count == 0) return;

        float dotSize = Math.Max(4, tileSize / 6);
        using var brush = new SolidBrush(Color.FromArgb(120, 0, 255, 100));

        foreach (var pos in path)
        {
            float cx = (pos.x + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float cy = (pos.y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
            g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
        }
    }

    #endregion

    #region Кэширование текстур

    private Image? GetOrLoadTexture(string protoId)
    {
        if (string.IsNullOrEmpty(protoId)) return null;

        if (_textureCache.TryGetValue(protoId, out var cached))
            return cached;

        Image? texture = null;
        if (_indexer != null)
        {
            var texturePath = _indexer.GetFullTexturePath(protoId);
            if (texturePath != null && File.Exists(texturePath))
            {
                try
                {
                    if (protoId == "Firelock" || protoId == "FirelockGlass")
                    {
                        string directory = Path.GetDirectoryName(texturePath)!;
                        string fileName = Path.GetFileName(texturePath);
                        if (fileName.Equals("closed.png", StringComparison.OrdinalIgnoreCase))
                        {
                            string openPath = Path.Combine(directory, "open.png");
                            if (File.Exists(openPath))
                            {
                                texturePath = openPath;
                            }
                        }
                    }

                    texture = Image.FromFile(texturePath);
                    _protoTextureDirCache[protoId] = Path.GetDirectoryName(texturePath) ?? "";
                    _protoStateNameCache[protoId] = Path.GetFileNameWithoutExtension(texturePath);
                }
                catch { }
            }
        }

        _textureCache[protoId] = texture;
        return texture;
    }

    private Size GetRsiFrameSize(string protoId, Image fallbackImage)
    {
        string dir = _protoTextureDirCache.TryGetValue(protoId, out var d) ? d : "";
        if (_rsiFrameSizeCache.TryGetValue(dir, out var cached)) return cached;

        Size result = new Size(Math.Min(32, fallbackImage.Width), Math.Min(32, fallbackImage.Height));
        try
        {
            string metaPath = Path.Combine(dir, "meta.json");
            if (!string.IsNullOrEmpty(dir) && File.Exists(metaPath))
            {
                var json = File.ReadAllText(metaPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("size", out var sizeElem) &&
                    sizeElem.TryGetProperty("x", out var xEl) &&
                    sizeElem.TryGetProperty("y", out var yEl))
                {
                    result = new Size(xEl.GetInt32(), yEl.GetInt32());
                }
            }
        }
        catch { }

        _rsiFrameSizeCache[dir] = result;
        return result;
    }

    private Rectangle GetSourceRect(string protoId, Image img, float rotation = 0f)
    {
        if (img == null) return Rectangle.Empty;

        var (directions, framesPerDirection) = GetStateDirectionInfo(protoId);

        // Без раздельных кадров по направлению (обычный случай — полы, стены, двери,
        // подавляющее большинство декалей и сущностей) результат НЕ зависит от rotation:
        // кадр всегда один и тот же, поворот — чисто визуальная трансформация в
        // DrawTexturedRect, а не выбор другого кадра. Раньше rotation безусловно входил
        // в ключ кэша — на картах с тысячами декалей/сущностей под разными углами это
        // плодило кучу почти-дублирующихся записей в кэше и лишние строковые аллокации
        // на каждый кадр рендера.
        string key = directions >= 4
            ? $"{protoId}_{img.Width}_{img.Height}_{rotation:F2}"
            : $"{protoId}_{img.Width}_{img.Height}";

        if (_sourceRectCache.TryGetValue(key, out var cached))
            return cached;

        var frameSize = GetRsiFrameSize(protoId, img);

        int col = 0, row = 0;
        if (directions >= 4)
        {
            // Порядок направлений, зашитый в сам движок Robust Toolbox: 0=юг, 1=север,
            // 2=восток, 3=запад (это порядок, в котором кадры направлений идут ПОДРЯД
            // в общей последовательности кадров стейта — а не "направление = своя строка").
            float normalized = rotation % (float)(2 * Math.PI);
            if (normalized < 0) normalized += (float)(2 * Math.PI);
            int quarter = (int)Math.Round(normalized / (Math.PI / 2)) % 4;
            int dirIndex = _quarterToDirOrder[quarter];

            // Реальная упаковка PNG у RSI — ПОСЛЕДОВАТЕЛЬНАЯ: все кадры стейта (направление
            // за направлением, внутри направления — кадр за кадром анимации) кладутся
            // подряд слева направо, с переносом на следующую строку по достижении правого
            // края изображения. Поэтому нельзя просто взять "номер направления = номер
            // строки" — нужно вычислить последовательный индекс кадра и разложить его
            // по СТОЛБЦАМ реального изображения (cols = реальная ширина / ширина кадра),
            // а не предполагать раскладку заранее. Берём всегда кадр анимации 0 —
            // проигрывание анимации во времени этот рендерер не поддерживает.
            int frameIndex = dirIndex * framesPerDirection;

            int cols = Math.Max(1, img.Width / Math.Max(1, frameSize.Width));
            col = frameIndex % cols;
            row = frameIndex / cols;
        }

        var rect = new Rectangle(col * frameSize.Width, row * frameSize.Height, frameSize.Width, frameSize.Height);
        _sourceRectCache[key] = rect;
        return rect;
    }

    // Порядок направлений в общей последовательности кадров RSI-стейта (0=юг,1=север,
    // 2=восток,3=запад — порядок enum Direction в Robust Toolbox). Индекс массива —
    // "четверть оборота" от нашего rotation (0=0°,1=90°,2=180°,3=270°), значение —
    // позиция этого направления в последовательности кадров стейта.
    //
    // 0° (юг/низ) и 180° (север/верх) уже совпадали с игрой правильно — юг/север
    // задаются напрямую индексами 0 и 1, без переворота. А вот 90°/270° раньше указывали
    // на противоположную сторону (запад вместо востока и наоборот) — направление отсчёта
    // поворота у игры и у этой раскладки не совпадало именно по горизонтальной оси.
    // Меняем местами значения для quarter=1 и quarter=3 (было 3 и 2, стало 2 и 3).
    private static readonly int[] _quarterToDirOrder = { 0, 2, 1, 3 };

    /// <summary>
    /// Читает у стейта и "directions", и число кадров анимации на направление
    /// (длину под-массива "delays") — оба нужны, чтобы вычислить ПОСЛЕДОВАТЕЛЬНЫЙ
    /// индекс кадра (направление*framesPerDirection + кадрАнимации), который потом
    /// раскладывается по строкам/столбцам реальной сетки PNG (см. GetSourceRect).
    /// БЕЗ framesPerDirection нельзя правильно посчитать смещение — движок паковал
    /// кадры не "один ряд на направление", а подряд, оборачивая по мере заполнения
    /// строки нужным количеством столбцов (получается почти квадратная сетка).
    /// </summary>
    private (int directions, int framesPerDirection) GetStateDirectionInfo(string protoId)
    {
        string dir = _protoTextureDirCache.TryGetValue(protoId, out var d) ? d : "";
        string state = _protoStateNameCache.TryGetValue(protoId, out var s) ? s : "";
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(state)) return (1, 1);

        if (!_rsiStateDirectionsCache.TryGetValue(dir, out var stateMap))
        {
            stateMap = new Dictionary<string, (int, int)>();
            try
            {
                string metaPath = Path.Combine(dir, "meta.json");
                if (File.Exists(metaPath))
                {
                    var json = File.ReadAllText(metaPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("states", out var statesArr))
                    {
                        foreach (var stateElem in statesArr.EnumerateArray())
                        {
                            if (!stateElem.TryGetProperty("name", out var nameEl)) continue;
                            string name = nameEl.GetString() ?? "";
                            int dirs = stateElem.TryGetProperty("directions", out var dirEl) ? dirEl.GetInt32() : 1;

                            int framesPerDir = 1;
                            if (stateElem.TryGetProperty("delays", out var delaysEl) && delaysEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var firstSub = delaysEl.EnumerateArray().FirstOrDefault();
                                if (firstSub.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    framesPerDir = Math.Max(1, firstSub.GetArrayLength());
                            }

                            stateMap[name] = (dirs, framesPerDir);
                        }
                    }
                }
            }
            catch { }
            _rsiStateDirectionsCache[dir] = stateMap;
        }

        return stateMap.TryGetValue(state, out var found) ? found : (1, 1);
    }

    private int GetStateDirections(string protoId) => GetStateDirectionInfo(protoId).directions;
    // Кэш ImageAttributes по цвету декали — пересоздавать ColorMatrix на каждый DrawImage
    // накладно, а цветов у декалей на карте обычно немного (одни и те же несколько цветов
    // из палитры повторяются на десятках декалей)
    private readonly Dictionary<string, ImageAttributes> _decalTintCache = new();

    /// <summary>
    /// Парсит цвет декали в формате "#RRGGBBAA" (как хранится в PlacedDecal.Color
    /// и экспортируется в DecalGrid). При ошибке — непрозрачный белый (нет тонирования).
    /// </summary>
    private static Color ParseDecalColor(string hex)
    {
        try
        {
            var h = hex.TrimStart('#');
            if (h.Length == 8)
            {
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                int a = Convert.ToInt32(h.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            if (h.Length == 6)
            {
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Color.White;
    }

    /// <summary>
    /// ImageAttributes с ColorMatrix, умножающей RGB и альфу текстуры на компоненты
    /// заданного цвета — так игра тонирует декали (текстура декали обычно белая/маска,
    /// а итоговый цвет задаётся полем color в DecalGrid).
    /// </summary>
    private ImageAttributes GetDecalTintAttributes(string decalColorHex)
    {
        if (_decalTintCache.TryGetValue(decalColorHex, out var cached))
            return cached;

        var color = ParseDecalColor(decalColorHex);
        float rf = color.R / 255f;
        float gf = color.G / 255f;
        float bf = color.B / 255f;
        float af = color.A / 255f;

        var matrix = new ColorMatrix(new float[][]
        {
            new float[] { rf, 0,  0,  0,  0 },
            new float[] { 0,  gf, 0,  0,  0 },
            new float[] { 0,  0,  bf, 0,  0 },
            new float[] { 0,  0,  0,  af, 0 },
            new float[] { 0,  0,  0,  0,  1 }
        });

        var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix);

        _decalTintCache[decalColorHex] = attrs;
        return attrs;
    }

    public void ClearCache()
    {
        foreach (var kvp in _textureCache)
        {
            if (kvp.Value != null)
                kvp.Value.Dispose();
        }
        _textureCache.Clear();
        _sourceRectCache.Clear();

        foreach (var kvp in _decalTintCache)
            kvp.Value.Dispose();
        _decalTintCache.Clear();
    }

    #endregion

    #region Вспомогательные методы

    private Color GetPipeColor(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => Color.FromArgb(180, 100, 200, 255),
            "Waste" => Color.FromArgb(180, 255, 150, 150),
            "Normal" => Color.FromArgb(180, 200, 200, 200),
            _ => Color.FromArgb(180, 150, 150, 150)
        };
    }

    private Color GetPipeDotColor(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => Color.FromArgb(200, 100, 200, 255),
            "Waste" => Color.FromArgb(200, 255, 150, 150),
            "Normal" => Color.FromArgb(200, 200, 200, 200),
            _ => Color.FromArgb(200, 150, 150, 150)
        };
    }

    #endregion

    #region Остальные методы

    private void DrawGrid(Graphics g, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int alpha = (int)(180 * opacity);
        using var pen = new Pen(Color.FromArgb(alpha, 200, 200, 200), 1);

        float offsetX = viewOffset.X - (gridOffset.X * tileSize);
        float offsetY = viewOffset.Y - (gridOffset.Y * tileSize);

        int startX = (int)(-offsetX % tileSize);
        int startY = (int)(-offsetY % tileSize);
        if (startX < 0) startX += tileSize;
        if (startY < 0) startY += tileSize;

        for (int x = startX; x <= _buffer.Width; x += tileSize)
            g.DrawLine(pen, x, 0, x, _buffer.Height);
        for (int y = startY; y <= _buffer.Height; y += tileSize)
            g.DrawLine(pen, 0, y, _buffer.Width, y);
    }

    private bool RoomHasWallOnSide(Room room, int x, int y, int dx, int dy)
    {
        return !room.Contains(x + dx, y + dy);
    }

    /// <summary>
    /// Прямоугольник клетки с инсетом в половину тайла ТОЛЬКО с тех сторон, где у
    /// комнаты реально есть свой тайл стены (см. HasWallOnSide). Сторона, "проигравшая"
    /// владение общей стеной соседней комнате, инсета не получает — заливка/линия там
    /// доходит вплотную до края тайла, потому что стены в этом тайле физически нет,
    /// пол начинается сразу с края и упирается в чужую стену, стоящую в соседнем тайле.
    /// </summary>
    private RectangleF GetCellInsetRect(Room room, int x, int y, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        float cellX = (x + gridOffset.X) * tileSize - viewOffset.X;
        float cellY = (y + gridOffset.Y) * tileSize - viewOffset.Y;
        float half = tileSize / 2f;

        float left = cellX + (room.Contains(x - 1, y) ? 0 : half);
        float right = cellX + tileSize - (room.Contains(x + 1, y) ? 0 : half);
        float top = cellY + (room.Contains(x, y - 1) ? 0 : half);
        float bottom = cellY + tileSize - (room.Contains(x, y + 1) ? 0 : half);

        return RectangleF.FromLTRB(left, top, right, bottom);
    }



    private Region GetCellFillRegion(Room room, int x, int y, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var rect = GetCellInsetRect(room, x, y, tileSize, viewOffset, gridOffset);
        var region = new Region(rect);
        float half = tileSize / 2f;

        var diagonals = new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) };
        foreach (var (ddx, ddy) in diagonals)
        {
            bool orthoBothOpen = room.Contains(x + ddx, y) && room.Contains(x, y + ddy);
            bool diagonalForeign = !room.Contains(x + ddx, y + ddy);

            if (orthoBothOpen && diagonalForeign)
            {
                float cx = ddx > 0 ? rect.Right - half : rect.Left;
                float cy = ddy > 0 ? rect.Bottom - half : rect.Top;
                region.Exclude(new RectangleF(cx, cy, half, half));
            }
        }

        return region;
    }



    /// <summary>
    /// Достраивает Г-образные коннекторы во внутренних (вогнутых) углах комнаты —
    /// там линии двух соседних клеток не встречаются сами по себе, каждая
    /// утапливается на пол-тайла в свою сторону. Работает чисто в пределах одной
    /// комнаты (RemovedCells), никаких других комнат тут не участвует.
    /// </summary>
    private void DrawConcaveCornerConnectors(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, Pen pen)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;

                var rectAnchor = GetCellInsetRect(room, x, y, tileSize, viewOffset, gridOffset);

                if (RoomHasWallOnSide(room, x, y, 0, -1) && room.Contains(x - 1, y - 1) &&
                    RoomHasWallOnSide(room, x - 1, y - 1, 1, 0))
                {
                    var rectPartner = GetCellInsetRect(room, x - 1, y - 1, tileSize, viewOffset, gridOffset);
                    float px = rectPartner.Right;
                    g.DrawLine(pen, px, rectPartner.Bottom, px, rectAnchor.Top);
                    g.DrawLine(pen, px, rectAnchor.Top, rectAnchor.Left, rectAnchor.Top);
                }

                if (RoomHasWallOnSide(room, x, y, 0, -1) && room.Contains(x + 1, y - 1) &&
                    RoomHasWallOnSide(room, x + 1, y - 1, -1, 0))
                {
                    var rectPartner = GetCellInsetRect(room, x + 1, y - 1, tileSize, viewOffset, gridOffset);
                    float px = rectPartner.Left;
                    g.DrawLine(pen, px, rectPartner.Bottom, px, rectAnchor.Top);
                    g.DrawLine(pen, px, rectAnchor.Top, rectAnchor.Right, rectAnchor.Top);
                }

                if (RoomHasWallOnSide(room, x, y, 0, 1) && room.Contains(x - 1, y + 1) &&
                    RoomHasWallOnSide(room, x - 1, y + 1, 1, 0))
                {
                    var rectPartner = GetCellInsetRect(room, x - 1, y + 1, tileSize, viewOffset, gridOffset);
                    float px = rectPartner.Right;
                    g.DrawLine(pen, px, rectPartner.Top, px, rectAnchor.Bottom);
                    g.DrawLine(pen, px, rectAnchor.Bottom, rectAnchor.Left, rectAnchor.Bottom);
                }

                if (RoomHasWallOnSide(room, x, y, 0, 1) && room.Contains(x + 1, y + 1) &&
                    RoomHasWallOnSide(room, x + 1, y + 1, -1, 0))
                {
                    var rectPartner = GetCellInsetRect(room, x + 1, y + 1, tileSize, viewOffset, gridOffset);
                    float px = rectPartner.Left;
                    g.DrawLine(pen, px, rectPartner.Top, px, rectAnchor.Bottom);
                    g.DrawLine(pen, px, rectAnchor.Bottom, rectAnchor.Right, rectAnchor.Bottom);
                }
            }
        }
    }








    private void DrawRoomFill(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int alpha = (int)(room.FillColor.A * opacity);
        using var brush = new SolidBrush(Color.FromArgb(alpha, room.FillColor.R, room.FillColor.G, room.FillColor.B));

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;

                using var region = GetCellFillRegion(room, x, y, tileSize, viewOffset, gridOffset);
                g.FillRegion(brush, region);
            }
        }
    }

    /// <summary>
    /// Обводка — это трассировка реального контура комнаты: для каждой занятой
    /// клетки проверяем 4 соседей через room.Contains (то же условие, что и в
    /// TileBuilder.GetBoundaryWallProto), и если сосед не принадлежит этой же
    /// комнате — рисуем отрезок ровно по этой стороне клетки. Так обводка
    /// "обтекает" вырез и совпадает с фактическим положением стен, а не рисует
    /// старый прямоугольник целиком.
    /// </summary>
    private void DrawRoomLine(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        Color color = isCurrent ? Color.Red : Color.FromArgb((int)(room.LineColor.A * opacity), room.LineColor.R, room.LineColor.G, room.LineColor.B);
        using var pen = new Pen(color, isCurrent ? 3 : 2);

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                if (room.RemovedCells.Contains((x, y))) continue;

                var rect = GetCellInsetRect(room, x, y, tileSize, viewOffset, gridOffset);

                if (!room.Contains(x, y - 1))
                    g.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
                if (!room.Contains(x, y + 1))
                    g.DrawLine(pen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                if (!room.Contains(x - 1, y))
                    g.DrawLine(pen, rect.Left, rect.Top, rect.Left, rect.Bottom);
                if (!room.Contains(x + 1, y))
                    g.DrawLine(pen, rect.Right, rect.Top, rect.Right, rect.Bottom);
            }
        }

        DrawConcaveCornerConnectors(g, room, tileSize, viewOffset, gridOffset, pen);

        if (!HideRoomOverlay && tileSize > 20 && opacity > 0.3f)
        {
            float startX = (room.X + gridOffset.X) * tileSize - viewOffset.X + tileSize / 2f;
            float startY = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y + tileSize / 2f;

            using var font = new Font("Arial", Math.Min(10, tileSize / 3));
            Color textColor = GetContrastColor(room.FillColor);
            int alpha = (int)(200 * opacity);
            using var brush = new SolidBrush(Color.FromArgb(alpha, textColor));

            int innerWidth = Math.Max(0, room.Width - 2);
            int innerHeight = Math.Max(0, room.Height - 2);
            g.DrawString($"{innerWidth}×{innerHeight}", font, brush, startX + 2, startY + 2);
        }
    }





    private void DrawSubtractPreview(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        // Заливка вырезаемой области — по полным клеткам (сама область вычитания
        // задаётся целыми тайлами, инсет тут не нужен, это не контур комнаты)
        float startX = (room.X + gridOffset.X) * tileSize - viewOffset.X;
        float startY = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        float width = room.Width * tileSize;
        float height = room.Height * tileSize;

        var fillRect = new RectangleF(startX, startY, width, height);
        using var brush = new SolidBrush(Color.FromArgb(90, 255, 0, 0));
        g.FillRectangle(brush, fillRect);

        // Рамку вырезаемой области рисуем с тем же инсетом в половину тайла,
        // что и обводку комнат (DrawRoomLine) — иначе во время перетаскивания
        // рамка идёт по краю тайлов, а не по их середине, и визуально не совпадает
        // с тем, как будет выглядеть итоговый контур после применения вычитания
        float half = tileSize / 2f;
        var lineRect = RectangleF.FromLTRB(
            startX + half,
            startY + half,
            startX + width - half,
            startY + height - half);

        using var pen = new Pen(Color.Red, 3)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        if (lineRect.Width > 0 && lineRect.Height > 0)
            g.DrawRectangle(pen, lineRect.X, lineRect.Y, lineRect.Width, lineRect.Height);
    }

    private void DrawRestorePreview(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        // Заливка восстанавливаемой области — зелёный цвет
        float startX = (room.X + gridOffset.X) * tileSize - viewOffset.X;
        float startY = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        float width = room.Width * tileSize;
        float height = room.Height * tileSize;

        var fillRect = new RectangleF(startX, startY, width, height);
        using var brush = new SolidBrush(Color.FromArgb(90, 0, 180, 0));
        g.FillRectangle(brush, fillRect);

        // Рамка восстанавливаемой области
        float half = tileSize / 2f;
        var lineRect = RectangleF.FromLTRB(
            startX + half,
            startY + half,
            startX + width - half,
            startY + height - half);

        using var pen = new Pen(Color.Green, 3)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        if (lineRect.Width > 0 && lineRect.Height > 0)
            g.DrawRectangle(pen, lineRect.X, lineRect.Y, lineRect.Width, lineRect.Height);
    }
    private Color GetContrastColor(Color backgroundColor)
    {
        int brightness = (int)(backgroundColor.R * 0.299 + backgroundColor.G * 0.587 + backgroundColor.B * 0.114);
        return brightness < 128 ? Color.White : Color.Black;
    }

    private void DrawInfo(Graphics g, float scale, string toolName, MapData map)
    {
        using var font = new Font("Arial", 12, FontStyle.Bold);
        using var brush = new SolidBrush(Color.DarkGray);
        var name = map.ActiveGrid?.Name ?? "Нет";
        string mode = HideRoomOverlay ? " [ОВЕРЛЕЙ СКРЫТ]" : "";
        string pipeMode = ShowPipeOverlay ? "" : " [ТРУБЫ СКРЫТЫ]";
        string connectionsMode = ShowAlarmConnections ? "" : " [СВЯЗИ СКРЫТЫ]";
        g.DrawString($"Инструмент: {toolName}{mode}{pipeMode}{connectionsMode}  Масштаб: {scale:P0}  Активный грид: {name}  Всего гридов: {map.Grids.Count}",
            font, brush, 10, 10);
    }

    private List<(int x, int y)> CalculatePipePath((int x, int y) start, (int x, int y) end)
    {
        var positions = new List<(int x, int y)>();

        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        int stepY = startY <= endY ? 1 : -1;
        for (int y = startY; y != endY + stepY; y += stepY)
        {
            positions.Add((startX, y));
        }

        int stepX = startX <= endX ? 1 : -1;
        int startXPos = startX + stepX;
        for (int x = startXPos; x != endX + stepX; x += stepX)
        {
            positions.Add((x, endY));
        }

        return positions;
    }

    private void DrawAlarmConnections(Graphics g, AlarmNetwork network, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        if (network == null || network.Connections.Count == 0) return;

        foreach (var connection in network.Connections)
        {
            // Пропускаем связь, если ОБА её конца (сигнализация и устройство) вне видимой
            // области — раньше рисовались все связи по всей карте на каждый кадр,
            // независимо от того, что реально на экране.
            bool sourceVisible = IsPointVisible(connection.Source.X, connection.Source.Y, visibleRect);
            bool targetVisible = IsPointVisible(connection.Target.X, connection.Target.Y, visibleRect);
            if (!sourceVisible && !targetVisible) continue;

            float sx = (connection.Source.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X; float sy = (connection.Source.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            float tx = (connection.Target.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float ty = (connection.Target.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            using (var pen = new Pen(connection.LineColor, connection.LineWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, sx, sy, tx, ty);
            }

            float dotSize = 4;
            using (var brush = new SolidBrush(connection.LineColor))
            {
                g.FillEllipse(brush, sx - dotSize / 2, sy - dotSize / 2, dotSize, dotSize);
                g.FillEllipse(brush, tx - dotSize / 2, ty - dotSize / 2, dotSize, dotSize);
            }
        }
    }

    public void SetAlarmNetwork(AlarmNetwork network)
    {
        _currentNetwork = network;
    }

    /// <summary>
    /// Помечает TileGrid конкретного грида как устаревший — при следующем Render()
    /// он будет пересобран заново. Вызывать из MainForm при любом структурном
    /// изменении грида (комнаты, двери, ручные тайлы), а не на каждый рендер.
    /// </summary>
    public void InvalidateTileGrid(int gridUid)
    {
        _dirtyTileGrids.Add(gridUid);
    }
    public void SetAlarmPreview(int x, int y, float rotation, string type)
    {
        _previewX = x;
        _previewY = y;
        _previewRotation = rotation;
        _previewType = type;
        _showAlarmPreview = true;
    }

    public void ClearAlarmPreview()
    {
        _showAlarmPreview = false;
    }

    private void DrawAlarmPreview(Graphics g, float scale, PointF viewOffset)
    {
        if (!_showAlarmPreview || string.IsNullOrEmpty(_previewType)) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _currentMap.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_currentMap.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        float screenX = _previewX * tileSize + gridOffsetX - viewOffset.X;
        float screenY = _previewY * tileSize + gridOffsetY - viewOffset.Y;

        // Рисуем полупрозрачный фон тайла
        using (var brush = new SolidBrush(Color.FromArgb(60, 100, 200, 255)))
        {
            g.FillRectangle(brush, screenX, screenY, tileSize, tileSize);
        }

        // Рисуем рамку
        using (var pen = new Pen(Color.FromArgb(200, 0, 200, 255), 2))
        {
            g.DrawRectangle(pen, screenX, screenY, tileSize, tileSize);
        }

        // Рисуем иконку сигнализации
        float centerX = screenX + tileSize / 2;
        float centerY = screenY + tileSize / 2;
        float iconSize = tileSize * 0.4f;

        var state = g.Save();

        g.TranslateTransform(centerX, centerY);
        g.RotateTransform(_previewRotation * 180 / (float)Math.PI);

        string iconText = _previewType == "AirAlarm" ? "🔊" : "🔥";
        using (var font = new Font("Segoe UI", iconSize, FontStyle.Regular))
        using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
        {
            var size = g.MeasureString(iconText, font);
            g.DrawString(iconText, font, brush, -size.Width / 2, -size.Height / 2);
        }

        // Стрелка направления
        float arrowLength = tileSize * 0.35f;
        using (var pen = new Pen(Color.FromArgb(200, 0, 255, 255), 3))
        {
            pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
            g.DrawLine(pen, 0, 0, 0, -arrowLength);
        }

        // Круг в центре
        using (var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255)))
        {
            g.FillEllipse(brush, -4, -4, 8, 8);
        }

        g.Restore(state);

        // Текст с типом сигнализации под тайлом
        using (var font = new Font("Arial", 8, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.White))
        using (var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        {
            string label = _previewType == "AirAlarm" ? "Воздух" : "Пожар";
            var size = g.MeasureString(label, font);
            float textX = screenX + tileSize / 2 - size.Width / 2;
            float textY = screenY + tileSize + 2;

            g.DrawString(label, font, shadow, textX + 1, textY + 1);
            g.DrawString(label, font, brush, textX, textY);
        }
    }


    private void DrawEntityPreview(Graphics g, float scale, PointF viewOffset)
    {
        if (!_showEntityPreview || string.IsNullOrEmpty(_previewEntityProto)) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        var gridOffset = new PointF(_currentMap.ActiveGrid.Position.X, _currentMap.ActiveGrid.Position.Y + layerOffsetY);

        var rect = ToRect(_previewEntityX, _previewEntityY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);
        float cx = rect.X + tileSize / 2f;
        float cy = rect.Y + tileSize / 2f;

        ImageAttributes? tint = !string.IsNullOrEmpty(_previewDecalColor)
        ? GetDecalTintAttributes(_previewDecalColor)
        : null;

        // Если noRot: true — игнорируем rotation превью
        float previewRotation = IsPrototypeNoRotate(_previewEntityProto) ? 0f : _previewEntityRotation;

        DrawTexturedRect(g, _previewEntityProto, rect, tint, (gg, r) =>
        {
            Color fallback = !string.IsNullOrEmpty(_previewDecalColor)
                ? ParseDecalColor(_previewDecalColor)
                : Color.FromArgb(255, 0, 255);
            using var brush = new SolidBrush(Color.FromArgb(120, fallback.R, fallback.G, fallback.B));
            gg.FillRectangle(brush, r);
            using var pen = new Pen(Color.FromArgb(180, 0, 0, 0), 1);
            gg.DrawRectangle(pen, r);
        }, previewRotation);
    }

    private void DrawAlarmDirectionArrows(Graphics g, List<MapEntity> alarms, float scale, PointF viewOffset, PointF gridPosition)
    {
        if (!ShowAlarmConnections) return;
        if (alarms.Count == 0) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        float gridOffsetX = gridPosition.X * tileSize;
        float gridOffsetY = gridPosition.Y * tileSize;

        // Список сигнализаций приходит уже отфильтрованным по видимой области вызывающим
        // кодом (Render()) — раньше тут заново сканировались ВСЕ сигнализации грида через
        // grid.Entities, включая те, что далеко за пределами экрана.
        foreach (var alarm in alarms)
        {
            float screenX = (float)alarm.X * tileSize + gridOffsetX - viewOffset.X;
            float screenY = (float)alarm.Y * tileSize + gridOffsetY - viewOffset.Y;
            float centerX = screenX + tileSize / 2;
            float centerY = screenY + tileSize / 2;

            float rotation = alarm is AirAlarmEntity air ? air.Rotation :
                            (alarm as FireAlarmEntity)?.Rotation ?? 0;

            float arrowLength = tileSize * 0.4f;

            var state = g.Save();
            g.TranslateTransform(centerX, centerY);
            g.RotateTransform(rotation * 180 / (float)Math.PI);

            using (var pen = new Pen(Color.FromArgb(180, 255, 255, 0), 2))
            {
                pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                g.DrawLine(pen, 0, 0, 0, -arrowLength);
            }

            g.Restore(state);
        }
    }


    private void DrawGenericEntitiesBatch(Graphics g, List<MapEntity> entities, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (entities.Count == 0) return;

        // Сортируем по Y — сущности ниже на экране рисуются первыми
        var sortedEntities = entities.OrderBy(e => e.Y).ToList();

        // Кэш прототипов с noRotate по protoId
        var noRotateCache = new Dictionary<string, bool>();

        foreach (var entity in sortedEntities)
        {
            var protoId = entity.Proto ?? "";
            if (!noRotateCache.TryGetValue(protoId, out var noRotate))
            {
                noRotate = IsPrototypeNoRotate(protoId);
                noRotateCache[protoId] = noRotate;
            }

            var rect = ToRect(entity.X, entity.Y, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

            // Если noRot: true — игнорируем rotation сущности
            float rotation = noRotate ? 0f : entity.Rotation;

            DrawTexturedRect(g, protoId, rect, null, (gg, r) =>
            {
                using var brush = new SolidBrush(Color.FromArgb(180, 255, 0, 255));
                gg.FillRectangle(brush, r);
                using var pen = new Pen(Color.Black, 1);
                gg.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

                if (tileSize > 16)
                {
                    using var font = new Font("Segoe UI", 6);
                    using var textBrush = new SolidBrush(Color.White);
                    string label = protoId.Length > 8 ? protoId.Substring(0, 8) : protoId;
                    gg.DrawString(label, font, textBrush, r.X + 1, r.Y + 1);
                }
            }, rotation);
        }
    }


    private void DrawSelectionBox(Graphics g)
    {
        if (!_showSelectionBox) return;

        int x = Math.Min(_selectionBoxStart.X, _selectionBoxEnd.X);
        int y = Math.Min(_selectionBoxStart.Y, _selectionBoxEnd.Y);
        int w = Math.Abs(_selectionBoxEnd.X - _selectionBoxStart.X);
        int h = Math.Abs(_selectionBoxEnd.Y - _selectionBoxStart.Y);
        var rect = new Rectangle(x, y, w, h);

        using var fillBrush = new SolidBrush(Color.FromArgb(45, 255, 140, 0));
        g.FillRectangle(fillBrush, rect);

        using var pen = new Pen(Color.FromArgb(255, 255, 140, 0), 1.5f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, rect);
    }


    private void DrawDecalAreaEditOverlay(Graphics g, float scale, PointF viewOffset)
    {
        if (_decalAreaEditRect == null) return;
        if (_currentMap?.ActiveGrid == null) return;

        var (ax, ay, aw, ah) = _decalAreaEditRect.Value;
        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _currentMap.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_currentMap.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        float left = ax * tileSize + gridOffsetX - viewOffset.X;
        float top = ay * tileSize + gridOffsetY - viewOffset.Y;
        float width = aw * tileSize;
        float height = ah * tileSize;

        using var fillBrush = new SolidBrush(Color.FromArgb(50, 255, 200, 0));
        g.FillRectangle(fillBrush, left, top, width, height);

        using var pen = new Pen(Color.FromArgb(255, 255, 160, 0), 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, left, top, width, height);

        // Угловые "ручки" для перетаскивания — фиксированный размер в пикселях экрана,
        // не зависящий от масштаба, чтобы хват оставался удобным при любом зуме
        float handleSize = 10f;
        using var handleBrush = new SolidBrush(Color.FromArgb(255, 255, 160, 0));
        using var handlePen = new Pen(Color.Black, 1);

        var corners = new (float x, float y)[]
        {
        (left, top), (left + width, top), (left, top + height), (left + width, top + height)
        };

        foreach (var (cx, cy) in corners)
        {
            var rect = new RectangleF(cx - handleSize / 2, cy - handleSize / 2, handleSize, handleSize);
            g.FillRectangle(handleBrush, rect);
            g.DrawRectangle(handlePen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }


    private void DrawSelectionHighlight(Graphics g, float scale, PointF viewOffset)
    {
        if (_selection == null || _selection.Count == 0) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _currentMap.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_currentMap.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        // Контрастная "обводка": тёмная подложка + яркий пунктир поверх —
        // читается и на белом, и на тёмном фоне
        using var outlinePen = new Pen(Color.FromArgb(220, 0, 0, 0), 4);
        using var pen = new Pen(Color.FromArgb(255, 255, 60, 0), 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            DashPattern = new float[] { 4, 3 }
        };

        foreach (var obj in _selection)
        {
            Rectangle rect;

            switch (obj)
            {
                case Room room:
                    float rx = (room.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ry = (room.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)rx, (int)ry, (int)(room.Width * tileSize), (int)(room.Height * tileSize));
                    break;

                case PlacedDecal decal:
                    float dx = (decal.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float dy = (decal.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)(dx - tileSize / 2f), (int)(dy - tileSize / 2f), tileSize, tileSize);
                    break;

                case MapEntity entity:
                    float ex = (entity.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ey = (entity.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)(ex - tileSize / 2f), (int)(ey - tileSize / 2f), tileSize, tileSize);
                    break;

                case PlacedTile tile:
                    float tx = (tile.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ty = (tile.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)tx, (int)ty, tileSize, tileSize);
                    break;

                default:
                    continue;
            }




            g.DrawRectangle(outlinePen, rect);
            g.DrawRectangle(pen, rect);
        }
    }
    public void SetEntityPreview(float x, float y, float rotation, string proto, string? decalColor = null)
    {
        _previewEntityX = x;
        _previewEntityY = y;
        _previewEntityRotation = rotation;
        _previewEntityProto = proto;
        _previewDecalColor = decalColor;
        _showEntityPreview = true;
    }

    public void ClearEntityPreview()
    {
        _showEntityPreview = false;
    }

    #endregion
}