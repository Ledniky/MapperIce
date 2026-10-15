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
    private readonly DrawDepthManager _drawDepthManager;
    private readonly TileBuilder _tileBuilder;
    private readonly PipeBuilder _pipeBuilder;
    private readonly WireBuilder _wireBuilder;
    private readonly WireTypeManager _wireTypeManager;
    private readonly string _rootPath = "";
    public bool HideRoomOverlay { get; set; } = false;
    private MapData? _currentMap;
    public bool ShowPipeOverlay { get; set; } = true;
    public bool ShowWires { get; set; } = true;
    /// <summary>Показывать область обзора камер (как зона покрытия НВ-кабеля).</summary>
    public bool ShowCameraAreas { get; set; } = true;

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

    // Кэш кадров IconSmooth-стейтов: ключ "прототип|имя файла стейта" -> Image.
    // Один state_N.png (например state_3.png, 64×64) содержит 4 кадра-фриза по 32×32 —
    // по одному на каждый угол (SE/NW/NE/SW). Отдельный кэш от _textureCache, потому
    // что у обычных текстур один кадр на стейт, а тут — 4, и лежат они под вменяемым
    // ключом (не конфликтуют с прототипом).
    private readonly Dictionary<string, Image> _iconSmoothStateCache = new();

    // Размеры тайлов в пикселях для кэширования
    private int _cachedTileSize = 0;

    // Интерполяция для масштабирования
    private readonly InterpolationMode _interpolationMode = InterpolationMode.NearestNeighbor;
    private AlarmNetwork? _currentNetwork;
    public bool ShowAlarmConnections { get; set; } = true;

    // Связи беспроводной сети (кнопки/рычаги → устройства)
    /// <summary>true — рисовать ВСЕ связи сети (зелёная кнопка в верхней панели).</summary>
    public bool ShowNetworkLinks { get; set; } = false;
    /// <summary>Клетки, участвующие в связях выбранного устройства — их связи рисуются ярко.</summary>
    public HashSet<(int x, int y)> NetworkHighlightPositions { get; set; } = new();

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
    // false — в этом тайле ставить нельзя (нет комнаты/прототипа или клетка занята):
    // рисуется только красная рамка тайла, без спрайта.
    private bool _previewEntityValid = true;

    /// <summary>Цвет фона канваса в рендере. Задаётся из настроек, используется при очистке кадра.</summary>
    public Color BackgroundColor { get; set; } = Color.White;
    /// <summary>Цвет линий сетки канваса. Задаётся из настроек.</summary>
    public Color GridColor { get; set; } = Color.FromArgb(180, 200, 200, 200);

    // Подсветка инструмента "Расширить комнату" при наведении — какая комната,
    // от какой клетки и в какую сторону (dx, dy) будет расширяться при клике
    private Room? _expandPreviewRoom = null;
    private int _expandPreviewCellX;
    private int _expandPreviewCellY;
    private int _expandPreviewDx;
    private int _expandPreviewDy;
    private bool _showExpandPreview = false;

    private List<object> _selection = new();
    private bool _showSelectionBox = false;
    private Point _selectionBoxStart;
    private Point _selectionBoxEnd;

    // Растягиваемая рамка инструмента "Удалить область" (в тайловых координатах, инклюзивно)
    private (int x, int y, int w, int h)? _deleteAreaRect = null;

    public void SetDeleteAreaRect(int x, int y, int w, int h)
    {
        _deleteAreaRect = (x, y, w, h);
    }

    public void ClearDeleteAreaRect()
    {
        _deleteAreaRect = null;
    }

    private (int x, int y, int w, int h)? _decalAreaEditRect = null;

    public void SetDecalAreaEditRect(int x, int y, int w, int h)
    {
        _decalAreaEditRect = (x, y, w, h);
    }

    public void ClearDecalAreaEditRect()
    {
        _decalAreaEditRect = null;
        _decalAreaEditSubtractions = null;
        _decalAreaEditCutCells = null;
        _decalAreaEditRemovedCells = null;
    }



    private List<DecalAreaRect>? _decalAreaEditSubtractions = null;

    public void SetDecalAreaEditSubtractions(List<DecalAreaRect>? subtractions)
    {
        _decalAreaEditSubtractions = subtractions;
    }

    // Потайлёво вырезанные клетки декалей (кнопка «✖» в окне «Ручные области») —
    // рисуются на карте при редактировании области, чтобы было видно, какие тайлы без декалей.
    private HashSet<(int X, int Y)>? _decalAreaEditCutCells = null;

    public void SetDecalAreaEditCutCells(HashSet<(int X, int Y)>? cutCells)
    {
        _decalAreaEditCutCells = cutCells;
    }

    // Потайлёво удалённые из области клетки (изменение формы, «➖ Вычесть») — рисуются
    // тёмным, чтобы было видно, какие тайлы вырезаны из формы области.
    private HashSet<(int X, int Y)>? _decalAreaEditRemovedCells = null;

    public void SetDecalAreaEditRemovedCells(HashSet<(int X, int Y)>? removedCells)
    {
        _decalAreaEditRemovedCells = removedCells;
    }

    // Растягиваемый мышью прямоугольник вырезки декалей (в режиме «✖»)
    private (int x, int y, int w, int h)? _decalAreaCutDragRect = null;

    public void SetDecalAreaCutDragRect(int x, int y, int w, int h)
    {
        _decalAreaCutDragRect = (x, y, w, h);
    }

    public void ClearDecalAreaCutDragRect()
    {
        _decalAreaCutDragRect = null;
    }

    // Растягиваемый мышью прямоугольник вырезки КЛЕТОК ФОРМЫ области (режим «➖ Вычесть») —
    // отдельный от вырезки декалей (✖), чтобы оба инструмента можно было использовать на карте.
    private (int x, int y, int w, int h)? _decalAreaSubtractDragRect = null;

    public void SetDecalAreaSubtractDragRect(int x, int y, int w, int h)
    {
        _decalAreaSubtractDragRect = (x, y, w, h);
    }

    public void ClearDecalAreaSubtractDragRect()
    {
        _decalAreaSubtractDragRect = null;
    }


    // Растягиваемая МЫШЬЮ новая ручная область декалей (ещё не добавлена в слой)
    private (int x, int y, int w, int h)? _decalAreaDrawRect = null;

    public void SetDecalAreaDrawRect(int x, int y, int w, int h)
    {
        _decalAreaDrawRect = (x, y, w, h);
    }

    public void ClearDecalAreaDrawRect()
    {
        _decalAreaDrawRect = null;
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

    public Renderer(int width, int height, PrototypeIndexer? indexer, DrawDepthManager? drawDepthManager, TileBuilder tileBuilder, PipeBuilder pipeBuilder, WireBuilder wireBuilder, WireTypeManager wireTypeManager)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
        _drawDepthManager = drawDepthManager ?? new DrawDepthManager();
        _tileBuilder = tileBuilder;
        _pipeBuilder = pipeBuilder;
        _wireBuilder = wireBuilder;
        _wireTypeManager = wireTypeManager;
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

            g.Clear(BackgroundColor);

            int tileSize = (int)(Constants.TILE_SIZE * scale);

            if (_cachedTileSize != tileSize)
            {
                _cachedTileSize = tileSize;
            }

            var visibleGrids = map.Grids.Where(g => g.IsVisible).ToList();

            // Индекс активного слоя для скрытия слоёв выше него (по позиции в ПОЛНОМ
            // списке слоёв — см. сноску ниже про GetLayerOffsetY).
            int activeGridIndex = -1;
            if (map.ActiveGrid != null)
            {
                activeGridIndex = map.Grids.FindIndex(g => g.Uid == map.ActiveGrid.Uid);
            }

            foreach (var grid in visibleGrids)
            {
                // Скрываем слои, идущие выше активного
                int gridIndex = map.Grids.IndexOf(grid);
                if (gridIndex > activeGridIndex)
                    continue;

                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;

                // Смещение грида: Position + автоматическое смещение слоя по Y.
                // ВАЖНО: индекс берётся по ПОЛНОМУ списку слоёв (_map.Grids), а не по
                // списку видимых — иначе скрытие одного слоя (IsVisible=false) сдвинуло бы
                // Y-смещение всех следующих слоёв, и клетки рисовались бы с другим смещением,
                // чем их показывают мышь и рамка выделения (GetTilePosition/TileToScreen
                // используют _map.Grids.IndexOf).
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

                // Объединённый рендеринг: все элементы сортируются по Y + DrawDepthOffset
                DrawRenderLayer(g, tileGrid, grid, tileSize, viewOffset, gridOffset, opacity, visibleRect);

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

                // Список труб строится ВСЕГДА, даже когда оверлей труб выключен флагом «🔧»
                // в верхней панели: сами линии/точки/стрелки тогда скрываются, а вот
                // элементы на КОНЦАХ труб (утилизационные и почтовые юниты на слое U,
                // вентиляции газовых слоёв L1..L3) должны оставаться видимыми — иначе
                // вместе с трубами с карты пропадали бы и расставленные на них приёмники.
                var gridPipes = _pipeBuilder.GetPipes(grid);
                var allPipes = gridPipes
                    .Where(p => IsPointVisible(p.X, p.Y, visibleRect))
                    .OrderBy(p => p.Y)
                    .ToList();

                if (ShowPipeOverlay)
                {
                    DrawPipeLinesBatch(g, allPipes, tileSize, viewOffset, gridOffset);

                    // Маркеры фильтра — рисуем первыми, чтобы стрелка и точка трубы были поверх
                    DrawFilterMarkers(g, allPipes, tileSize, viewOffset, gridOffset);

                    // Стрелки направления потока — рисуем ДО точек, чтобы были сзади
                    DrawPipeFlowArrows(g, allPipes, tileSize, viewOffset, gridOffset);

                    if (allPipes.Count > 0)
                        DrawPipeDotsBatch(g, allPipes, tileSize, viewOffset, gridOffset);

                    if (_pipeBuilder.IsDrawing && _pipeBuilder.StartPoint.HasValue)
                    {
                        var start = _pipeBuilder.StartPoint.Value;
                        var end = _pipeBuilder.EndPoint ?? start;
                        var path = CalculatePipePath(start, end);
                        DrawTempPipePath(g, path, tileSize, viewOffset, gridOffset);
                    }
                }

                // Концы труб (юниты утилизации/почты и вентиляции на законцовках) —
                // вне зависимости от флага «Трубы». Соседей для определения конца
                // считаем по ПОЛНОМУ списку труб слоя: иначе у края экрана труба,
                // у которой сосед остался за кадром, считалась бы концом и получала
                // лишний спрайт приёмника/вентиляции.
                if (allPipes.Count > 0)
                    DrawEndpointMarkers(g, allPipes, gridPipes, tileSize, viewOffset, gridOffset);

                // Область обзора камер — тот же приём, что и зона покрытия НВ-кабеля
                // (см. DrawLvCoverageOverlay), только радиус больше. Рисуется и для
                // камер-«привязок» из свойств комнаты, и для камер, поставленных
                // обычным прототипом из панели репозитория.
                if (ShowCameraAreas)
                    DrawCameraCoverageOverlay(g, grid, tileSize, viewOffset, gridOffset, visibleRect);

                // Электросеть — отдельный слой поверх труб, та же идея (линии между
                // соседями одного типа + точки узлов), без специфичных для труб
                // маркеров фильтра/стрелок утилизации
                if (ShowWires)
                {
                    var allWires = _wireBuilder.GetWires(grid)
                        .Where(w => IsPointVisible(w.X, w.Y, visibleRect))
                        .OrderBy(w => w.Y)
                        .ToList();
                    DrawLvCoverageOverlay(g, allWires, tileSize, viewOffset, gridOffset);
                    DrawWireLinesBatch(g, allWires, tileSize, viewOffset, gridOffset); if (allWires.Count > 0)
                        DrawWireDotsBatch(g, allWires, tileSize, viewOffset, gridOffset);

                    if (_wireBuilder.IsDrawing && _wireBuilder.StartPoint.HasValue)
                    {
                        var wireStart = _wireBuilder.StartPoint.Value;
                        var wireEnd = _wireBuilder.EndPoint ?? wireStart;
                        var wirePath = CalculatePipePath(wireStart, wireEnd);
                        DrawTempPipePath(g, wirePath, tileSize, viewOffset, gridOffset);
                    }
                }

                // Собираем сигнализации для стрелок — они теперь внутри DrawRenderLayer,                // но стрелки рисуются отдельно, поэтому фильтруем заново
                var visibleAlarmsForArrows = grid.Entities
                    .OfType<MapEntity>()
                    .Where(e => e is AirAlarmEntity or FireAlarmEntity)
                    .Where(a => IsPointVisible(a.X, a.Y, visibleRect))
                    .ToList();
                DrawAlarmDirectionArrows(g, visibleAlarmsForArrows, scale, viewOffset, gridOffset);

                if (ShowAlarmConnections && ShowPipeOverlay && _currentNetwork != null)
                {
                    DrawAlarmConnections(g, _currentNetwork, tileSize, viewOffset, gridOffset, visibleRect);
                }
            }


            DrawInfo(g, scale, toolName, map);

            // Рисуем предпросмотр сигнализации под курсором
            DrawAlarmPreview(g, scale, viewOffset);

            // Рисуем предпросмотр размещаемого прототипа под курсором
            DrawEntityPreview(g, scale, viewOffset);
            DrawExpandRoomPreview(g, scale, viewOffset);
            DrawSelectionHighlight(g, scale, viewOffset);
            DrawSelectionBox(g);
            DrawDeleteAreaOverlay(g, scale, viewOffset);
            DrawDecalAreaDrawOverlay(g, scale, viewOffset);
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
        // Многослойный прототип (YAML Sprite.layers) — рисуем все слои поверх друг
        // друга отдельным путём и выходим, не трогая старую однослойную логику ниже.
        // GetLayers учитывает наследование от parent (в отличие от
        // FindPrototype(...).Layers), поэтому наследуемые прототипы вроде
        // Locker*Filled/Closet*Filled — которые переопределяют только parent и
        // EntityTableContainerFill — тоже рисуются многослойно, как в панели репозитория.
        var layered = !string.IsNullOrEmpty(protoId) ? _indexer?.GetLayers(protoId) : null;
        if (layered != null && layered.Count > 0)
        {
            DrawLayeredTexturedRect(g, protoId!, layered, rect, tint, fallback, rotation);
            return;
        }

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

    /// <summary>
    /// Рисует все видимые слои прототипа (proto.Layers) друг поверх друга, в том же
    /// порядке, в котором они перечислены в YAML — так же, как это делает сам движок.
    /// Смещение компонента Sprite.offset применяется ко всей композиции целиком (как
    /// раньше для одного слоя), а собственный offset конкретного слоя (если задан) —
    /// уже поверх этого, только к этому слою.
    /// </summary>
    private void DrawLayeredTexturedRect(Graphics g, string protoId, List<Models.SpriteLayer> layers, Rectangle rect, ImageAttributes? tint, Action<Graphics, Rectangle>? fallback, float rotation)
    {
        var (offX, offY) = _indexer?.GetSpriteOffset(protoId) ?? (0f, 0f);
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

        bool anyDrawn = false;
        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;

        // EntityStorageVisuals: шкафчики/локеры переопределяют state базовых слоёв
        // спрайта (Base → stateBaseClosed, Door → stateDoorClosed), причём сам компонент
        // Sprite с layers живёт у далёкого предка (ClosetBase, closet.rsi), а state —
        // у конкретного наследника (LockerCaptionFilled и т.п.). Подменяем state слоя
        // перед загрузкой текстуры, иначе рисовался бы дефолтный "generic"/"generic_door".
        var storageViz = _indexer?.GetEntityStorageVisuals(protoId);

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.Visible) continue;

            // Составной ключ кэша — отдельная запись в _textureCache/_sourceRectCache
            // и т.п. на каждый слой каждого прототипа (а не одна на protoId, как для
            // однослойных текстур). State хранения включается в ключ, чтобы разные
            // state (закрыто/открыто) не конфликтовали в кэше.
            string effectiveState = layer.State;
            if (storageViz != null)
            {
                bool isBase;
                bool isDoor;
                if (!string.IsNullOrEmpty(layer.MapKey))
                {
                    isBase = layer.MapKey.Contains("Base");
                    isDoor = layer.MapKey.Contains("Door");
                }
                else
                {
                    bool first = i == 0;
                    bool second = i == 1;
                    isBase = first && !second;
                    isDoor = !first && second;
                }

                string? overrideState = isBase ? storageViz.Value.baseClosed
                                       : isDoor ? storageViz.Value.doorClosed
                                       : null;
                if (!string.IsNullOrEmpty(overrideState))
                    effectiveState = overrideState;
            }

            string cacheKey = $"{protoId}__layer{i}__{effectiveState ?? "?"}";
            Image? layerTexture = GetOrLoadLayerTexture(cacheKey, protoId, layer, effectiveState);
            if (layerTexture == null) continue;

            anyDrawn = true;

            var layerRect = rect;
            if (layer.HasOffset)
            {
                int pixelDX = (int)Math.Round(layer.OffsetX * rect.Width);
                int pixelDY = (int)Math.Round(-layer.OffsetY * rect.Height);
                layerRect = new Rectangle(rect.X + pixelDX, rect.Y + pixelDY, rect.Width, rect.Height);
            }

            // РЕШЕНИЕ ПРО ПОВОРОТ ПРИНИМАЕТСЯ НА КАЖДЫЙ СЛОЙ ОТДЕЛЬНО (а не одно на всю
            // композицию, как было раньше) — у разных слоёв одной и той же сущности
            // (например, "computerLayerBody" со state "computer" и "computerLayerScreen"
            // со state "alert-0") число directions в meta.json может отличаться. Если у
            // ЭТОГО слоя directions>=4, GetSourceRect уже выбирает нужный кадр по rotation
            // сам — аффинный поворот здесь не нужен и даже вреден (даёт двойной поворот:
            // кадр меняется под направление И весь слой ещё крутится как картинка,
            // из-за чего сущность визуально "не туда" смотрит и как будто застревает
            // на юге). Если же у слоя directions<4 (кадр всегда один и тот же) — нужен
            // обычный аффинный поворот картинки, как раньше для однослойных спрайтов.
            bool layerHasDirections = GetStateDirections(cacheKey) >= 4;

            void DrawThisLayer()
            {
                var layerSrc = GetSourceRect(cacheKey, layerTexture, rotation);

                // GetDecalTintAttributes — несмотря на название, просто строит ColorMatrix
                // тонирования по hex-цвету, пригодно для любого тонирования, не только декалей
                var layerTint = tint ?? (!string.IsNullOrEmpty(layer.Color) ? GetDecalTintAttributes(layer.Color) : null);

                if (layerTint != null)
                    g.DrawImage(layerTexture, layerRect, layerSrc.X, layerSrc.Y, layerSrc.Width, layerSrc.Height, GraphicsUnit.Pixel, layerTint);
                else
                    DrawPreservingAspect(g, layerTexture, layerRect, layerSrc);
            }

            if (layerHasDirections)
                DrawThisLayer();
            else
                WithRotation(g, cx, cy, rotation, DrawThisLayer);
        }

        if (!anyDrawn)
            fallback?.Invoke(g, rect);
    }


    /// <summary>
    /// Загружает и кэширует текстуру ОДНОГО слоя многослойного прототипа. cacheKey —
    /// составной ("protoId__layerN"), поэтому переиспользует те же словари
    /// (_textureCache/_protoTextureDirCache/_protoStateNameCache), что и GetOrLoadTexture
    /// для обычных однослойных текстур, без коллизий с реальными protoId.
    /// </summary>
    private Image? GetOrLoadLayerTexture(string cacheKey, string protoId, Models.SpriteLayer layer, string? stateOverride = null)
    {
        if (_textureCache.TryGetValue(cacheKey, out var cached))
            return cached;

        Image? texture = null;
        if (_indexer != null)
        {
            var texturePath = _indexer.GetLayerTexturePath(protoId, layer.SpritePath, layer.RsiPath, stateOverride ?? layer.State);
            if (texturePath != null && File.Exists(texturePath))
            {
                try
                {
                    texture = Image.FromFile(texturePath);
                    _protoTextureDirCache[cacheKey] = Path.GetDirectoryName(texturePath) ?? "";

                    string? stateName = stateOverride ?? layer.State;
                    _protoStateNameCache[cacheKey] = !string.IsNullOrEmpty(stateName)
                        ? stateName
                        : Path.GetFileNameWithoutExtension(texturePath);
                }
                catch { }
            }
        }

        _textureCache[cacheKey] = texture;
        return texture;
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

        // Кэш цветов/пер по итоговому цвету ребра
        var penCache = new Dictionary<string, Pen>();
        using var fallbackPen = new Pen(Color.Gray, 2);

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

        // Каждое ребро между двумя соседними трубами рисуем РОВНО ОДИН РАЗ.
        // Раньше перебирались все 4 направления у КАЖДОЙ трубы, и одно и то же
        // соединение рисовалось дважды — по разу с каждого конца, каждый раз
        // своим пером. Итоговый видимый цвет зависел от того, кто из двух узлов
        // рисуется позже в порядке сортировки по Y (тот и оказывался "сверху"),
        // а не от того, какой узел реально покрашен — отсюда несимметричная
        // подкраска (сверху/слева видно синий, снизу/справа остаётся зелёный).
        // Проверяя соседей только вправо и вниз, каждое ребро посещается один раз
        // суммарно по всем трубам — дублирования больше нет в принципе.
        var forwardDirections = new[] { (1, 0), (0, 1) };

        foreach (var pipe in pipes)
        {
            var pipeDict = pipeDicts[pipe.PipeType];
            foreach (var (dx, dy) in forwardDirections)
            {
                var key = (pipe.X + dx, pipe.Y + dy);
                if (!pipeDict.TryGetValue(key, out var neighbor)) continue;

                // Цвет ребра: если хоть у одного из двух узлов задан пользовательский
                // CustomColor — используем его (покрашенный узел приоритетнее дефолтного
                // цвета типа трубы), иначе — обычный цвет типа
                var edgeColor = pipe.CustomColor ?? neighbor.CustomColor ?? GetPipeColor(pipe.PipeType);
                bool hasCustom = pipe.CustomColor.HasValue || neighbor.CustomColor.HasValue;
                var cacheKey = hasCustom
                    ? $"{pipe.PipeType}|{edgeColor.ToArgb()}"
                    : pipe.PipeType;

                if (!penCache.TryGetValue(cacheKey, out var pen))
                {
                    pen = new Pen(edgeColor, Math.Max(2, tileSize / 10));
                    penCache[cacheKey] = pen;
                }

                var (cx, cy) = GetPipeNodeScreenCenter(pipe, tileSize, viewOffset, gridOffset);
                var (nx, ny) = GetPipeNodeScreenCenter(neighbor, tileSize, viewOffset, gridOffset);
                g.DrawLine(pen, cx, cy, nx, ny);
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
            var cacheKey = pipe.CustomColor.HasValue
                ? $"{pipe.PipeType}|{pipe.CustomColor.Value.ToArgb()}"
                : pipe.PipeType;

            if (!brushCache.TryGetValue(cacheKey, out var cached))
            {
                var color = pipe.CustomColor ?? GetPipeDotColor(pipe.PipeType);
                var brush = new SolidBrush(color);
                var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
                cached = (brush, borderPen);
                brushCache[cacheKey] = cached;
            }

            var (cx, cy) = GetPipeNodeScreenCenter(pipe, tileSize, viewOffset, gridOffset);

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

    private void DrawWireLinesBatch(Graphics g, List<WireEntity> wires, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (wires.Count == 0) return;

        var penCache = new Dictionary<string, Pen>();

        var wireDicts = new Dictionary<string, Dictionary<(float x, float y), WireEntity>>();
        foreach (var wire in wires)
        {
            if (!wireDicts.TryGetValue(wire.WireType, out var dict))
            {
                dict = new Dictionary<(float x, float y), WireEntity>();
                wireDicts[wire.WireType] = dict;
            }
            dict[(wire.X, wire.Y)] = wire;
        }

        // Каждое ребро — один раз (та же идея, что и в DrawPipeLinesBatch)
        var forwardDirections = new[] { (1, 0), (0, 1) };

        foreach (var wire in wires)
        {
            var wireDict = wireDicts[wire.WireType];
            foreach (var (dx, dy) in forwardDirections)
            {
                var key = (wire.X + dx, wire.Y + dy);
                if (!wireDict.TryGetValue(key, out _)) continue;

                if (!penCache.TryGetValue(wire.WireType, out var pen))
                {
                    pen = new Pen(GetWireColor(wire.WireType), Math.Max(2, tileSize / 10));
                    penCache[wire.WireType] = pen;
                }

                float cx = (wire.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (wire.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
                float nx = (key.Item1 + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                float ny = (key.Item2 + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
                g.DrawLine(pen, cx, cy, nx, ny);
            }
        }

        foreach (var p in penCache.Values)
            p?.Dispose();
    }

    private void DrawWireDotsBatch(Graphics g, List<WireEntity> wires, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (wires.Count == 0) return;

        float dotSize = Math.Max(4, tileSize / 6);
        var brushCache = new Dictionary<string, SolidBrush>();

        foreach (var wire in wires.OrderBy(w => w.Y))
        {
            if (!brushCache.TryGetValue(wire.WireType, out var brush))
            {
                brush = new SolidBrush(GetWireColor(wire.WireType));
                brushCache[wire.WireType] = brush;
            }

            float cx = (wire.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float cy = (wire.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
        }

        foreach (var b in brushCache.Values)
            b.Dispose();
    }

    private Color GetWireColor(string wireType)
    {
        // В отличие от GetPipeColor (жёстко зашитые цвета труб), цвет кабеля
        // берётся из WireTypeManager — так диалог "Настройки" реально влияет
        // на отрисовку, а не только на иконки инструментов
        return _wireTypeManager.GetWireType(wireType).Color;
    }

    /// <summary>
    /// Зелёная область покрытия НВ-кабеля радиусом 3 клетки — закрашиваются конкретные
    /// ТАЙЛЫ, чей центр попадает в радиус от узла кабеля. Форма области — КРУГ:
    /// расстояние считается евклидовым (√(dx² + dy²)), поэтому диагональный шаг "стоит"
    /// как √2, а не как два обычных шага, и углы области срезаны (в отличие от ромба
    /// при манхэттенской метрике и квадрата при чебышёвской). Узлы кабеля хранятся
    /// в целых координатах тайла, поэтому расстояние считается между ЦЕНТРАМИ тайлов
    /// (обе точки — тайл + 0.5). Все покрытые тайлы от всех НВ-узлов собираются в один
    /// HashSet, поэтому пересекающиеся зоны не дают двойной альфы.
    /// </summary>
    private void DrawLvCoverageOverlay(Graphics g, List<WireEntity> wires, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var lvWires = wires.Where(w => w.WireType == "LV").ToList();
        if (lvWires.Count == 0) return;

        const int radiusTiles = 3;

        var coveredTiles = new HashSet<(int x, int y)>();

        foreach (var wire in lvWires)
        {
            int wireTileX = (int)Math.Floor(wire.X);
            int wireTileY = (int)Math.Floor(wire.Y);

            for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
            {
                for (int dy = -radiusTiles; dy <= radiusTiles; dy++)
                {
                    // Евклидово расстояние в клетках: даёт круг.
                    if (dx * dx + dy * dy > radiusTiles * radiusTiles) continue;

                    coveredTiles.Add((wireTileX + dx, wireTileY + dy));
                }
            }
        }

        if (coveredTiles.Count == 0) return;

        using var brush = new SolidBrush(Color.FromArgb(50, 40, 200, 40));
        foreach (var (tx, ty) in coveredTiles)
        {
            var rect = ToRect(tx, ty, tileSize, viewOffset, gridOffset);
            g.FillRectangle(brush, rect);
        }
    }

    /// <summary>
    /// Область обзора камер: закрашиваются тайлы в КВАДРАТЕ со стороной 6.5 клетки
    /// от камеры (расстояние по Чебышёву — max(|dx|,|dy|), поэтому область квадратная,
    /// а не круглая и не ромб, как у НВ-кабеля). Охвачены все камеры: и «привязки»
    /// из свойств комнаты (CameraEntity), и камеры, поставленные обычным прототипом
    /// из панели репозитория (MapEntity с прототипом SurveillanceCamera*).
    /// Пересекающиеся зоны не дают двойной альфы — все покрытые тайлы собираются
    /// в один HashSet, а за экран области не считаются вовсе (IsTileVisible).
    /// </summary>
    private void DrawCameraCoverageOverlay(Graphics g, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        // Половина стороны квадрата: 6.5 клетки дают 13×13 тайлов (тайлы с шагом
        // до 6 от клетки камеры включительно).
        const float halfSideTiles = 6.5f;

        // Не один цикл по сущностям с фильтрацией по прототипу: «привязка»-камера
        // хранит прототип в CameraProto, обычная сущность — в Proto.
        var cameras = new List<(float X, float Y)>();
        foreach (var entity in grid.Entities)
        {
            string proto = entity is CameraEntity camera ? camera.CameraProto : entity.Proto;
            if (!IsCameraProto(proto)) continue;
            cameras.Add((entity.X, entity.Y));
        }

        if (cameras.Count == 0) return;

        int scanRadius = (int)Math.Ceiling(halfSideTiles);
        var covered = new HashSet<(int x, int y)>();

        foreach (var (camX, camY) in cameras)
        {
            int cx = (int)Math.Floor(camX);
            int cy = (int)Math.Floor(camY);

            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                int tx = cx + dx;
                for (int dy = -scanRadius; dy <= scanRadius; dy++)
                {
                    int ty = cy + dy;
                    if (!IsTileVisible(tx, ty, visibleRect)) continue;

                    // Расстояние по Чебышёву — max(|dx|,|dy|): одинаковый шаг по любой
                    // оси, поэтому область получается квадратом, а не кругом.
                    float ndx = Math.Abs(tx + 0.5f - camX);
                    float ndy = Math.Abs(ty + 0.5f - camY);
                    if (ndx > halfSideTiles || ndy > halfSideTiles) continue;

                    covered.Add((tx, ty));
                }
            }
        }

        if (covered.Count == 0) return;

        using var brush = new SolidBrush(Color.FromArgb(45, 60, 140, 230));
        foreach (var (tx, ty) in covered)
        {
            var rect = ToRect(tx, ty, tileSize, viewOffset, gridOffset);
            g.FillRectangle(brush, rect);
        }
    }

    /// <summary>
    /// Прототип относится к камерам видеонаблюдения (для области обзора).
    /// Охватываются и стационарные (SurveillanceCamera*), и беспроводные
    /// (SurveillanceWirelessCamera*); роутеры, платы и мониторы — нет.
    /// </summary>
    private static bool IsCameraProto(string? proto)
    {
        if (string.IsNullOrEmpty(proto)) return false;
        if (!proto.StartsWith("SurveillanceCamera") && !proto.StartsWith("SurveillanceWirelessCamera"))
            return false;
        return !proto.Contains("Router") &&
               !proto.Contains("Circuitboard") &&
               !proto.Contains("Monitor") &&
               !proto.Contains("Assembly");
    }

    private void DrawEndpointMarkers(Graphics g, List<PipeEntity> pipes, List<PipeEntity>? neighborSource,
        int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        var dirs = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

        // Соседей берём из полного списка труб (если он передан), а рисуем только
        // переданные маркеры — обычно это трубы, попавшие в видимую область экрана.
        var allForNeighbors = neighborSource ?? pipes;

        // Группируем трубы по слою: конец определяется по соседям того же слоя
        foreach (var group in pipes.GroupBy(p => p.PipeType))
        {
            var groupPipes = group.ToList();
            var posSet = new HashSet<(int x, int y)>();
            foreach (var p in allForNeighbors)
                if (p.PipeType == group.Key)
                    posSet.Add(((int)p.X, (int)p.Y));

            foreach (var pipe in groupPipes)
            {
                int neighbors = 0;
                var px = (int)pipe.X;
                var py = (int)pipe.Y;
                foreach (var (dx, dy) in dirs)
                {
                    if (posSet.Contains((px + dx, py + dy)))
                        neighbors++;
                }

                // Не конец трубы — пропускаем
                if (neighbors != 1) continue;

                if (pipe.PipeType == "Util")
                {
                    // Утилизация: на конце трубы рисуем спрайт соответствующей мебели
                    // — DisposalUnit или MailingUnit — так же, как обычные трубы рисуют
                    // спрайт вентиляции (DrawLastLayerOnly). Раньше здесь была просто
                    // цветная точка-маркер, теперь — реальный спрайт приёмника.
                    string unitProto = pipe.EndpointType == EndpointType.MailingUnit
                        ? "MailingUnit"
                        : "DisposalUnit";

                    int cellX = (int)Math.Round((pipe.X + gridOffset.X) * tileSize - viewOffset.X);
                    int cellY = (int)Math.Round((pipe.Y + gridOffset.Y) * tileSize - viewOffset.Y);
                    var cellRect = new Rectangle(cellX, cellY, tileSize, tileSize);
                    // Для почтового/утилизаторного блока на узле трубы рисуем ТОЛЬКО базовый
                    // слой корпуса (Base → mailing.png / disposal.png), а не всю композицию
                    // слоёв: на маленьком узле трубы оверлеи (ручка/зарядка/сброс) только
                    // захламляют картинку, и блок должен выглядеть как обычная единица сети.
                    DrawBaseLayerOnly(g, unitProto, cellRect);

                    // Тег на MailingUnit (текст под/над приёмником)
                    if (pipe.EndpointType == EndpointType.MailingUnit && !string.IsNullOrEmpty(pipe.FilterLabel))
                    {
                        var tag = pipe.FilterLabel.TrimEnd(',');
                        using var font = new Font("Arial", Math.Max(7f, tileSize / 5f));
                        using var brush = new SolidBrush(Color.White);
                        var size = g.MeasureString(tag, font);
                        g.DrawString(tag, font, brush, cellX + tileSize / 2f - size.Width / 2, cellY - size.Height);
                    }
                }
                else
                {
                    // Обычные слои (Distra/Waste/Normal): рисуем на конце трубы
                    // спрайт вентиляции, указанный в настройках (VentProto из
                    // настроек слоя/узла). Для "None" прототипа нет — спрайт не
                    // загрузится, и покажется просто конец трубы.
                    string ventProto = !string.IsNullOrEmpty(pipe.VentProto)
                        ? pipe.VentProto!
                        : DefaultVentProtoForLayer(pipe.PipeType);

                    int cellX = (int)Math.Round((pipe.X + gridOffset.X) * tileSize - viewOffset.X);
                    int cellY = (int)Math.Round((pipe.Y + gridOffset.Y) * tileSize - viewOffset.Y);
                    var cellRect = new Rectangle(cellX, cellY, tileSize, tileSize);
                    // Рисуем только последний слой (state) вентиляции
                    DrawLastLayerOnly(g, ventProto, cellRect);
                }
            }
        }
    }

    /// <summary>
    /// Рисует спрайт прототипа, используя ТОЛЬКО последний видимый слой (state)
    /// из его Sprite.layers, а не всю композицию слоёв друг поверх друга. Нужно
    /// для концов труб, где вентиляция должна отображаться одним финальным слоем.
    /// Если слоёв нет — рисуем обычным путём (DrawTexturedRect).
    /// </summary>
    private void DrawLastLayerOnly(Graphics g, string protoId, Rectangle rect)
    {
        var proto = _indexer?.FindPrototype(protoId);
        if (proto == null || proto.Layers.Count == 0)
        {
            DrawTexturedRect(g, protoId, rect, null, null, 0f);
            return;
        }

        // Ищем последний ВИДИМЫЙ слой (обычно он же — верхний в Z-порядке)
        for (int i = proto.Layers.Count - 1; i >= 0; i--)
        {
            var layer = proto.Layers[i];
            if (!layer.Visible) continue;

            string cacheKey = $"{protoId}__layer{i}";
            var tex = GetOrLoadLayerTexture(cacheKey, protoId, layer);
            if (tex == null) continue;

            var src = GetSourceRect(cacheKey, tex, 0f);
            if (src.Width > 0 && src.Height > 0)
                DrawPreservingAspect(g, tex, rect, src);
            return;
        }

        DrawTexturedRect(g, protoId, rect, null, null, 0f);
    }

    /// <summary>
    /// Рисует ТОЛЬКО базовый слой (корпус) многослойного прототипа — слой, чей map-ключ
    /// содержит "Base" (например, "enum.DisposalUnitVisualLayers.Base" → state "mailing"/
    /// "disposal"). Если такого слоя нет — берём первый видимый слой. Нужно для маленьких
    /// маркеров (конец утиль-трубы), где композиция всех слоёв захламляет картинку, а
    /// последний слой — почти пустой оверлей ручки.
    /// </summary>
    private void DrawBaseLayerOnly(Graphics g, string protoId, Rectangle rect)
    {
        var proto = _indexer?.FindPrototype(protoId);
        if (proto == null || proto.Layers.Count == 0)
        {
            DrawTexturedRect(g, protoId, rect, null, null, 0f);
            return;
        }

        var visible = proto.Layers.Where(l => l.Visible).ToList();
        // Сначала слой с map-ключом Base (корпус), иначе первый видимый
        Models.SpriteLayer? target = visible.FirstOrDefault(l => !string.IsNullOrEmpty(l.MapKey) && l.MapKey.Contains("Base"))
                                   ?? visible.FirstOrDefault();
        if (target == null)
        {
            DrawTexturedRect(g, protoId, rect, null, null, 0f);
            return;
        }

        int idx = proto.Layers.IndexOf(target);
        string cacheKey = $"{protoId}__layer{idx}";
        var tex = GetOrLoadLayerTexture(cacheKey, protoId, target);
        if (tex == null)
        {
            DrawTexturedRect(g, protoId, rect, null, null, 0f);
            return;
        }

        var src = GetSourceRect(cacheKey, tex, 0f);
        if (src.Width > 0 && src.Height > 0)
            DrawPreservingAspect(g, tex, rect, src);
        else
            DrawTexturedRect(g, protoId, rect, null, null, 0f);
    }
    /// <summary>
    /// Дефолтный тип вентиляции для конца трубы слоя, если у конкретного узла
    /// не запечён свой VentProto (старые карты). Совпадает с экспортной логикой
    /// YAMLGenerator.
    /// </summary>
    private static string DefaultVentProtoForLayer(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => "GasVentPump",
            "Normal" => "None",
            _ => "GasVentScrubber"
        };
    }

    /// <summary>
    /// Рисует текст по центру точки (cx, cy).
    /// </summary>
    private static void DrawCenteredText(Graphics g, string text, Font font, Color color, float cx, float cy)
    {
        using var brush = new SolidBrush(color);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, cx - size.Width / 2, cy - size.Height / 2);
    }

    private void DrawFilterMarkers(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var markedPipes = pipes.Where(p => p.HasFilterMarker).ToList();
        if (markedPipes.Count == 0) return;

        float squareSize = tileSize / 2f;

        foreach (var pipe in markedPipes)
        {
            // Квадрат рисуется прямо поверх узла ("базовой трубы" — со смещением
            // только у Distra/Waste, см. GetPipeNodeScreenCenter)
            var (cx, cy) = GetPipeNodeScreenCenter(pipe, tileSize, viewOffset, gridOffset);

            using var brush = new SolidBrush(Color.FromArgb(255, 200, 200, 100)); // жёлтый, как у развилки
            g.FillRectangle(brush, cx - squareSize / 2, cy - squareSize / 2, squareSize, squareSize);

            // Рисуем текст маркера
            if (!string.IsNullOrEmpty(pipe.FilterLabel))
            {
                using var textBrush = new SolidBrush(Color.White);
                var font = new Font("Arial", Math.Max(7f, squareSize / 4f));
                var textSize = g.MeasureString(pipe.FilterLabel, font);
                g.DrawString(pipe.FilterLabel, font, textBrush,
                    cx - textSize.Width / 2,
                    cy - textSize.Height / 2);
            }
        }
    }

    private void DrawPipeFlowArrows(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var utilPipes = pipes.Where(p => p.PipeType == "Util").ToList();
        if (utilPipes.Count == 0) return;

        // Строим словарь позиций для утилизации
        var utilDict = new Dictionary<(float x, float y), PipeEntity>();
        foreach (var p in utilPipes)
            utilDict[(p.X, p.Y)] = p;

        foreach (var pipe in utilPipes)
        {
            // Считаем соседей
            var neighbors = 0;
            var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            foreach (var (dx, dy) in directions)
            {
                if (utilDict.ContainsKey((pipe.X + dx, pipe.Y + dy)))
                    neighbors++;
            }

            // Рисуем стрелку на развилках (3) и перекрёстках (4)
            if (neighbors != 3 && neighbors != 4) continue;

            var (cx, cy) = GetPipeNodeScreenCenter(pipe, tileSize, viewOffset, gridOffset);

            float arrowSize = tileSize / 2f;

            // Определяем направление стрелки (0=юг, 1=запад, 2=север, 3=восток)
            float angle = pipe.UtilArrowRotation * (float)(Math.PI / 2);
            float sin = (float)Math.Sin(angle);
            float cos = (float)Math.Cos(angle);

            // Форма стрелки — треугольник с выемкой у основания
            var basePoints = new[]
            {
                    new PointF(0, -arrowSize),                // наконечник
                    new PointF(-arrowSize * 0.5f, arrowSize * 0.15f),  // левое плечо
                    new PointF(0, arrowSize * 0.1f),          // центр выемки
                    new PointF(arrowSize * 0.5f, arrowSize * 0.15f)    // правое плечо
                };

            // Поворачиваем и смещаем
            var points = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                points[i] = new PointF(
                    cx + basePoints[i].X * cos - basePoints[i].Y * sin,
                    cy + basePoints[i].X * sin + basePoints[i].Y * cos
                );
            }

            // Стрелка красится в цвет самой трубы (CustomColor, если задан
            // пользователем), а не в фиксированный зелёный — иначе перекрашенная
            // в другой цвет Util-труба на стыках выглядела бы "смешанной" с
            // исходным зелёным из-за наложения непрозрачной зелёной стрелки поверх
            var arrowColor = pipe.CustomColor ?? Color.FromArgb(220, 30, 60, 5);
            using var arrowBrush = new SolidBrush(arrowColor);
            g.FillPolygon(arrowBrush, points);
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

    #region Объединённый рендеринг по DrawDepth

    /// <summary>
    /// Рисует линии связей беспроводной сети (кнопки/рычаги → устройства).
    /// Показываются, если включён общий режим ShowNetworkLinks (все связи),
    /// либо связи, в которых участвуют клетки из NetworkHighlightPositions
    /// (выбранное устройство — рисуются ярко).
    /// </summary>
    private void DrawEntityNetworkLinks(Graphics g, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        bool showAll = ShowNetworkLinks;
        var hi = NetworkHighlightPositions;
        if (!showAll && (hi == null || hi.Count == 0)) return;

        var linksEntities = grid.Entities.OfType<MapEntity>()
            .Where(e => e.NetworkLinks != null && e.NetworkLinks.Count > 0)
            .ToList();
        if (linksEntities.Count == 0) return;

        int NodeScreenX(float wx) => (int)((wx + 0.5f + gridOffset.X) * tileSize - viewOffset.X);
        int NodeScreenY(float wy) => (int)((wy + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y);

        using var dimPen = new Pen(Color.FromArgb(90, 255, 220, 60), 2);
        using var brightPen = new Pen(Color.FromArgb(255, 255, 220, 60), 3);

        foreach (var entity in linksEntities)
        {
            var srcCell = ((int)Math.Round(entity.X), (int)Math.Round(entity.Y));
            bool srcSelected = hi != null && hi.Contains(srcCell);

            int sx = NodeScreenX(entity.X);
            int sy = NodeScreenY(entity.Y);

            foreach (var link in entity.NetworkLinks)
            {
                var tgtCell = ((int)Math.Round(link.TargetX), (int)Math.Round(link.TargetY));
                bool tgtSelected = hi != null && hi.Contains(tgtCell);

                if (!showAll && !srcSelected && !tgtSelected) continue;

                int tx = NodeScreenX(link.TargetX);
                int ty = NodeScreenY(link.TargetY);

                bool emphasize = hi != null && hi.Count > 0 && (srcSelected || tgtSelected);

                // ВАЖНО: не оборачивать в using общий pen — иначе первый же DrawLine
                // диспоузит его, и следующий вызов бросает "Parameter is not valid".
                // dim/brightPen живут весь метод и диспоузятся в его конце.
                var pen = emphasize ? brightPen : dimPen;
                g.DrawLine(pen, sx, sy, tx, ty);

                if (emphasize)
                {
                    using var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 220, 60));
                    g.FillEllipse(dotBrush, sx - 3, sy - 3, 6, 6);
                    g.FillEllipse(dotBrush, tx - 3, ty - 3, 6, 6);
                }
            }
        }
    }

    private void DrawRenderLayer(Graphics g, TileGrid tileGrid, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset, float opacity, RectangleF visibleRect)
    {
        var renderQueue = new List<(double WorldY, int DrawDepthOffset, int LayerOrder, int InsertOrder, Action draw)>();
        int _insertCounter = 0;

        // Индекс IconSmooth (тайл → smooth-ключи): собирает И тайлы-стены, И сущности, чтобы
        // стены из генератора комнат и из панели репозитория сглаживались друг с другом.
        BuildIconSmoothIndex(grid, tileGrid, visibleRect);

        // Пол
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Floor))
        {
            if (!IsTileVisible(tile.X, tile.Y, visibleRect)) continue;
            int dd = tile.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(tile.ProtoId ?? "Plating");
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("FloorTiles");
            var t = tile;
            renderQueue.Add((t.Y, dd, 0, _insertCounter++, () => DrawSingleTile(g, t.X, t.Y, t.ProtoId ?? "Plating", tileSize, viewOffset, gridOffset, true, opacity)));
        }

        // Пол под дверями
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            if (tile.HasFloorUnder && !string.IsNullOrEmpty(tile.FloorProtoUnder))
            {
                int dd = 0;
                if (_indexer != null)
                {
                    var ddName = _indexer.GetDrawDepth(tile.FloorProtoUnder);
                    if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
                }
                if (dd == 0) dd = _drawDepthManager.GetOffset("FloorTiles");
                var t = tile;
                renderQueue.Add((t.Y, dd, 0, _insertCounter++, () => DrawSingleTile(g, t.X, t.Y, t.FloorProtoUnder!, tileSize, viewOffset, gridOffset, true, opacity)));
            }
        }

        // Стены
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            if (!IsTileVisible(tile.X, tile.Y, visibleRect)) continue;
            int dd = tile.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(tile.ProtoId ?? "WallSolid");
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("Walls");
            var t = tile;
            renderQueue.Add((t.Y, dd, 0, _insertCounter++, () => DrawWallTile(g, tileGrid, t.X, t.Y, t.ProtoId ?? "WallSolid", tileSize, viewOffset, gridOffset, opacity)));
        }

        // Двери
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            if (!IsTileVisible(tile.X, tile.Y, visibleRect)) continue;
            int dd = tile.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(tile.ProtoId ?? "Airlock");
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("Doors");
            var t = tile;
            renderQueue.Add((t.Y, dd, 0, _insertCounter++, () => DrawSingleDoor(g, t.X, t.Y, t.ProtoId ?? "Airlock", tileSize, viewOffset, gridOffset)));
        }

        // Декали
        // Декали — украшения пола/стен: они рисуются ПОВЕРХ только тайлов (пола/стен), но
        // ВСЕГДА ПОД любыми сущностями (мебель, ковры, мобы). Ковры и прочий floor-декор —
        // это сущности на уровне "FloorObjects" и выше, поэтому полку декалей фиксируем на
        // один слой НИЖЕ самой нижней сущности (FloorObjects - 1). Это ставит декали сразу
        // над тайлами пола и под ЖЕ любым объектом, независимо от глубины прототипа декали
        // (раньше декаль резолвилась до своего drawdepth — WallTops/Walls — и вылезала
        // поверх ковров; а потолок Objects-1 её от этого не спасал).
        // Единая полка также означает, что среди декалей наслоение целиком решает
        // LayerOrder (порядок слоёв из «Наследования декалей»), а не глубина прототипа.
        int decalShelfDepth = _drawDepthManager.GetOffset("FloorObjects") - 1;
        // Ковры (сущности с id "Carpet*") — отдельная полка ровно НА ОДИН слой выше
        // декалей (FloorObjects вместо FloorObjects-1). В игре ковры рисуются на
        // drawdepth "FloorObjects" — поверх пола и декалей, но под мебелью/мобами,
        // поэтому и в редакторе они должны быть над декалями, а не под ними.
        int carpetShelfDepth = decalShelfDepth + 1;
        foreach (var decal in grid.Decals)
        {
            if (!IsPointVisible(decal.X, decal.Y, visibleRect)) continue;
            var d = decal;
            int layerOrder = d.PatternLayerIndex;
            renderQueue.Add((d.Y, decalShelfDepth, layerOrder, _insertCounter++, () => DrawSingleDecal(g, d, tileSize, viewOffset, gridOffset, opacity)));
        }

        // Ковры (сущности с id "Carpet*") — рисуем на отдельной полке поверх декалей,
        // принудительно игнорируя drawdepth прототипа (иначе ковёр с drawdepth ниже
        // FloorObjects провалился бы под декали). Внутри себя ковры сортируем по Y.
        foreach (var entity in grid.Entities)
        {
            if (entity.Proto == null ||
                !entity.Proto.StartsWith("Carpet", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsPointVisible(entity.X, entity.Y, visibleRect)) continue;
            var c = entity;
            renderQueue.Add((c.Y, carpetShelfDepth, 0, _insertCounter++, () => DrawSingleEntity(g, c.Proto, c.X, c.Y, c.Rotation, tileSize, viewOffset, gridOffset)));
        }

        // Огнешлюзы
        foreach (var firelock in grid.Entities.OfType<FirelockEntity>())
        {
            if (!IsPointVisible(firelock.X, firelock.Y, visibleRect)) continue;
            int dd = firelock.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(firelock.Proto);
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            var f = firelock;
            renderQueue.Add((f.Y, dd, 0, _insertCounter++, () => DrawSingleFirelock(g, f, tileSize, viewOffset, gridOffset)));
        }

        // Сигнализации (AirAlarm / FireAlarm) — теперь внутри общей сортировки
        foreach (var entity in grid.Entities.OfType<AirAlarmEntity>())
        {
            if (!IsPointVisible(entity.X, entity.Y, visibleRect)) continue;
            int dd = entity.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(entity.Proto);
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("Objects");
            var a = entity;
            renderQueue.Add((a.Y, dd, 0, _insertCounter++, () => DrawSingleAlarm(g, a, tileSize, viewOffset, gridOffset, "AirAlarm", Color.FromArgb(200, 255, 200, 100))));
        }
        foreach (var entity in grid.Entities.OfType<FireAlarmEntity>())
        {
            if (!IsPointVisible(entity.X, entity.Y, visibleRect)) continue;
            int dd = entity.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(entity.Proto);
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("Objects");
            var f = entity;
            renderQueue.Add((f.Y, dd, 0, _insertCounter++, () => DrawSingleAlarm(g, f, tileSize, viewOffset, gridOffset, "FireAlarm", Color.FromArgb(200, 255, 100, 100))));
        }

        // Generic entities (без труб, огнешлюзов и сигнализаций — у них свои
        // отдельные циклы выше в этом же методе с другим якорем позиционирования
        // (top-left, а не центр тайла); без этого исключения та же сущность
        // рендерилась второй раз с ошибочным смещением на пол-тайла влево-вверх)
        foreach (var entity in grid.Entities)
        {
            if (entity is PipeEntity or FirelockEntity or AirAlarmEntity or FireAlarmEntity or WireEntity or HolopadEntity or LockerEntity or SuitStorageEntity or BedEntity or CameraEntity or DresserEntity or IntercomEntity or FaxMachineEntity) continue;
            if (entity.Proto != null && entity.Proto.StartsWith("Carpet", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsPointVisible(entity.X, entity.Y, visibleRect)) continue;
            int dd = entity.DrawDepthOffset;
            if (dd == 0 && _indexer != null)
            {
                var ddName = _indexer.GetDrawDepth(entity.Proto);
                if (!string.IsNullOrEmpty(ddName)) dd = _drawDepthManager.GetOffset(ddName);
            }
            if (dd == 0) dd = _drawDepthManager.GetOffset("Objects");
            var e = entity;
            renderQueue.Add((e.Y, dd, 0, _insertCounter++, () => DrawSingleEntity(g, e.Proto, e.X, e.Y, e.Rotation, tileSize, viewOffset, gridOffset)));
        }

        // Голопады — отдельный слой «привязок» поверх остальных объектов
        foreach (var holopad in grid.Entities.OfType<HolopadEntity>())
        {
            if (!IsPointVisible(holopad.X, holopad.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var h = holopad;
            renderQueue.Add((h.Y, dd, 0, _insertCounter++, () => DrawSingleHolopad(g, h.HolopadProto, h.X, h.Y, tileSize, viewOffset, gridOffset)));
        }

        // Шкафчики — отдельный слой «привязок» поверх остальных объектов
        foreach (var locker in grid.Entities.OfType<LockerEntity>())
        {
            if (!IsPointVisible(locker.X, locker.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var l = locker;
            renderQueue.Add((l.Y, dd, 0, _insertCounter++, () => DrawSingleLocker(g, l.LockerProto, l.X, l.Y, tileSize, viewOffset, gridOffset)));
        }

        // Хранилища скафандров — отдельный слой «привязок» поверх остальных объектов
        foreach (var suit in grid.Entities.OfType<SuitStorageEntity>())
        {
            if (!IsPointVisible(suit.X, suit.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var s = suit;
            renderQueue.Add((s.Y, dd, 0, _insertCounter++, () => DrawSingleSuitStorage(g, s.SuitStorageProto, s.X, s.Y, tileSize, viewOffset, gridOffset)));
        }

        // Кровати — отдельный слой «привязок» поверх остальных объектов
        foreach (var bed in grid.Entities.OfType<BedEntity>())
        {
            if (!IsPointVisible(bed.X, bed.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var b = bed;
            renderQueue.Add((b.Y, dd, 0, _insertCounter++, () => DrawSingleBed(g, b.BedProto, b.BedsheetProto, b.X, b.Y, tileSize, viewOffset, gridOffset)));
        }

        // Камеры — отдельный слой «привязок» поверх остальных объектов. Поворот
        // определяется автоматически по ближайшей стене (0 = стена с юга от тайла).
        foreach (var camera in grid.Entities.OfType<CameraEntity>())
        {
            if (!IsPointVisible(camera.X, camera.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var c = camera;
            float rotation = WallMountOrientation.ResolveRotation(grid, tileGrid, c.X, c.Y, c.Rotation);
            renderQueue.Add((c.Y, dd, 0, _insertCounter++, () => DrawSingleCamera(g, c.CameraProto, rotation, c.X, c.Y, tileSize, viewOffset, gridOffset)));
        }

        // Комоды — отдельный слой «привязок» поверх остальных объектов
        foreach (var dresser in grid.Entities.OfType<DresserEntity>())
        {
            if (!IsPointVisible(dresser.X, dresser.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var d = dresser;
            renderQueue.Add((d.Y, dd, 0, _insertCounter++, () => DrawSingleDresser(g, d.DresserProto, d.X, d.Y, tileSize, viewOffset, gridOffset)));
        }

        // Интеркомы — отдельный слой «привязок»; лежат в тайле-стены и развёрнуты
        // гранью в комнату (поворот сохранён при установке).
        foreach (var intercom in grid.Entities.OfType<IntercomEntity>())
        {
            if (!IsPointVisible(intercom.X, intercom.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var i = intercom;
            renderQueue.Add((i.Y, dd, 0, _insertCounter++, () => DrawSingleIntercom(g, i.IntercomProto, i.Rotation, i.X, i.Y, tileSize, viewOffset, gridOffset)));
        }

        // Факсы — отдельный слой «привязок»; стоят по центру клетки, как комод,
        // отличаются прототипом (FaxMachineBase / FaxMachineCaptain) и адресом.
        foreach (var fax in grid.Entities.OfType<FaxMachineEntity>())
        {
            if (!IsPointVisible(fax.X, fax.Y, visibleRect)) continue;
            int dd = _drawDepthManager.GetOffset("Objects") + 1;
            var f = fax;
            renderQueue.Add((f.Y, dd, 0, _insertCounter++, () => DrawSingleFax(g, f.FaxProto, f.X, f.Y, tileSize, viewOffset, gridOffset)));
        }

        // Сортируем: сначала по слою DrawDepthOffset (меньше = ниже/заднее),
        // затем по порядку слоя декали (LayerOrder — как настроено в окне Decal Rule;
        // для всего, что не декаль, всегда 0, поэтому на них это не влияет),
        // и только затем внутри всего этого — по Y (меньше Y = дальше на экране = заднее)
        renderQueue.Sort((a, b) =>
        {
            int ddComp = a.DrawDepthOffset.CompareTo(b.DrawDepthOffset);
            if (ddComp != 0) return ddComp;
            int layerComp = a.LayerOrder.CompareTo(b.LayerOrder);
            if (layerComp != 0) return layerComp;
            return a.WorldY.CompareTo(b.WorldY);
        });

        foreach (var item in renderQueue)
        {
            item.draw();
        }

        // Связи беспроводной сети — поверх всего слоя
        DrawEntityNetworkLinks(g, grid, tileSize, viewOffset, gridOffset);
    }

    /// <summary>
    /// Стена-тайл: если прототип (или предок) имеет IconSmooth — рисует её сглаженной
    /// (как и сущности-столы), иначе — обычный тайл. Соседи стены — соседние тайлы
    /// TileContent.Wall в tileGrid (стены в этом редакторе лежат на тайлах, а не как сущности).
    /// </summary>
    private void DrawWallTile(Graphics g, TileGrid tileGrid, int x, int y, string protoId, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (_indexer?.GetIconSmoothInfo(protoId) is { } smoothInfo && smoothInfo.Enabled && smoothInfo.Base != null)
        {
            // Соседи по 8 направлениям ищем в едином индексе (_smoothKeysByTile), куда входят
            // и тайлы-стены (генератор комнат), и сущности (панель репозитория) — поэтому стены
            // из обоих источников сглаживаются друг с другом.
            bool[] m = SmoothMatchNeighbors(smoothInfo, x, y);

            int[] fills = ComputeCornerFills(m); // [NE,NW,SW,SE]
            int quarter = 0; // стены SnapgridCenter, поворот не применяем
            int[] layerFills = RotateLayerFills(fills, quarter); // [SE,NE,NW,SW]

            var rect = ToRect(x, y, tileSize, viewOffset, gridOffset); // стена-тайл рисуется от угла клетки

            bool anyLoaded = false;
            Image?[] imgs = new Image?[4];
            string[] cacheKeys = new string[4];
            for (int li = 0; li < 4; li++)
            {
                int state = layerFills[li];
                imgs[li] = GetOrLoadIconSmoothState(protoId, smoothInfo, state);
                cacheKeys[li] = $"{protoId}||{smoothInfo.Base}{state}";
                if (imgs[li] != null) anyLoaded = true;
            }
            if (!anyLoaded)
            {
                // Падаем в обычный рендер тайла
                DrawSingleTile(g, x, y, protoId, tileSize, viewOffset, gridOffset, false, opacity);
                return;
            }

            for (int li = 0; li < 4; li++)
            {
                Image? stateImg = imgs[li];
                if (stateImg == null) continue;
                float layerAngle = (float)IconSmoothLayerBaseAngle[li];
                Rectangle src = GetSourceRect(cacheKeys[li], stateImg, layerAngle);
                if (src.Width == 0 || src.Height == 0) continue;
                g.DrawImage(stateImg, rect, src, GraphicsUnit.Pixel);
            }
            return;
        }

        DrawSingleTile(g, x, y, protoId, tileSize, viewOffset, gridOffset, false, opacity);
    }

    private void DrawSingleTile(Graphics g, float worldX, float worldY, string protoId, int tileSize, PointF viewOffset, PointF gridOffset, bool isFloor, float opacity)
    {
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset);
        var texture = GetOrLoadTexture(protoId);
        var srcRect = texture != null ? GetSourceRect(protoId, texture, 0f) : Rectangle.Empty;

        if (texture != null)
        {
            if (isFloor)
            {
                g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
            }
            else
            {
                if (srcRect.Width != srcRect.Height && srcRect.Width > 0 && srcRect.Height > 0)
                {
                    float ratio = (float)srcRect.Width / srcRect.Height;
                    int drawW = tileSize;
                    int drawH = Math.Max(1, (int)(tileSize / ratio));
                    int drawX = rect.X + (rect.Width - drawW) / 2;
                    int drawY = rect.Y + (rect.Height - drawH) / 2;
                    g.DrawImage(texture, new Rectangle(drawX, drawY, drawW, drawH), srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
            }
        }
        else
        {
            if (isFloor)
            {
                using var brush = new SolidBrush(Color.FromArgb((int)(150 * opacity), 200, 200, 200));
                g.FillRectangle(brush, rect);
            }
            else
            {
                using var pen = new Pen(Color.Gray, 1);
                g.DrawRectangle(pen, rect);
            }
        }
    }

    private void DrawSingleDoor(Graphics g, float worldX, float worldY, string protoId, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset);
        var texture = GetOrLoadTexture(protoId);
        var srcRect = texture != null ? GetSourceRect(protoId, texture, 0f) : Rectangle.Empty;

        if (texture != null)
        {
            if (srcRect.Width != srcRect.Height && srcRect.Width > 0 && srcRect.Height > 0)
            {
                float ratio = (float)srcRect.Width / srcRect.Height;
                int drawW = tileSize;
                int drawH = Math.Max(1, (int)(tileSize / ratio));
                int drawX = rect.X + (rect.Width - drawW) / 2;
                int drawY = rect.Y + (rect.Height - drawH) / 2;
                g.DrawImage(texture, new Rectangle(drawX, drawY, drawW, drawH), srcRect, GraphicsUnit.Pixel);
            }
            else
            {
                g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
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

    private void DrawSingleDecal(Graphics g, PlacedDecal decal, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        var rect = ToRect(decal.X, decal.Y, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);
        float cx = rect.X + tileSize / 2f;
        float cy = rect.Y + tileSize / 2f;

        WithRotation(g, cx, cy, decal.Rotation, () =>
        {
            var tintAttrs = GetDecalTintAttributes(decal.Color);
            DrawTexturedRect(g, decal.Proto, rect, tintAttrs, (gg, r) =>
            {
                var fallbackColor = ParseDecalColor(decal.Color);
                using var brush = new SolidBrush(Color.FromArgb(
                    (int)(160 * opacity),
                    fallbackColor.R, fallbackColor.G, fallbackColor.B));
                gg.FillRectangle(brush, r);
            });
        });
    }

    private void DrawSingleFirelock(Graphics g, FirelockEntity firelock, int tileSize, PointF viewOffset, PointF gridOffset)
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

    // ===== IconSmooth (сглаживание иконок по соседям) =====
    // См. старый SS14 SharedIconSmoothSystem/IconSmoothSystem (режим Corners).
    // Каждый такой объект (например стол) склеивается с такими же соседями: клетка рисуется
    // как 4 наложенных угловых слоя (NE/NW/SW/SE), каждый берёт стейт "{base}{fill}" (fill 0–7),
    // где fill складывается из занятости 3 прилегающих к углу тайлов (1=кардинал против часовой,
    // 2=диагональ, 4=кардинал по часовой). directions:4 в meta.json = 4 кадра каждого state_N.png
    // (по одному на угол), выбор кадра для каждого слоя делает GetSourceRect по углу слоя.

    /// <summary>Порядок угловых слоёв в RSI, соответствующий DirectionOffset None/CCW/Flip/CW
    /// (как LayerSetDirOffset в игре). Индекс в массиве — номер слоя: 0=SE,1=NE,2=NW,3=SW.</summary>
    private static readonly string[] IconSmoothLayerNames = { "SE", "NE", "NW", "SW" };

    /// <summary>Базовый угол каждого углового слоя (в радианах), который переводит «нативного»
    /// в RSI-направление кадра (SE→0, NE→π/2, NW→π, SW→−π/2). По сути DirectionOffset из игры.</summary>
    private static readonly double[] IconSmoothLayerBaseAngle =
    {
        0.0,          // SE: None
        Math.PI / 2,  // NE: CounterClockwise (+90°)
        Math.PI,      // NW: Flip (180°)
        -Math.PI / 2  // SW: Clockwise (−90°)
    };

    /// <summary>
    /// Превращает список сущностей грида в словарь "тайл → смус-соседи", где для каждого такого
    /// тайла собраны прототипы стоящих на нём сглаживаемых сущностей с их разрешёнными настройками.
    /// Множество ключей — все smooth-ключи и additionalKeys карты, чтобы быстро проверять совпадение.
    /// </summary>
    // Единый индекс сглаживания: "тайл (x,y) → множество smooth-ключей, представленных на этом тайле".
    // Источники ключей — И тайлы-стены (tileGrid TileContent.Wall), И сущности (MapEntity). Благодаря
    // этому стены, созданные генератором комнат (тайлы), сглаживаются со стенами, поставленными
    // через панель репозитория (сущности), и наоборот.
    private Dictionary<(int x, int y), HashSet<string>> _smoothKeysByTile = new();

    // Кэш: protoId -> список smooth-ключей (Key + AdditionalKeys), чтобы не пересчитывать GetIconSmoothInfo
    // для каждого прототипа на каждый кадр.
    private readonly Dictionary<string, HashSet<string>?> _smoothProtoKeyCache = new();

    /// <summary>Возвращает список смус-ключей прототипа (Key + AdditionalKeys), либо null если смуса нет.</summary>
    private HashSet<string>? GetSmoothKeysForProto(string? protoId)
    {
        if (string.IsNullOrEmpty(protoId) || _indexer == null) return null;
        lock (_smoothProtoKeyCache)
        {
            if (_smoothProtoKeyCache.TryGetValue(protoId, out var cached))
                return cached;
        }
        var settings = _indexer.GetIconSmoothInfo(protoId);
        HashSet<string>? result = null;
        if (settings != null && settings.Enabled)
        {
            result = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(settings.Key)) result.Add(settings.Key);
            foreach (var k in settings.AdditionalKeys)
                if (!string.IsNullOrEmpty(k)) result.Add(k);
            if (result.Count == 0) result = null;
        }
        lock (_smoothProtoKeyCache)
        {
            _smoothProtoKeyCache[protoId] = result;
        }
        return result;
    }

    /// <summary>
    /// Построение единого индекса сглаживания: для каждого тайла собираем множество smooth-ключей,
    /// которые на нём представлены — из тайлов-стен (tileGrid) и из сущностей (grid). 
    /// </summary>
    private void BuildIconSmoothIndex(Grid grid, TileGrid tileGrid, RectangleF visibleRect)
    {
        _smoothKeysByTile = new Dictionary<(int, int), HashSet<string>>();

        if (_indexer == null) return;

        void AddKey(int x, int y, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_smoothKeysByTile.TryGetValue((x, y), out var set))
                set.Add(key);
            else
                _smoothKeysByTile[(x, y)] = new HashSet<string>(StringComparer.Ordinal) { key };
        }

        // 1) Тайлы-стены (генератор комнат): ключ из прототипа стены на тайле.
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            if (!IsTileVisible(tile.X, tile.Y, visibleRect)) continue;
            var keys = GetSmoothKeysForProto(tile.ProtoId);
            if (keys == null) continue;
            foreach (var k in keys) AddKey(tile.X, tile.Y, k);
        }

        // 2) Сущности (панель репозитория).
        foreach (var entity in grid.Entities.OfType<Models.MapEntity>())
        {
            if (string.IsNullOrEmpty(entity.Proto)) continue;
            if (!IsPointVisible(entity.X, entity.Y, visibleRect)) continue;
            var keys = GetSmoothKeysForProto(entity.Proto);
            if (keys == null) continue;
            int tx = (int)Math.Floor(entity.X);
            int ty = (int)Math.Floor(entity.Y);
            foreach (var k in keys) AddKey(tx, ty, k);
        }
    }

    // Смещения 8 соседей в тайловых координатах: North,NorthEast,East,SouthEast,South,SouthWest,West,NorthWest
    // (screen-мир, North = −Y, East = +X — как в Renderer/GetTilePosition).
    private static readonly (int dx, int dy)[] SmoothNeighborOffsets =
    {
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    /// <summary>
    /// Совпадёт ли сосед на указанном тайле с нашим smooth-компонентом: у соседа должен быть
    /// IconSmooth с enabled, и его smooth-ключ присутствует в любом из наших смус-ключей
    /// (наш Key ИЛИ наш AdditionalKeys). Это ровно MatchingEntity из игры (older IconSmoothSystem).
    /// Источники соседа — единый индекс (_smoothKeysByTile), куда входят и тайлы-стены, и сущности.
    /// </summary>
    private static bool KeysOverlap(HashSet<string> oursKeys, HashSet<string> otherKeys)
    {
        foreach (var k in otherKeys)
            if (oursKeys.Contains(k))
                return true;
        return false;
    }

    // Массив из 8 результатов MatchingEntity для 8 соседей (индекс = SmoothNeighborOffsets).
    private bool[] SmoothMatchNeighbors(IconSmoothSettings settings, int tx, int ty)
    {
        bool[] m = new bool[8];
        HashSet<string> ours = new(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(settings.Key)) ours.Add(settings.Key);
        foreach (var k in settings.AdditionalKeys) ours.Add(k);
        if (ours.Count == 0) return m;

        for (int i = 0; i < 8; i++)
        {
            var (dx, dy) = SmoothNeighborOffsets[i];
            if (_smoothKeysByTile.TryGetValue((tx + dx, ty + dy), out var otherKeys))
            {
                if (KeysOverlap(ours, otherKeys)) m[i] = true;
            }
        }
        return m;
    }

    /// <summary>
    /// Считает CornerFill (0–7) для углов (корнер индексы: 0=SE,1=NE,2=NW,3=SW) по занятости
    /// 8 соседей — точно как CalculateCornerFill в игре (стартовый проход без поворота).
    /// neighbors[i] — результат MatchingEntity для i-го смещения из SmoothNeighborOffsets.
    /// Возвращает массив fills[4] = [fillNE, fillNW, fillSW, fillSE] (в именах функции игры).
    /// </summary>
    private int[] ComputeCornerFills(bool[] m)
    {
        bool n = m[0], ne = m[1], e = m[2], se = m[3],
             s = m[4], sw = m[5], w = m[6], nw = m[7];

        int cornerNE = 0, cornerNW = 0, cornerSW = 0, cornerSE = 0;
        if (n)  { cornerNE |= 1; cornerNW |= 4; }
        if (ne) { cornerNE |= 2; }
        if (e)  { cornerNE |= 4; cornerSE |= 1; }
        if (se) { cornerSE |= 2; }
        if (s)  { cornerSE |= 4; cornerSW |= 1; }
        if (sw) { cornerSW |= 2; }
        if (w)  { cornerSW |= 4; cornerNW |= 1; }
        if (nw) { cornerNW |= 2; }
        return new[] { cornerNE, cornerNW, cornerSW, cornerSE }; // [NE,NW,SW,SE]
    }

    /// <summary>
    /// По направлению объекта возвращает, какое значение CornerFill (0–7) из массива fills
    /// [NE,NW,SW,SE] должен получить КАЖДЫЙ из 4 угловых ЛОКАЛЬНЫХ слоёв (0=SE,1=NE,2=NW,3=SW).
    /// Точное соответствие возврату CalculateCornerFill + LayerSetRsiState в игре (слой SE всегда
    /// получает cornerSE и т.д., поэтому даже у южной ориентации нужна перестановка из массива).
    /// quarter: 0=South,1=East,2=North,3=West (см. GetQuarter).
    /// </summary>
    private int[] RotateLayerFills(int[] fills, int quarter)
    {
        int NE = fills[0], NW = fills[1], SW = fills[2], SE = fills[3];
        switch (quarter)
        {
            case 1: // East: локальные NE,NW,SW,SE = мировые SE,NE,NW,SW
                return new[] { NE, NW, SW, SE };
            case 2: // North: локальные = SW,SE,NE,NW
                return new[] { NW, SW, SE, NE };
            case 3: // West: локальные = SE,NE,NW,SW
                return new[] { SW, SE, NE, NW };
            default: // South: локальные = NE,NW,SW,SE
                return new[] { SE, NE, NW, SW };
        }
    }

    /// <summary>Квартализация угла (радианы) в 0..3 как GetCardinalDir: 0=S,1=E,2=N,3=W.</summary>
    private static int GetQuarter(float rotation)
    {
        float norm = rotation % (float)(2 * Math.PI);
        if (norm < 0) norm += (float)(2 * Math.PI);
        return (int)Math.Round(norm / (Math.PI / 2)) % 4;
    }

    /// <summary>
    /// Загружает и кэширует один state_{base}{n}.png прототипа сглаживания. key = "прото|state".
    /// Заполняет _protoTextureDirCache/_protoStateNameCache для повторного использования GetSourceRect.
    /// </summary>
    private Image? GetOrLoadIconSmoothState(string protoId, IconSmoothSettings settings, int state)
    {
        if (settings.Base == null) return null;
        string cacheKey = $"{protoId}||{settings.Base}{state}";
        if (_iconSmoothStateCache.TryGetValue(cacheKey, out var cached))
            return cached;

        Image? img = null;
        if (_indexer != null)
        {
            string? dir = _indexer.GetRsiDirectory(protoId);
            string? path = _indexer.GetIconSmoothStatePath(dir, settings.Base, state);
            if (path != null && File.Exists(path))
            {
                try
                {
                    img = Image.FromFile(path);
                    string stateName = $"{settings.Base}{state}";
                    _protoTextureDirCache[cacheKey] = Path.GetDirectoryName(path) ?? "";
                    _protoStateNameCache[cacheKey] = stateName;
                }
                catch { img = null; }
            }
        }
        _iconSmoothStateCache[cacheKey] = img!;
        return img;
    }

    /// <summary>
    /// Рисует сглаживаемую сущность: грузит 4 угловых state-кадра (state_{base}{fill}) и накладывает
    /// их на клетку rect, каким-либо углом слоя беря нужный из 4 направленных кадров внутри PNG.
    /// </summary>
    private void DrawSingleSmoothEntity(Graphics g, string protoId, float worldX, float worldY, float rotation, int tileSize, PointF viewOffset, PointF gridOffset, IconSmoothSettings settings)
    {
        if (_indexer == null) return;
        int tx = (int)Math.Floor(worldX);
        int ty = (int)Math.Floor(worldY);

        bool[] neigh = SmoothMatchNeighbors(settings, tx, ty);
        int[] fills = ComputeCornerFills(neigh);
        int quarter = GetQuarter(rotation);
        int[] layerFills = RotateLayerFills(fills, quarter); // [SE,NE,NW,SW]

        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // Сначала пробуем предзагрузить все 4 стейта. Если ни один не загрузился
        // (нет RSI-папки/кадров у прототипа) — НЕ рисуем пустую клетку, а даём
        // вызывающему коду уйти в обычный DrawTexturedRect.
        bool anyLoaded = false;
        Image?[] imgs = new Image?[4];
        string[] cacheKeys = new string[4];
        for (int li = 0; li < 4; li++)
        {
            int state = layerFills[li];
            imgs[li] = GetOrLoadIconSmoothState(protoId, settings, state);
            cacheKeys[li] = $"{protoId}||{settings.Base}{state}";
            if (imgs[li] != null) anyLoaded = true;
        }
        // Если ни один стейт-кадр не загрузился — выходим, вызывающий код уйдёт в обычный спрайт.
        if (!anyLoaded) return;
        for (int li = 0; li < 4; li++)
        {
            Image? stateImg = imgs[li];
            if (stateImg == null) continue;

            // Угол слоя: собственный поворот объекта + базовый угол слоя (какой угол нужно передать
            // в GetSourceRect, чтобы он выбрал кадр этого угла). 
            float layerAngle = rotation + (float)IconSmoothLayerBaseAngle[li];
            Rectangle src = GetSourceRect(cacheKeys[li], stateImg, layerAngle);
            if (src.Width == 0 || src.Height == 0) continue;
            g.DrawImage(stateImg, rect, src, GraphicsUnit.Pixel);
        }
    }

    private void DrawSingleEntity(Graphics g, string protoId, float worldX, float worldY, float rotation, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // IconSmooth: если у прототипа (или его предков) есть компонент IconSmooth —
        // рисуем сглаженным способом (4 угловых слоя), а не обычной текстурой.
        if (_indexer != null && _indexer.GetIconSmoothInfo(protoId) is { } smoothInfo)
        {
            if (smoothInfo.Enabled && smoothInfo.Base != null)
            {
                DrawSingleSmoothEntity(g, protoId, worldX, worldY, rotation, tileSize, viewOffset, gridOffset, smoothInfo);
                return;
            }
        }

        if (_indexer != null)
        {
            var proto = _indexer.FindPrototype(protoId);
            if (proto?.IsStructure == true && _indexer.GetStateDirections(protoId) < 4)
                rotation = 0f;
        }

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

    /// <summary>
    /// Отрисовка голопада как отдельной «привязки»: маркер с иконкой 📟 и
    /// подписью прототипа, чтобы привязка была видна как самостоятельный объект.
    /// </summary>
    private void DrawSingleHolopad(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 255, 170, 0),
            fallbackPen: Color.FromArgb(255, 180, 90, 0),
            protoLabel: "holopad");
    }

    private void DrawSingleLocker(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 40, 180, 60),
            fallbackPen: Color.FromArgb(180, 30, 140, 50),
            protoLabel: "locker");
    }

    private void DrawSingleSuitStorage(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 40, 120, 200),
            fallbackPen: Color.FromArgb(180, 40, 110, 200),
            protoLabel: "suit");
    }

    private void DrawSingleBed(Graphics g, string protoId, string bedsheetProto, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // Рисуем сперва кровать, а поверх неё одеяло (если задано) в том же прямоугольнике.
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 120, 90, 200),
            fallbackPen: Color.FromArgb(180, 90, 60, 200),
            protoLabel: "bed");

        if (!string.IsNullOrEmpty(bedsheetProto))
        {
            var sheetTexture = GetOrLoadTexture(bedsheetProto);
            if (sheetTexture != null)
            {
                DrawTexturedRect(g, bedsheetProto, rect, null, null);
            }
        }
    }

    /// <summary>
    /// Отрисовка камеры как отдельной «привязки»: обычный маркер на всю клетку,
    /// спрайт в повороте направления (кадр направления RSI выбирает сам
    /// DrawTexturedRect по rotation).
    /// </summary>
    private void DrawSingleCamera(Graphics g, string protoId, float rotation, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 90, 90, 200),
            fallbackPen: Color.FromArgb(180, 60, 60, 160),
            protoLabel: "camera",
            rotation: rotation);
    }

    /// <summary>
    /// Отрисовка комода как «привязки» — обычный маркер авто-объекта во всю клетку
    /// с текстурой прототипа.
    /// </summary>
    private void DrawSingleDresser(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 170, 120, 60),
            fallbackPen: Color.FromArgb(180, 120, 80, 40),
            protoLabel: "dresser");
    }

    /// <summary>
    /// Отрисовка интеркома как «привязки». Интерком стоит В тайле-стены и развёрнут
    /// гранью в комнату, поэтому спрайт рисуется с его поворотом — кадр направления
    /// у интеркома выбирает DrawTexturedRect (у спрайта directions: 4).
    /// </summary>
    private void DrawSingleIntercom(Graphics g, string protoId, float rotation, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 120, 140, 170),
            fallbackPen: Color.FromArgb(180, 90, 100, 120),
            protoLabel: "intercom",
            rotation: rotation);
    }

    /// <summary>
    /// Отрисовка факса как «привязки» — обычный маркер авто-объекта во всю клетку
    /// с текстурой прототипа (адрес факса в подписи не помещается, он виден в лупе).
    /// </summary>
    private void DrawSingleFax(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        DrawAutoObject(g, protoId, worldX, worldY, tileSize, viewOffset, gridOffset,
            fallbackBrush: Color.FromArgb(40, 200, 200, 200),
            fallbackPen: Color.FromArgb(180, 130, 130, 130),
            protoLabel: "fax");
    }

    /// <summary>
    /// Общая отрисовка «привязки» (авто-объекта из свойств комнаты): почти прозрачный
    /// оранжевый прямоугольник-маркер + рамка, поверх — текстура прототипа (если есть),
    /// а при достаточном зуме — подпись "auto" справа сверху и короткое имя прототипа снизу.
    /// Голопад рисуется тем же методом, что и остальные привязки (шкафчик/скафандр/кровать).
    /// rotation — поворот сущности (у камеры выбирает кадр направления спрайта).
    /// </summary>
    private void DrawAutoObject(Graphics g, string protoId, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset,
        Color fallbackBrush, Color fallbackPen, string protoLabel, float rotation = 0f)
    {
        var cellRect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // Почти прозрачный жёлто-оранжевый фон — привязка / авто-объект (единый для всех)
        using (var fillBrush = new SolidBrush(Color.FromArgb(40, 255, 170, 0)))
            g.FillRectangle(fillBrush, cellRect);
        using (var pen = new Pen(Color.FromArgb(255, 180, 90, 0), 1.5f))
            g.DrawRectangle(pen, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

        var texture = !string.IsNullOrEmpty(protoId) ? GetOrLoadTexture(protoId) : null;
        if (texture != null)
        {
            // Текстура — во всю клетку (как в игре); у направленных спрайтов
            // DrawTexturedRect сам выберет кадр нужной стороны по rotation.
            DrawTexturedRect(g, protoId, cellRect, null, null, rotation);
        }
        else
        {
            using var fill = new SolidBrush(fallbackBrush);
            g.FillRectangle(fill, cellRect);
            using var p = new Pen(fallbackPen, 1f);
            g.DrawRectangle(p, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);
        }

        if (tileSize > 14)
        {
            // Подпись "auto" вплотную к правому верхнему углу — маркер авто-объекта
            using var autoFont = new Font("Segoe UI", 7, FontStyle.Bold);
            using var autoBrush = new SolidBrush(Color.Black);
            var autoSize = g.MeasureString("auto", autoFont);
            g.DrawString("auto", autoFont, autoBrush, cellRect.Right - autoSize.Width, cellRect.Y);

            // Подпись прототипа снизу
            using var smallFont = new Font("Segoe UI", 6);
            using var labelBrush = new SolidBrush(Color.FromArgb(220, 90, 50, 0));
            string label = string.IsNullOrEmpty(protoId) ? protoLabel : ShortenProtoLabel(protoId);
            g.DrawString(label, smallFont, labelBrush, cellRect.X + 1, cellRect.Bottom - 10);
        }
    }

    /// <summary>Короткое имя прототипа для подписи снизу у привязок.</summary>
    private static string ShortenProtoLabel(string protoId) =>
        protoId.Replace("SurveillanceCamera", "cam ").Replace("Dresser", "drs ").Replace("Intercom", "ic ").Replace("FaxMachine", "fax ").Replace("Holopad", "").Replace("Bedsheet", "").Replace("SuitStorage", "")
               .Replace("General", "$").Replace("Command", "C").Replace("Science", "S")
               .Replace("Medical", "M").Replace("Engineering", "E").Replace("Security", "Sec")
               .Replace("Service", "Sv").Replace("Cargo", "Cg").Replace("Long", "LR").Replace("Range", "");

    private void DrawSingleAlarm(Graphics g, MapEntity entity, int tileSize, PointF viewOffset, PointF gridOffset, string protoId, Color bgColor)
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

                    // Имя состояния берём из прототипа, а не из имени файла — иначе для
                    // RSI-спрайтов (где файл называется "door.png", а состояния в
                    // meta.json — "closed"/"open"/...) rotation не работает
                    string? stateFromProto = _indexer?.FindPrototype(protoId)?.State;
                    _protoStateNameCache[protoId] = !string.IsNullOrEmpty(stateFromProto)
                        ? stateFromProto
                        : Path.GetFileNameWithoutExtension(texturePath);
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

        if (stateMap.TryGetValue(state, out var found))
            return found;

        // Если указанное состояние не найдено, ищем любое с directions > 1 —
        // иначе RSI-спрайты без точного совпадения state теряют rotation
        foreach (var kv in stateMap)
        {
            if (kv.Value.directions > 1)
                return kv.Value;
        }

        return (1, 1);
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

    /// <summary>
    /// Возвращает смещение в долях тайла для визуального позиционирования трубы
    /// в зависимости от направления (rotation). Конвенция: 0=юг, PI/2=восток,
    /// PI=север, 3PI/2=запад.
    /// </summary>
    /// <summary>
    /// Смещение узла трубы от центра тайла в долях тайла, в зависимости ТОЛЬКО от
    /// типа трубы. У PipeEntity нет реального поворота (Rotation нигде не
    /// проставляется при создании, всегда 0) — раньше сдвиг ошибочно считался
    /// через несуществующий поворот. Теперь: Distra всегда снизу-слева, Waste
    /// всегда сверху-справа, Normal и Util — без сдвига, строго по центру.
    /// </summary>
    public static (float offsetX, float offsetY) GetPipeTypeOffset(string pipeType)
    {
        const float q = 0.25f; // четверть тайла

        return pipeType switch
        {
            "Distra" => (-q, q), // снизу-слева
            "Waste" => (q, -q), // сверху-справа
            _ => (0, 0)  // Normal, Util — по центру
        };
    }

    /// <summary>
    /// "Базовая труба" — единая точка расчёта экранного центра узла трубы для всех
    /// топологических мест отрисовки (линии, точки, маркеры конца/фильтра, стрелки
    /// потока).
    /// </summary>
    private static (float cx, float cy) GetPipeNodeScreenCenter(PipeEntity pipe, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var (offX, offY) = GetPipeTypeOffset(pipe.PipeType);

        float cx = (pipe.X + 0.5f + offX + gridOffset.X) * tileSize - viewOffset.X;
        float cy = (pipe.Y + 0.5f + offY + gridOffset.Y) * tileSize - viewOffset.Y;
        return (cx, cy);
    }

    private Color GetPipeColor(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => Color.FromArgb(180, 100, 200, 255),
            "Waste" => Color.FromArgb(180, 255, 150, 150),
            "Normal" => Color.FromArgb(180, 200, 200, 200),
            "Util" => Color.FromArgb(180, 100, 150, 50),
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
            "Util" => Color.FromArgb(200, 100, 150, 50),
            _ => Color.FromArgb(200, 150, 150, 150)
        };
    }

    #endregion

    #region Остальные методы

    private void DrawGrid(Graphics g, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int alpha = (int)(GridColor.A * opacity);
        if (alpha < 0) alpha = 0;
        if (alpha > 255) alpha = 255;
        using var pen = new Pen(Color.FromArgb(alpha, GridColor.R, GridColor.G, GridColor.B), 1);

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
        string pipeMode = ShowPipeOverlay ? "" : " [ЛИНИИ ТРУБ СКРЫТЫ, КОНЦЫ ВИДНЫ]";
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
        if (!_showEntityPreview) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        var gridOffset = new PointF(_currentMap.ActiveGrid.Position.X, _currentMap.ActiveGrid.Position.Y + layerOffsetY);

        var rect = ToRect(_previewEntityX, _previewEntityY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // Рамка целевого тайла: сразу видно, в какую клетку встанет объект. Красная
        // рамка — ставить нельзя (нет комнаты/прототипа для этой комнаты или клетка
        // уже занята), тогда спрайт не рисуется вовсе.
        using (var fillBrush = new SolidBrush(_previewEntityValid
                   ? Color.FromArgb(45, 60, 230, 90)
                   : Color.FromArgb(80, 220, 60, 60)))
            g.FillRectangle(fillBrush, rect);
        using (var framePen = new Pen(_previewEntityValid
                   ? Color.FromArgb(220, 40, 170, 60)
                   : Color.FromArgb(230, 200, 40, 40), 2f))
            g.DrawRectangle(framePen, rect.X, rect.Y, rect.Width, rect.Height);

        if (!_previewEntityValid || string.IsNullOrEmpty(_previewEntityProto)) return;

        // Структуры (BaseStructure) всегда смотрят на юг, независимо от rotation,
        // НО если у иконки в meta.json directions: 4 —尊重 rotation (направленная иконка)
        float previewRotation = _previewEntityRotation;
        if (_indexer != null)
        {
            var proto = _indexer.FindPrototype(_previewEntityProto);
            if (proto?.IsStructure == true && _indexer.GetStateDirections(_previewEntityProto) < 4)
                previewRotation = 0f;
        }

        ImageAttributes? tint = !string.IsNullOrEmpty(_previewDecalColor)
        ? GetDecalTintAttributes(_previewDecalColor)
        : null;

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


    private void DrawExpandRoomPreview(Graphics g, float scale, PointF viewOffset)
    {
        if (!_showExpandPreview || _expandPreviewRoom == null) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        var gridOffset = new PointF(_currentMap.ActiveGrid.Position.X, _currentMap.ActiveGrid.Position.Y + layerOffsetY);

        using var freeBrush = new SolidBrush(Color.FromArgb(150, 40, 220, 90));
        using var freePen = new Pen(Color.DarkGreen, 2);
        using var blockedBrush = new SolidBrush(Color.FromArgb(150, 220, 40, 40));
        using var blockedPen = new Pen(Color.DarkRed, 2);

        // Проверка занятости идёт циклически по каждой клетке края ВШИРЬ (вправо
        // и влево относительно направления расширения), пересчитываясь заново на
        // каждом шаге — см. ExpandRunScanCells. Свободные/общая стена — зелёный,
        // внутренний пол чужой комнаты — красный. Это единый алгоритм с фактическим
        // применением (ExpandRoomBorder), поэтому превью и результат не расходятся.
        var scanCells = RoomSubtractor.ExpandRunScanCells(
            _currentMap.ActiveGrid, _expandPreviewRoom,
            _expandPreviewCellX, _expandPreviewCellY, _expandPreviewDx, _expandPreviewDy);

        foreach (var (cx, cy, blocked) in scanCells)
        {
            var rect = ToRect(cx, cy, tileSize, viewOffset, gridOffset);
            g.FillRectangle(blocked ? blockedBrush : freeBrush, rect);
            g.DrawRectangle(blocked ? blockedPen : freePen, rect);
        }
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

        foreach (var entity in sortedEntities)
        {
            var protoId = entity.Proto ?? "";

            var rect = ToRect(entity.X, entity.Y, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

            // Структуры (BaseStructure) всегда смотрят на юг, независимо от rotation,
            // НО если у иконки в meta.json directions: 4 —尊重 rotation (направленная иконка)
            float rotation = entity.Rotation;
            if (_indexer != null)
            {
                var proto = _indexer.FindPrototype(protoId);
                if (proto?.IsStructure == true && _indexer.GetStateDirections(protoId) < 4)
                    rotation = 0f;
            }

            DrawTexturedRect(g, protoId, rect, null, (gg, r) =>
            {
                bool isWindow = protoId.Contains("Window", StringComparison.OrdinalIgnoreCase);
                using var brush = new SolidBrush(isWindow
                    ? Color.FromArgb(180, 0, 120, 215)   // синий — окно
                    : Color.FromArgb(180, 255, 0, 255)); // розовый — остальное
                gg.FillRectangle(brush, r);
                using var pen = new Pen(Color.Black, 1);
                gg.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

                if (tileSize > 16)
                {
                    using var font = new Font("Segoe UI", 6);
                    using var textBrush = isWindow
                        ? new SolidBrush(Color.FromArgb(200, 200, 230, 255))
                        : new SolidBrush(Color.White);
                    string label = isWindow ? "🪟" : (protoId.Length > 8 ? protoId.Substring(0, 8) : protoId);
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

    // Визуализация растягиваемой области инструмента "Удалить область" (DeleteArea).
    // Рамка задаётся в тайловых координатах текущего грида инклюзивно (начальная и
    // конечная клетки внутри области), поэтому к размеру добавляется 1 тайл.
    private void DrawDeleteAreaOverlay(Graphics g, float scale, PointF viewOffset)
    {
        if (_deleteAreaRect == null) return;
        if (_currentMap?.ActiveGrid == null) return;

        var (ax, ay, aw, ah) = _deleteAreaRect.Value;
        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _currentMap.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_currentMap.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        float left = ax * tileSize + gridOffsetX - viewOffset.X;
        float top = ay * tileSize + gridOffsetY - viewOffset.Y;
        float width = aw * tileSize;
        float height = ah * tileSize;

        using var fillBrush = new SolidBrush(Color.FromArgb(50, 230, 70, 70));
        g.FillRectangle(fillBrush, left, top, width, height);

        using var pen = new Pen(Color.FromArgb(255, 230, 60, 60), 2f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, left, top, width, height);
    }


    private void DrawDecalAreaDrawOverlay(Graphics g, float scale, PointF viewOffset)
    {
        if (_decalAreaDrawRect == null) return;
        if (_currentMap?.ActiveGrid == null) return;

        var (ax, ay, aw, ah) = _decalAreaDrawRect.Value;
        int tileSize = (int)(Constants.TILE_SIZE * scale);
        int activeIndex = _currentMap.Grids.IndexOf(_currentMap.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _currentMap.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_currentMap.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        float left = ax * tileSize + gridOffsetX - viewOffset.X;
        float top = ay * tileSize + gridOffsetY - viewOffset.Y;
        float width = aw * tileSize;
        float height = ah * tileSize;

        using var fillBrush = new SolidBrush(Color.FromArgb(60, 120, 200, 255));
        g.FillRectangle(fillBrush, left, top, width, height);

        using var pen = new Pen(Color.FromArgb(255, 80, 160, 255), 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, left, top, width, height);
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

        // Вся область заливается жёлтым, но вычитаемые зоны "вырезаются" из неё как
        // настоящие отверстия: там прозрачно, и сквозь них видно карту. Сами вычеты
        // помечаются только тонкой пунктирной рамкой (в отличие от прежней красной
        // заливки — теперь вырез выглядит как вычтенное место, а не как отдельная зона).
        using var fillBrush = new SolidBrush(Color.FromArgb(50, 255, 200, 0));

        using (var region = new Region(new RectangleF(left, top, width, height)))
        {
            if (_decalAreaEditSubtractions != null)
            {
                foreach (var sub in _decalAreaEditSubtractions)
                {
                    float sl = sub.X * tileSize + gridOffsetX - viewOffset.X;
                    float st = sub.Y * tileSize + gridOffsetY - viewOffset.Y;
                    region.Exclude(new RectangleF(sl, st, sub.Width * tileSize, sub.Height * tileSize));
                }
            }
            g.FillRegion(fillBrush, region);
        }

        using var pen = new Pen(Color.FromArgb(255, 255, 160, 0), 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, left, top, width, height);

        // Тонкая рамка вокруг вычитаемых зон — чтобы было видно, откуда что вычтено
        if (_decalAreaEditSubtractions != null)
        {
            using var subPen = new Pen(Color.FromArgb(200, 120, 120, 120), 1)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dot
            };
            foreach (var sub in _decalAreaEditSubtractions)
            {
                float sl = sub.X * tileSize + gridOffsetX - viewOffset.X;
                float st = sub.Y * tileSize + gridOffsetY - viewOffset.Y;
                g.DrawRectangle(subPen, sl, st, sub.Width * tileSize, sub.Height * tileSize);
            }
        }

        // Потайлёво вырезанные клетки декалей (кнопка «🗑 Удалить») — заливаются красным,
        // чтобы было видно, в каких тайлах декаль этой области не появляется. Заливаем весь тайл.
        if (_decalAreaEditCutCells != null && _decalAreaEditCutCells.Count > 0)
        {
            using var cutBrush = new SolidBrush(Color.FromArgb(160, 220, 40, 40));
            foreach (var cell in _decalAreaEditCutCells)
            {
                float cl = cell.X * tileSize + gridOffsetX - viewOffset.X;
                float ct = cell.Y * tileSize + gridOffsetY - viewOffset.Y;
                g.FillRectangle(cutBrush, cl, ct, tileSize, tileSize);
            }
        }

        // Потайлёво удалённые из формы области клетки («➖ Вычесть») — закрашиваются тёмным,
        // чтобы было видно, где форма области реально вырезана. Заливаем весь тайл.
        if (_decalAreaEditRemovedCells != null && _decalAreaEditRemovedCells.Count > 0)
        {
            using var remBrush = new SolidBrush(Color.FromArgb(190, 60, 60, 80));
            foreach (var cell in _decalAreaEditRemovedCells)
            {
                float cl = cell.X * tileSize + gridOffsetX - viewOffset.X;
                float ct = cell.Y * tileSize + gridOffsetY - viewOffset.Y;
                g.FillRectangle(remBrush, cl, ct, tileSize, tileSize);
            }
        }



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

        // Растягиваемый мышью прямоугольник вырезки декалей (режим «✖»)
        if (_decalAreaCutDragRect != null)
        {
            var (cx2, cy2, cw2, ch2) = _decalAreaCutDragRect.Value;
            float dl = cx2 * tileSize + gridOffsetX - viewOffset.X;
            float dt = cy2 * tileSize + gridOffsetY - viewOffset.Y;
            float dw = cw2 * tileSize;
            float dh = ch2 * tileSize;

            using var dfill = new SolidBrush(Color.FromArgb(70, 220, 40, 40));
            g.FillRectangle(dfill, dl, dt, dw, dh);
            using var dpen = new Pen(Color.FromArgb(255, 220, 40, 40), 2)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };
            g.DrawRectangle(dpen, dl, dt, dw, dh);
        }

        // Растягиваемый мышью прямоугольник вырезки КЛЕТОК ФОРМЫ области (режим «➖ Вычесть»)
        if (_decalAreaSubtractDragRect != null)
        {
            var (ex, ey, ew, eh) = _decalAreaSubtractDragRect.Value;
            float el = ex * tileSize + gridOffsetX - viewOffset.X;
            float et = ey * tileSize + gridOffsetY - viewOffset.Y;
            float ew2 = ew * tileSize;
            float eh2 = eh * tileSize;

            using var efill = new SolidBrush(Color.FromArgb(70, 40, 40, 40));
            g.FillRectangle(efill, el, et, ew2, eh2);
            using var epen = new Pen(Color.FromArgb(255, 60, 60, 60), 2)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };
            g.DrawRectangle(epen, el, et, ew2, eh2);
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
    /// <summary>
    /// Превью размещаемого объекта под курсором: спрайт прототипа с поворотом и
    /// рамка целевого тайла (зелёная — ставить можно, красная — нельзя).
    /// valid = false используется для привязок, у которых в этой клетке нет
    /// подходящего прототипа (например комод вне комнаты главы).
    /// </summary>
    public void SetEntityPreview(float x, float y, float rotation, string proto, string? decalColor = null, bool valid = true)
    {
        _previewEntityX = x;
        _previewEntityY = y;
        _previewEntityRotation = rotation;
        _previewEntityProto = proto;
        _previewDecalColor = decalColor;
        _previewEntityValid = valid;
        _showEntityPreview = true;
    }

    public void ClearEntityPreview()
    {
        _showEntityPreview = false;
    }


    public void SetExpandRoomPreview(Room room, int cellX, int cellY, int dx, int dy)
    {
        _expandPreviewRoom = room;
        _expandPreviewCellX = cellX;
        _expandPreviewCellY = cellY;
        _expandPreviewDx = dx;
        _expandPreviewDy = dy;
        _showExpandPreview = true;
    }

    public void ClearExpandRoomPreview()
    {
        _showExpandPreview = false;
        _expandPreviewRoom = null;
    }

    #endregion
}