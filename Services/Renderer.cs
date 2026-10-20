// Services/Renderer.cs
using MapperIce.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapperIce.Services;

public partial class Renderer
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

    /// <summary>Показывать области, выделенные инструментом «Разделитель гридов».</summary>
    public bool ShowDividerAreas { get; set; } = true;

    // Кэш текстур для ускорения рендеринга
    // Кэш текстур для ускорения рендеринга
    private readonly Dictionary<string, Image?> _textureCache = new();
    // Ключ — (идентификатор спрайта, четверть оборота): строка собиралась на каждый
    // вызов, а вызывается это на каждый объект каждый кадр. Для кадров без направлений
    // четверть всегда 0, т.е. ключ остаётся по одному на спрайт.
    private readonly Dictionary<(string key, int quarter), Rectangle> _sourceRectCache = new();
    private readonly Dictionary<string, string> _protoTextureDirCache = new();
    private readonly Dictionary<string, Size> _rsiFrameSizeCache = new();
    private readonly Dictionary<string, string> _protoStateNameCache = new();
    private readonly Dictionary<string, Dictionary<string, (int directions, int framesPerDirection)>> _rsiStateDirectionsCache = new();
    private readonly HashSet<int> _dirtyTileGrids = new();
    // Гриды, чей рендер-кэш (индекс сглаживания и чанки пола) устарел и должен быть
    // пересобран при следующем Render. Отличается от _dirtyTileGrids: тот очищается в
    // Render после пересборки TileGrid (стр. ~284) — ДО вызова DrawRenderLayer, поэтому
    // на его основе кэши сглаживания/пола не могли пересобираться на структурное изменение.
    private readonly HashSet<int> _dirtyRenderCaches = new();
    private readonly Dictionary<int, TileGrid> _tileGridCache = new();

    // === Чанковый кэш статичного слоя ПОЛА (инстанцирование для GDI+) ===
    // Пол — самый массовый слой (тысячи одинаковых тайлов). Вместо того чтобы на каждый
    // кадр делать тысячи отдельных DrawImage по тайлам, пол выпекается в маленькие
    // битмапы по чанкам: на кадр выводится по одному DrawImage на видимый чанк.
    // Пол всегда строго внизу z-порядка (ниже полки декалей), поэтому запекать его
    // отдельным слоем безопасно — с декалями/сущностями/дверями не переплетается.
    // Кэш валиден, потому что пол не меняется при панорамировании/зуме — пересобирается
    // только при структурном изменении грида (тот же флаг _dirtyTileGrids).
    private const int StaticChunkMaxTiles = 16;
    private const int StaticChunkMaxBitmaps = 512;
    // Максимальный размер чанка по пикселям: при сильном приближении (тайл 128–256px)
    // чанк 16×16 клеток раздувался до огромных битмапов (до 4096² ≈ 64 МБ) и их печение
    // с рисованием доминировало в кадре. Поэтому число клеток в чанке режем по пикселям.
    private const int StaticChunkMaxPixels = 512;

    private static int GetChunkTiles(int tileSize) => Math.Clamp(StaticChunkMaxPixels / Math.Max(1, tileSize), 1, StaticChunkMaxTiles);
    // Ключ (gridUid, tileSize, chunkX, chunkY) -> Bitmap. Размер тайла в ключе: с переменной
    // величиной чанка (GetChunkTiles) сетки разных зумов разные, и без него записи
    // под разными зумами пересекались бы.
    private readonly Dictionary<(int gridUid, int tileSize, int cx, int cy), Bitmap> _floorChunkCache = new();
    private readonly LinkedList<(int gridUid, int tileSize, int cx, int cy)> _floorChunkLru = new();
    // Чанки, в которых нет ни одного тайла пола — BakeFloorChunk возвращает для них null.
    // Храним их отдельным множеством, а не как null-значения в _floorChunkCache, чтобы
    // ClearGridFloorChunks/ClearFloorChunks могли безопасно Dispose каждый закэшированный
    // битмап (null в словаре приводил к NullReferenceException).
    private readonly HashSet<(int gridUid, int tileSize, int cx, int cy)> _emptyFloorChunks = new();

    // Кэш кадров IconSmooth-стейтов: ключ (прототип, номер стейта) -> Image.
    // Один state_N.png (например state_3.png, 64×64) содержит 4 кадра-фриза по 32×32 —
    // по одному на каждый угол (SE/NW/NE/SW). Отдельный кэш от _textureCache, потому
    // что у обычных текстур один кадр на стейт, а тут — 4, и лежат они под вменяемым
    // ключом (не конфликтуют с прототипом). Ключ-кортеж вместо строки: на каждый
    // сглаженный тайл в кадре приходится до 4 запросов, и склейка строки на каждый
    // из них была лишней аллокацией на ровном месте.
    private readonly Dictionary<(string protoId, int state), Image?> _iconSmoothStateCache = new();
    // Составной строковый ключ для тех же стейтов (нужен GetSourceRect: по нему
    // определяется каталог meta.json и имя стейта). Строится один раз на (прототип, стейт).
    private readonly Dictionary<(string protoId, int state), string> _iconSmoothStateKeys = new();

    // === Бейк спрайтов с тонированием и поворотом ===
    // GDI+ рисует DrawImage с ImageAttributes примерно в 25 раз медленнее обычного
    // (замер: ~118 мкс против ~3–5 мкс на спрайт 32×32), а DrawImage под Transform
    // ровно на 90° — примерно в 300 раз (~1 мс против ~6 мкс у 45°/180°/270°).
    // На карте это тысячи тонированных декалей/кабелей и десятки повёрнутых
    // объектов, поэтому такие спрайты один раз выпекаются в битмап нужного размера
    // (с тонированием и поворотом) и дальше рисуются обычным 1:1 DrawImage.
    private readonly record struct BakedSpriteKey(string Id, int Variant, int Quarter, int Width, int Height, string? Tint);
    private readonly Dictionary<BakedSpriteKey, Bitmap> _bakedSpriteCache = new();
    private const int BakedSpriteMaxEntries = 4096;

    // === Метаданные прототипа, нужные при отрисовке ===
    // Раньше на каждый объект каждый кадр заново обходилась цепочка parent: (слои,
    // offset, IsStructure, directions), причём GetStateDirections у индексатора
    // читал meta.json с диска. Теперь всё это резолвится один раз на protoId.
    private sealed class ProtoRenderMeta
    {
        public IconSmoothSettings? IconSmooth;
        public List<Models.SpriteLayer>? Layers;
        public (float x, float y) SpriteOffset;
        public bool IsStructure;
        public int StateDirections;
        public int DrawDepthOffset;
        public bool IsOccluder;
        public bool SnapCardinals;
        public (string? baseClosed, string? doorOpen, string? doorClosed)? StorageVisuals;
    }
    private readonly Dictionary<string, ProtoRenderMeta> _protoMetaCache = new(StringComparer.Ordinal);

    // Готовые данные отрисовки слоя многослойного прототипа: составной ключ текстур,
    // сама текстура и признак «у слоя есть directions». Заполняется один раз на
    // (прототип, номер слоя); раньше всё это пересчитывалось на каждый слой каждой
    // сущности в кадре.
    private readonly Dictionary<(string protoId, int layerIndex), (string CacheKey, Image? Texture, bool HasDirections)> _layerInfoCache = new();

    // === Композиция спрайтов в один битмап ===
    // Многослойные прототипы (у телеком-сервера 15 слоёв) и IconSmooth (4 угловых
    // слоя) раньше выводились построчно: 4–15 DrawImage на объект. На стандартной
    // карте это ~7500 вызовов GDI+ за кадр. Здесь слои один раз собираются в один
    // битмап, и на кадр остаётся один DrawImage на объект.
    private readonly Dictionary<(string protoId, int quarter, int width, int height), Bitmap?> _layeredCompositeCache = new();
    private const int LayeredCompositeMaxEntries = 1024;

    private readonly Dictionary<(string protoId, int mask, int quarter, int tileSize), Bitmap?> _smoothCompositeCache = new();
    private const int SmoothCompositeMaxEntries = 2048;

    // === Кэш оверлея обзора камер ===
    // До кэша на КАЖДЫЙ кадр заново обходились все сущности карты (проверка Occluder
    // по цепочке parent:) и для КАЖДОЙ камеры считалась видимость: две волны по ~289
    // клеткам плюс проход углов. На стандартной карте SS14 (~25 тыс. сущностей) это
    // давало ~85 мс на кадр. Теперь результат считается один раз на структурное
    // изменение грида — камеры неподвижны, меняется только кадр/зум.
    private sealed class CameraCoverageCache
    {
        public readonly List<(float X, float Y)> Cameras = new();
        // Ключ — клетка камеры; значение — видимые ею клетки (в мировых тайлах,
        // от зума не зависят).
        public readonly Dictionary<(int x, int y), List<(int x, int y)>> VisibleByCamera = new();
    }
    private readonly Dictionary<int, CameraCoverageCache> _cameraCoverageCache = new();
    private readonly HashSet<int> _dirtyCameraCoverage = new();

    // Резолвнутая визуальная информация кабеля (CableVisualizer + цвета слоёв) по protoId.
    // Резолв идёт по цепочке parent:, поэтому кэшируем, чтобы не обходить её на каждый
    // кабель каждый кадр. Сбрасывается в ClearCache вместе с остальными текстурами.
    private sealed class CableRenderInfo
    {
        public string StatePrefix { get; init; } = "";
        public string? ExtraLayerPrefix { get; init; }
        public string? Layer0Color { get; init; }
        public string? Layer1Color { get; init; }
        /// <summary>NodeGroupID кабеля (HVPower/MVPower/Apc/ExCable) либо, если он
        /// не задан, StatePrefix — ключ, по которому кабели считаются соседями.</summary>
        public string ConnectivityKey { get; init; } = "";
    }
    private readonly Dictionary<string, CableRenderInfo?> _cableRenderInfoCache = new();

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

    // Предпросмотр размещаемого объекта (привязки, узлы сети, сигнализации, прототипы)
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

    // Растягиваемая рамка инструмента "Удалить область" (в тайловых координатах, инклюзивно).
    // DividerStyle — рамка для «Разделителя гридов»: зелёная, в цвет выделения.
    private (int x, int y, int w, int h)? _deleteAreaRect = null;
    private bool _deleteAreaDividerStyle = false;

    public void SetDeleteAreaRect(int x, int y, int w, int h, bool dividerStyle = false)
    {
        _deleteAreaRect = (x, y, w, h);
        _deleteAreaDividerStyle = dividerStyle;
    }

    public void ClearDeleteAreaRect()
    {
        _deleteAreaRect = null;
        _deleteAreaDividerStyle = false;
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
                // Здесь они рисуются только при ВКЛЮЧЁННОМ оверлее — поверх всего,
                // вместе с линиями. При выключенном оверлее концы уходят под ковры
                // (см. DrawRenderLayer).
                if (ShowPipeOverlay && allPipes.Count > 0)
                    DrawEndpointMarkers(g, allPipes, gridPipes, tileSize, viewOffset, gridOffset);

                // Область обзора камер — тот же приём, что и зона покрытия НВ-кабеля
                // (см. DrawLvCoverageOverlay), только радиус больше. Рисуется и для
                // камер-«привязок» из свойств комнаты, и для камер, поставленных
                // обычным прототипом из панели репозитория.
                if (ShowCameraAreas)
                    DrawCameraCoverageOverlay(g, grid, tileGrid, tileSize, viewOffset, gridOffset, visibleRect);

                // Электросеть — отдельный слой поверх труб: провода инструмента рисуются
                // теми же спрайтами кабелей, что и сущности из репозитория (с подбором
                // стейта по соседям), а линии/точки остаются только фолбэком.
                if (ShowWires)
                {
                    // Кабели, нарисованные ИНСТРУМЕНТОМ, рисуются теми же спрайтами и с тем
                    // же соединением по соседям, что и кабели-сущности из репозитория
                    // (CableVisualizer). Линии/точки остаются только фолбэком — если для
                    // типа провода нет прототипа кабеля или не загрузился стейт спрайта.
                    var allWires = _wireBuilder.GetWires(grid)
                        .Where(w => IsPointVisible(w.X, w.Y, visibleRect))
                        .OrderBy(w => w.Y)
                        .ToList();
                    DrawLvCoverageOverlay(g, allWires, tileSize, viewOffset, gridOffset);
                    var unrenderedWires = DrawWireCableSprites(g, allWires, tileSize, viewOffset, gridOffset);
                    if (unrenderedWires.Count > 0)
                    {
                        DrawWireLinesBatch(g, unrenderedWires, tileSize, viewOffset, gridOffset);
                        DrawWireDotsBatch(g, unrenderedWires, tileSize, viewOffset, gridOffset);
                    }

                    if (_wireBuilder.IsDrawing && _wireBuilder.StartPoint.HasValue)
                    {
                        var wireStart = _wireBuilder.StartPoint.Value;
                        var wireEnd = _wireBuilder.EndPoint ?? wireStart;
                        var wirePath = CalculatePipePath(wireStart, wireEnd);
                        DrawTempPipePath(g, wirePath, tileSize, viewOffset, gridOffset);
                    }
                }

                // Связи атмо-сигнализаций — независимый слой: флаг «🔧 Трубы» на них
                // не влияет (раньше из-за лишнего условия ShowPipeOverlay кнопка 🔗
                // ничего не показывала при выключенных трубах).
                if (ShowAlarmConnections && _currentNetwork != null)
                {
                    DrawAlarmConnections(g, _currentNetwork, tileSize, viewOffset, gridOffset, visibleRect);
                }
            }


            DrawInfo(g, scale, toolName, map);

            // Рисуем предпросмотр размещаемого объекта под курсором (привязки, узлы
            // сети, сигнализации и обычные прототипы — все через один механизм)
            DrawEntityPreview(g, scale, viewOffset);
            DrawExpandRoomPreview(g, scale, viewOffset);
            DrawSelectionHighlight(g, scale, viewOffset);
            DrawSelectionBox(g);
            DrawDeleteAreaOverlay(g, scale, viewOffset);
            if (ShowDividerAreas)
                DrawDividerAreasOverlay(g, scale, viewOffset);
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
        var (drawW, drawH) = GetAspectDrawSize(rect, src);

        // Спрайт центрируется на исходном тайле (том, куда фактически поставлен
        // объект) и может выходить за его границы во все стороны — точно так же,
        // как раньше это уже работало для высоких неквадратных спрайтов.
        int drawX = rect.X + (rect.Width - drawW) / 2;
        int drawY = rect.Y + (rect.Height - drawH) / 2;
        g.DrawImage(texture, new Rectangle(drawX, drawY, drawW, drawH), src, GraphicsUnit.Pixel);
    }

    /// <summary>
    /// Размер, в который DrawPreservingAspect выводит кадр src внутри rect (масштаб по
    /// каждой оси от 32 px/тайл). Вынесено отдельно, чтобы запекание спрайта считало
    /// ровно ту же геометрию, что и обычная отрисовка.
    /// </summary>
    private static (int w, int h) GetAspectDrawSize(Rectangle rect, Rectangle src)
    {
        float tilesWide = (float)src.Width / RsiBaseTexelsPerTile;
        float tilesHigh = (float)src.Height / RsiBaseTexelsPerTile;
        return (Math.Max(1, (int)Math.Round(rect.Width * tilesWide)),
                Math.Max(1, (int)Math.Round(rect.Height * tilesHigh)));
    }

    /// <summary>
    /// Четверть оборота (0=S,1=E,2=N,3=W) для угла, кратного 90°, либо -1, если угол
    /// произвольный. Нужен, чтобы не гонять GDI+ через g.Transform: у него именно
    /// ровно 90° — самый медленный путь (~1 мс на спрайт вместо ~6 мкс).
    /// </summary>
    private static int GetQuarterTurn(float rotation)
    {
        float norm = rotation % (float)(2 * Math.PI);
        if (norm < 0) norm += (float)(2 * Math.PI);
        float degrees = norm * 180f / (float)Math.PI;
        int quarter = (int)Math.Round(degrees / 90f);
        return Math.Abs(degrees - quarter * 90f) < 0.01f ? ((quarter % 4) + 4) % 4 : -1;
    }

    /// <summary>
    /// Берёт из кэша (или выпекает) спрайт нужного размера с тонированием и поворотом
    /// на четверть оборота. Готовый битмап потом выводится обычным DrawImage — это
    /// в десятки раз дешевле, чем DrawImage с ImageAttributes или с Transform.
    /// rotation может быть 0 (только тонирование). null — угол не кратен 90°.
    /// </summary>
    private Bitmap? GetBakedSprite(string id, int variant, Image texture, Rectangle src, int width, int height,
                                   float rotation, string? tintColor)
    {
        if (texture == null || src.Width <= 0 || src.Height <= 0 || width <= 0 || height <= 0) return null;

        int quarter = GetQuarterTurn(rotation);
        if (quarter < 0) return null;

        // Кэш однослойных «экземпляров» ключуется размером (width/height), поэтому при
        // смене зума его НЕ очищаем: записи под другие размеры остаются валидными, и
        // возврат на ранее посещённый зум снова попадает в кэш (иначе каждый зум-шаг
        // перепекал бы всё, даже если этот размер уже видели). Вытесняет только лимит
        // MaxEntries внизу.

        var key = new BakedSpriteKey(id, variant, quarter, width, height, tintColor);
        if (_bakedSpriteCache.TryGetValue(key, out var cached)) return cached;

        if (_bakedSpriteCache.Count >= BakedSpriteMaxEntries) ClearBakedSprites();

        Bitmap bmp = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.InterpolationMode = _interpolationMode;
                bg.PixelOffsetMode = PixelOffsetMode.Half;
                bg.SmoothingMode = SmoothingMode.None;
                var dest = new Rectangle(0, 0, width, height);
                var tint = string.IsNullOrEmpty(tintColor) ? null : GetDecalTintAttributes(tintColor!);
                if (tint != null)
                    bg.DrawImage(texture, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, tint);
                else
                    bg.DrawImage(texture, dest, src, GraphicsUnit.Pixel);
            }

            // RotateFlip меняет местами ширину и высоту у 90°/270° — как и поворот
            // вокруг центра тайла, который делал WithRotation.
            if (quarter != 0)
            {
                bmp.RotateFlip(quarter switch
                {
                    1 => RotateFlipType.Rotate90FlipNone,
                    2 => RotateFlipType.Rotate180FlipNone,
                    _ => RotateFlipType.Rotate270FlipNone
                });
            }
        }
        catch
        {
            bmp.Dispose();
            return null;
        }

        _bakedSpriteCache[key] = bmp;
        return bmp;
    }

    /// <summary>Выводит запечённый спрайт по центру anchor-прямоугольника (как WithRotation).</summary>
    private static void DrawBakedSprite(Graphics g, Bitmap baked, Rectangle anchor)
    {
        g.DrawImage(baked, anchor.X + (anchor.Width - baked.Width) / 2,
                           anchor.Y + (anchor.Height - baked.Height) / 2,
                           baked.Width, baked.Height);
    }

    /// <summary>
    /// Пытается нарисовать спрайт с поворотом без GDI+-трансформации: если угол кратен
    /// 90°, кадр один раз выпекается (с тонированием, если задан цвет) и выводится 1:1
    /// по центру rect. false — угол произвольный, рисовать надо обычным путём.
    /// preserveAspect — как в прежней отрисовке: без тонирования кадр сохранял пропорции,
    /// с тонированием растягивался на весь rect.
    /// </summary>
    private bool TryDrawBaked(Graphics g, string id, int variant, Image texture, Rectangle rect, Rectangle src,
                              float rotation, string? tintColor, bool preserveAspect)
    {
        int quarter = GetQuarterTurn(rotation);
        if (quarter < 0) return false;

        int w = rect.Width, h = rect.Height;
        if (preserveAspect) (w, h) = GetAspectDrawSize(rect, src);

        var baked = GetBakedSprite(id, variant, texture, src, w, h, rotation, tintColor);
        if (baked == null) return false;

        DrawBakedSprite(g, baked, rect);
        return true;
    }

    private void ClearBakedSprites()
    {
        foreach (var kvp in _bakedSpriteCache)
            kvp.Value.Dispose();
        _bakedSpriteCache.Clear();
    }
    /// <summary>
    /// Рисует текстуру прототипа в rect (с опциональным тонированием tint), либо, если текстуры
    /// нет, вызывает fallback(g, rect) — там уже своя заглушка (цвет/эмодзи/рамка), т.к. она у
    /// каждого типа объекта своя.
    /// </summary>
    private void DrawTexturedRect(Graphics g, string? protoId, Rectangle rect, ImageAttributes? tint, Action<Graphics, Rectangle>? fallback, float rotation = 0f, string? tintColor = null)
    {
        // snapCardinals: true — спрайт не вращается при повороте (гироскоп и т.п.).
        // Обнуляем поворот до входа в любой путь отрисовки (однослойный/многослойный),
        // иначе симметричный спрайт с directions:1 аффинно крутится за сущностью.
        if (!string.IsNullOrEmpty(protoId) && GetProtoMeta(protoId!).SnapCardinals)
            rotation = 0f;

        // Многослойный прототип (YAML Sprite.layers) — рисуем все слои поверх друг
        // друга отдельным путём и выходим, не трогая старую однослойную логику ниже.
        // Слои берутся из кэша метаданных: GetLayers обходил цепочку parent: на
        // каждый объект каждый кадр и возвращал новый List даже без слоёв.
        var layered = GetProtoLayers(protoId ?? "");
        if (layered != null)
        {
            DrawLayeredTexturedRect(g, protoId!, layered, rect, tint, fallback, rotation, tintColor);
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
            var (offX, offY) = GetProtoSpriteOffset(protoId ?? "");
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
            bool hasDirections = GetStateDirections(protoId!) >= 4;

            // ЭКЗЕМПЛЯРЫ: при зуме кадр рисуется в одном и том же размере rect для всех
            // объектов прототипа. Вместо масштабированного DrawImage на каждый объект
            // (при отдалении ~15 мкс против ~3 мкс у блита 1:1) один раз печём копию
            // в этом размере и выводим 1:1.
            //
            // Применяем это ТОЛЬКО к ненаправленным спрайтам: у них кадр не зависит от
            // стороны, и поворот — чистое вращение битмапа. У направленных же кадр обязан
            // выбираться по rotation (GetSourceRect), и запечённая копия по одному ключу
            // не могла бы различать стороны/вращение — поэтому для них оставляем
            // исходный путь DoDraw(), который каждый кадр выбирает нужный кадр.
            if (!hasDirections &&
                TryDrawBaked(g, protoId!, 0, texture, rect, src, rotation, tintColor, preserveAspect: tint == null))
            {
                return;
            }

            if (hasDirections)
            {
                DoDraw();
            }
            else if (rotation != 0f)
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                WithRotation(g, cx, cy, rotation, DoDraw);
            }
            else
            {
                DoDraw();
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
    private void DrawLayeredTexturedRect(Graphics g, string protoId, List<Models.SpriteLayer> layers, Rectangle rect, ImageAttributes? tint, Action<Graphics, Rectangle>? fallback, float rotation, string? tintColor = null)
    {
        var (offX, offY) = GetProtoSpriteOffset(protoId);
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

        // Быстрый путь: слоёв со своим смещением нет, поворот кратен 90° — вся
        // композиция уже собрана в один битмап, и на кадр остаётся ОДИН DrawImage
        // вместо N (у телеком-сервера их 15). Слои с HasOffset идут старым путём:
        // их смещение поворачивается вокруг центра клетки, а не центра слоя.
        if (tint == null && LayerSetCanBeComposited(layers))
        {
            int quarter = GetQuarterTurn(rotation);
            if (quarter >= 0)
            {
                var composite = GetLayeredComposite(protoId, layers, rect.Width, rect.Height, quarter);
                if (composite != null)
                {
                    DrawBakedSprite(g, composite, rect);
                    return;
                }
            }
        }

        // EntityStorageVisuals (подмена state базовых слоёв) резолвится внутри
        // GetLayerInfo — один раз на (прототип, слой), а не на каждую сущность в кадре.

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.Visible) continue;

            // Составной ключ кэша, текстура и признак «у слоя есть направления»
            // кэшируются по (прототип, номер слоя): раньше строка склеивалась и
            // текстура искалась заново на КАЖДЫЙ слой КАЖДОЙ сущности в кадре
            // (у телеком-сервера до 15 слоёв), а на карте это тысячи объектов.
            var layerInfo = GetLayerInfo(protoId, i, layer);
            if (layerInfo.Texture == null) continue;

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
            bool layerHasDirections = layerInfo.HasDirections;
            string cacheKey = layerInfo.CacheKey;
            Image layerTexture = layerInfo.Texture;

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
            {
                DrawThisLayer();
                continue;
            }

            // Поворот кратен 90°: выпекаем слой (с тонированием и поворотом) один раз
            // и выводим 1:1 — GDI+ с ImageAttributes/Transform на этом был узким местом.
            // Слой со своим смещением поворачивается вокруг центра СВОЕГО прямоугольника,
            // а прежний WithRotation крутил его вокруг центра всей клетки — для смещённых
            // слоёв запекание дало бы сдвиг, поэтому такие слои идут старым путём.
            bool layerCanBake = !layer.HasOffset;

            string? layerTintColor = tint != null ? tintColor
                                   : (!string.IsNullOrEmpty(layer.Color) ? layer.Color : null);
            if (layerCanBake && rotation != 0f &&
                TryDrawBaked(g, protoId, i + 1, layerTexture, layerRect,
                             GetSourceRect(cacheKey, layerTexture, 0f), rotation, layerTintColor,
                             preserveAspect: layerTintColor == null))
            {
                continue;
            }

            WithRotation(g, cx, cy, rotation, DrawThisLayer);
        }

        if (!anyDrawn)
            fallback?.Invoke(g, rect);
    }


    /// <summary>
    /// Можно ли собрать композицию слоёв в один битмап: ни у одного видимого слоя
    /// нет собственного смещения (иначе поворот вокруг центра слоя не совпадёт
    /// с поворотом вокруг центра клетки).
    /// </summary>
    private static bool LayerSetCanBeComposited(List<Models.SpriteLayer> layers)
    {
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].Visible && layers[i].HasOffset) return false;
        return true;
    }
    /// <summary>
    /// Собирает все видимые слои прототипа в один битмап: на кадр вместо N DrawImage
    /// (у телеком-сервера 15) остаётся один. Слои без направленных кадров поворачиваются
    /// здесь же через RotateFlip, у направленных кадр выбирает GetSourceRect — ровно как
    /// в прежней построчной отрисовке, поэтому картинка не меняется. null — если ни одна
    /// текстура слоя не загрузилась (тогда работает прежний путь с фолбэком).
    /// Слои со своим смещением (HasOffset) здесь не участвуют: вызывающий код для такого
    /// прототипа идёт старым путём, потому что смещение поворачивается вокруг центра
    /// клетки, а не центра слоя.
    /// </summary>
    private Bitmap? GetLayeredComposite(string protoId, List<Models.SpriteLayer> layers, int width, int height, int quarter)
    {
        // Кэш многослойных композиций ключуется размером (width/height) — при смене зума
        // не очищаем: записи под другие размеры остаются валидными, и возврат на прежний
        // зум снова попадает в кэш вместо перепекания. Вытесняет только лимит ниже.

        var key = (protoId, quarter, width, height);
        if (_layeredCompositeCache.TryGetValue(key, out var cached)) return cached;

        if (_layeredCompositeCache.Count >= LayeredCompositeMaxEntries) ClearLayeredComposites();

        float quarterAngle = quarter * (float)(Math.PI / 2);
        var layerBitmaps = new List<Bitmap>();

        try
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.Visible || layer.HasOffset) continue;

                var info = GetLayerInfo(protoId, i, layer);
                if (info.Texture == null) continue;

                var src = GetSourceRect(info.CacheKey, info.Texture, quarterAngle);
                if (src.Width == 0 || src.Height == 0) continue;

                var tint = !string.IsNullOrEmpty(layer.Color) ? GetDecalTintAttributes(layer.Color) : null;
                var (w, h) = tint != null
                    ? (width, height)
                    : GetAspectDrawSize(new Rectangle(0, 0, width, height), src);

                var layerBmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var lg = Graphics.FromImage(layerBmp))
                {
                    lg.InterpolationMode = _interpolationMode;
                    lg.PixelOffsetMode = PixelOffsetMode.Half;
                    lg.SmoothingMode = SmoothingMode.None;
                    var dst = new Rectangle(0, 0, w, h);
                    if (tint != null)
                        lg.DrawImage(info.Texture, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, tint);
                    else
                        lg.DrawImage(info.Texture, dst, src, GraphicsUnit.Pixel);
                }

                // Направленный слой уже выбрал кадр по четверти — его крутить не нужно
                if (!info.HasDirections && quarter != 0)
                {
                    layerBmp.RotateFlip(quarter switch
                    {
                        1 => RotateFlipType.Rotate90FlipNone,
                        2 => RotateFlipType.Rotate180FlipNone,
                        _ => RotateFlipType.Rotate270FlipNone
                    });
                }

                layerBitmaps.Add(layerBmp);
            }

            if (layerBitmaps.Count == 0)
            {
                _layeredCompositeCache[key] = null;
                return null;
            }

            int canvasW = 0, canvasH = 0;
            foreach (var b in layerBitmaps)
            {
                if (b.Width > canvasW) canvasW = b.Width;
                if (b.Height > canvasH) canvasH = b.Height;
            }

            var composite = new Bitmap(canvasW, canvasH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var cg = Graphics.FromImage(composite))
            {
                cg.InterpolationMode = InterpolationMode.NearestNeighbor;
                cg.PixelOffsetMode = PixelOffsetMode.Half;
                cg.SmoothingMode = SmoothingMode.None;
                foreach (var b in layerBitmaps)
                    cg.DrawImage(b, (canvasW - b.Width) / 2, (canvasH - b.Height) / 2, b.Width, b.Height);
            }

            _layeredCompositeCache[key] = composite;
            return composite;
        }
        catch
        {
            return null;
        }
        finally
        {
            foreach (var b in layerBitmaps) b.Dispose();
        }
    }

    /// <summary>
    /// Собирает 4 угловых слоя IconSmooth в один битмап (вместо 4 DrawImage на объект).
    /// Кадры выбираются теми же layerFills/layerAngle, что и в прежней отрисовке,
    /// поэтому картинка не меняется. null — ни один state-кадр не загрузился.
    /// </summary>
    private Bitmap? GetSmoothComposite(string protoId, IconSmoothSettings settings, int mask, int quarter, int tileSize)
    {
        // Кэш смус-композиций ключуется tileSize — при смене зума не очищаем: записи под
        // другие размеры остаются валидными, и возврат на прежний зум снова попадает
        // в кэш вместо перепекания. Вытесняет только лимит ниже.

        var key = (protoId, mask, quarter, tileSize);
        if (_smoothCompositeCache.TryGetValue(key, out var cached)) return cached;

        if (_smoothCompositeCache.Count >= SmoothCompositeMaxEntries) ClearSmoothComposites();

        var layerFills = RotateLayerFills(ComputeCornerFills(mask), quarter);
        var images = new Image?[4];
        var keys = new string?[4];
        bool anyLoaded = false;
        for (int li = 0; li < 4; li++)
        {
            var (img, key2) = GetSmoothState(protoId, settings, LayerFillAt(layerFills, li));
            images[li] = img;
            keys[li] = key2;
            if (img != null) anyLoaded = true;
        }

        if (!anyLoaded)
        {
            _smoothCompositeCache[key] = null;
            return null;
        }

        Bitmap? bmp = null;
        try
        {
            bmp = new Bitmap(tileSize, tileSize, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.InterpolationMode = _interpolationMode;
                bg.PixelOffsetMode = PixelOffsetMode.Half;
                bg.SmoothingMode = SmoothingMode.None;
                var rect = new Rectangle(0, 0, tileSize, tileSize);

                for (int li = 0; li < 4; li++)
                {
                    var img = images[li];
                    var stateKey = keys[li];
                    if (img == null || stateKey == null) continue;

                    float layerAngle = quarter * (float)(Math.PI / 2) + (float)IconSmoothLayerBaseAngle[li];
                    var src = GetSourceRect(stateKey, img, layerAngle);
                    if (src.Width == 0 || src.Height == 0) continue;
                    bg.DrawImage(img, rect, src, GraphicsUnit.Pixel);
                }
            }

            _smoothCompositeCache[key] = bmp;
            return bmp;
        }
        catch
        {
            bmp?.Dispose();
            return null;
        }
    }

    private void ClearLayeredComposites()
    {
        foreach (var kvp in _layeredCompositeCache)
            kvp.Value?.Dispose();
        _layeredCompositeCache.Clear();
    }

    private void ClearSmoothComposites()
    {
        foreach (var kvp in _smoothCompositeCache)
            kvp.Value?.Dispose();
        _smoothCompositeCache.Clear();
    }

    /// <summary>
    /// Данные отрисовки одного слоя многослойного прототипа — из кэша по
    /// (прототип, номер слоя). Здесь же резолвится подмена state из EntityStorageVisuals
    /// (шкафчики/локеры переопределяют state базовых слоёв: Base → stateBaseClosed,
    /// Door → stateDoorClosed). Сам компонент Sprite с layers живёт у далёкого предка
    /// (ClosetBase, closet.rsi), а state — у конкретного наследника
    /// (LockerCaptionFilled и т.п.), поэтому без подмены рисовался бы дефолтный
    /// "generic"/"generic_door". Раньше это делалось на каждую сущность в кадре.
    /// </summary>
    private (string CacheKey, Image? Texture, bool HasDirections) GetLayerInfo(string protoId, int index, Models.SpriteLayer layer)
    {
        var key = (protoId, index);
        if (_layerInfoCache.TryGetValue(key, out var cached)) return cached;

        string effectiveState = layer.State;
        var storageViz = GetProtoMeta(protoId).StorageVisuals;
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
                bool first = index == 0;
                bool second = index == 1;
                isBase = first && !second;
                isDoor = !first && second;
            }

            string? overrideState = isBase ? storageViz.Value.baseClosed
                                   : isDoor ? storageViz.Value.doorClosed
                                   : null;
            if (!string.IsNullOrEmpty(overrideState))
                effectiveState = overrideState;
        }

        string cacheKey = $"{protoId}__layer{index}__{effectiveState ?? "?"}";
        var texture = GetOrLoadLayerTexture(cacheKey, protoId, layer, effectiveState);
        bool hasDirections = texture != null && GetStateDirections(cacheKey) >= 4;

        var result = (cacheKey, texture, hasDirections);
        _layerInfoCache[key] = result;
        return result;
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

            if (TryDrawDecalBaked(g, decal, rect))
                continue;

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
                }, decal.Rotation, string.IsNullOrEmpty(decal.Color) ? null : decal.Color);
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

                // Смещение узла зависит от типа кабеля (как у труб): иначе кабели
                // разных типов на одной клетке рисовались бы поверх друг друга.
                var (cx, cy) = GetWireNodeScreenCenter(wire.X, wire.Y, wire.WireType, tileSize, viewOffset, gridOffset);
                var (nx, ny) = GetWireNodeScreenCenter(key.Item1, key.Item2, wire.WireType, tileSize, viewOffset, gridOffset);
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

            var (cx, cy) = GetWireNodeScreenCenter(wire.X, wire.Y, wire.WireType, tileSize, viewOffset, gridOffset);

            g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
        }

        foreach (var b in brushCache.Values)
            b.Dispose();
    }

    /// <summary>
    /// Кабель-СУЩНОСТЬ из панели репозитория: стейт по маске соседей, как
    /// CableVisualizerSystem в игре. Маска WireVisDirFlags: North=1, South=2,
    /// East=4, West=8; соседи — кабели с тем же ConnectivityKey (NodeGroupID:
    /// HVPower/MVPower/Apc/ExCable) в индексе _cableKeysByTile.
    /// Стейт слоя 0 = "{StatePrefix}{mask}", слоя 1 = "{ExtraLayerPrefix}{mask}".
    /// Возвращает false, если базовый стейт не загрузился — тогда вызывающий
    /// рисует обычный статический спрайт прототипа.
    /// </summary>
    private bool DrawSingleCableEntity(Graphics g, string protoId, CableRenderInfo info, float worldX, float worldY, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        int tx = (int)Math.Floor(worldX);
        int ty = (int)Math.Floor(worldY);

        // Координаты редактора: Y растёт вниз, поэтому север — это (x, y-1).
        int mask = 0;
        if (HasCableNeighbor(tx, ty - 1, info.ConnectivityKey)) mask |= 1; // North
        if (HasCableNeighbor(tx, ty + 1, info.ConnectivityKey)) mask |= 2; // South
        if (HasCableNeighbor(tx + 1, ty, info.ConnectivityKey)) mask |= 4; // East
        if (HasCableNeighbor(tx - 1, ty, info.ConnectivityKey)) mask |= 8; // West

        // Сущность репозитория стоит в центре клетки (X/Y = tile + 0.5);
        // -0.5 переводит её в прямоугольник самой клетки, как у остальных сущностей.
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        if (!DrawCableState(g, protoId, info.StatePrefix, mask, info.Layer0Color, rect))
            return false;

        if (!string.IsNullOrEmpty(info.ExtraLayerPrefix))
            DrawCableState(g, protoId, info.ExtraLayerPrefix!, mask, info.Layer1Color, rect);

        return true;
    }

    /// <summary>
    /// Провода, поставленные инструментом «Электросеть», рисуются теми же спрайтами и с
    /// тем же подбором стейта по соседям, что и кабели-сущности из репозитория
    /// (CableVisualizer): стейт "{StatePrefix}{mask}", маска — соседи с тем же ключом
    /// связи (HV→HVPower, MV→MVPower, LV→Apc) в индексе _cableKeysByTile, куда входят
    /// и кабели-сущности, и провода инструмента. Возвращает провода, для которых спрайт
    /// нарисовать не удалось (нет прототипа кабеля/стейта) — их вызывающий рисует
    /// прежними линиями и точками.
    /// </summary>
    private List<WireEntity> DrawWireCableSprites(Graphics g, List<WireEntity> wires, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var failed = new List<WireEntity>();
        if (wires.Count == 0) return failed;

        foreach (var wire in wires)
        {
            string? proto = WireBuilder.GetCableProtoForWireType(wire.WireType);
            var info = proto != null ? GetCableRenderInfo(proto) : null;

            // WireEntity хранится в целых координатах клетки, а кабель-сущность — в её
            // центре (X/Y = tile + 0.5): +0.5 переводит провод в ту же систему координат,
            // иначе спрайт съехал бы на полтайла.
            if (proto == null || info == null ||
                !DrawSingleCableEntity(g, proto, info, wire.X + 0.5f, wire.Y + 0.5f, tileSize, viewOffset, gridOffset))
            {
                failed.Add(wire);
            }
        }

        return failed;
    }

    /// <summary>Есть ли на клетке (x,y) кабель с тем же ключом связи (сущность или провод).</summary>
    private bool HasCableNeighbor(int x, int y, string key)
    {
        return !string.IsNullOrEmpty(key) &&
               _cableKeysByTile.TryGetValue((x, y), out var keys) &&
               keys.Contains(key);
    }

    /// <summary>Рисует один слой CableVisualizer: стейт "{prefix}{mask}" с тонированием слоя.</summary>
    private bool DrawCableState(Graphics g, string protoId, string prefix, int mask, string? color, Rectangle rect)
    {
        string state = prefix + mask;
        string cacheKey = $"{protoId}||cable||{state}";

        Image? texture = GetOrLoadCableStateTexture(cacheKey, protoId, state);
        if (texture == null) return false;

        var src = GetSourceRect(cacheKey, texture, 0f);
        if (src.Width == 0 || src.Height == 0) return false;

        // Цвет слоя из YAML (Yellow/Green/hex). GetDecalTintAttributes умеет и имена,
        // и hex, и кэширует ImageAttributes по строке цвета.
        var tint = !string.IsNullOrEmpty(color) ? GetDecalTintAttributes(color!) : null;

        if (tint != null)
        {
            // DrawImage с ImageAttributes — ~118 мкс на спрайт, а кабелей на карте сотни.
            // Печём состояние кабеля с тонированием один раз (ключ — protoId + маска
            // соседей) и дальше выводим обычным 1:1 DrawImage.
            var baked = GetBakedSprite(protoId, mask, texture, src, rect.Width, rect.Height, 0f, color);
            if (baked != null)
            {
                DrawBakedSprite(g, baked, rect);
                return true;
            }
            g.DrawImage(texture, rect, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, tint);
        }
        else
        {
            DrawPreservingAspect(g, texture, rect, src);
        }

        return true;
    }

    /// <summary>
    /// Загружает и кэширует точный стейт кабеля ("mvcable_7" и т.п.). В отличие от
    /// обычных слоёв/прототипов здесь НЕЛЬЗЯ подставлять дефолтный state: иначе
    /// отсутствующий "mvcable_7" молча рисовался бы как "mvcable_0" и ломал картинку.
    /// </summary>
    private Image? GetOrLoadCableStateTexture(string cacheKey, string protoId, string state)
    {
        if (_textureCache.TryGetValue(cacheKey, out var cached))
            return cached;

        Image? texture = null;
        if (_indexer != null)
        {
            string? path = _indexer.GetRsiStatePath(protoId, state);
            if (path != null && File.Exists(path))
            {
                try
                {
                    texture = Image.FromFile(path);
                    _protoTextureDirCache[cacheKey] = Path.GetDirectoryName(path) ?? "";
                    _protoStateNameCache[cacheKey] = state;
                }
                catch { texture = null; }
            }
        }

        _textureCache[cacheKey] = texture;
        return texture;
    }

    /// <summary>
    /// Визуальная информация кабеля из прототипа (CableVisualizer + цвета слоёв),
    /// с кэшем по protoId. Возвращает null, если компонента нет ни у прототипа, ни
    /// у родителей — такой кабель рисуется фолбэком.
    /// </summary>
    private CableRenderInfo? GetCableRenderInfo(string protoId)
    {
        if (_indexer == null) return null;
        if (_cableRenderInfoCache.TryGetValue(protoId, out var cached))
            return cached;

        CableRenderInfo? info = null;
        var visualizer = _indexer.GetCableVisualizer(protoId);

        if (visualizer != null && !string.IsNullOrEmpty(visualizer.StatePrefix))
        {
            var layers = GetProtoLayers(protoId) ?? new List<Models.SpriteLayer>();
            string? componentColor = _indexer.GetSpriteColor(protoId);

            info = new CableRenderInfo
            {
                StatePrefix = visualizer.StatePrefix!,
                ExtraLayerPrefix = visualizer.ExtraLayerPrefix,
                // Соседство — по NodeGroupID (как CableVisSystem в игре); если его
                // нет в YAML, откатываемся на префикс спрайтов (одна семья кабелей).
                ConnectivityKey = _indexer.GetCableNodeGroup(protoId) ?? visualizer.StatePrefix!,
                // Слой 0 — тело кабеля; если у слоя нет своего цвета, берём цвет
                // компонента Sprite. Слой 1 — доп. полосы (у MV они заданы своими
                // цветами в слое, у LV слоя 1 нет вовсе).
                Layer0Color = layers.Count > 0 ? (layers[0].Color ?? componentColor) : componentColor,
                Layer1Color = layers.Count > 1 ? (layers[1].Color ?? componentColor) : null
            };
        }

        _cableRenderInfoCache[protoId] = info;
        return info;
    }

    private Color GetWireColor(string wireType)
    {
        // В отличие от GetPipeColor (жёстко зашитые цвета труб), цвет кабеля
        // берётся из WireTypeManager — так диалог "Настройки" реально влияет
        // на отрисовку, а не только на иконки инструментов
        return _wireTypeManager.GetWireType(wireType).Color;
    }

    /// <summary>
    /// Смещение узла кабеля от центра тайла в долях тайла, в зависимости ТОЛЬКО от типа
    /// кабеля — та же идея, что у труб (GetPipeTypeOffset): на одной клетке могут лежать
    /// кабели разных типов, и без разведения они рисовались бы поверх друг друга.
    /// ВВ уходит влево-вверх, НВ — вправо-вниз, СВ остаётся по центру.
    /// </summary>
    public static (float offsetX, float offsetY) GetWireTypeOffset(string wireType)
    {
        const float q = 0.25f; // четверть тайла

        return wireType switch
        {
            "HV" => (-q, -q), // ВВ — влево-вверх
            "LV" => (q, q),   // НВ — вправо-вниз
            _ => (0, 0)       // СВ (и незнакомые типы) — по центру
        };
    }

    /// <summary>
    /// Экранный центр узла кабеля с учётом смещения по типу — единая точка расчёта для
    /// линий и точек, чтобы они всегда совпадали.
    /// </summary>
    private static (float cx, float cy) GetWireNodeScreenCenter(float x, float y, string wireType, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        var (offX, offY) = GetWireTypeOffset(wireType);

        float cx = (x + 0.5f + offX + gridOffset.X) * tileSize - viewOffset.X;
        float cy = (y + 0.5f + offY + gridOffset.Y) * tileSize - viewOffset.Y;
        return (cx, cy);
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
    /// Камера НЕ видит сквозь стены: сначала собирается карта непрозрачных клеток
    /// (см. CollectCameraOccluders), а затем считается видимость ровно так же, как
    /// StationAiVisionSystem.ViewJob в игре (GetCameraVisibleTiles): две волны по
    /// глубине — диагональная (по Чебышёву) и прямая (по манхэттенскому расстоянию),
    /// плюс отдельный проход по углам стен. Именно такой расчёт даёт в игре раскрытие
    /// обзора под 45° (1 клетка в сторону на 1 клетку вглубь), а не узкий конус
    /// рейкаста в центр клетки (~22.5°).
    /// Пересекающиеся зоны не дают двойной альфы — все покрытые тайлы собираются
    /// в один HashSet, а за экран области не считаются вовсе (IsTileVisible).
    /// </summary>
    private void DrawCameraCoverageOverlay(Graphics g, Grid grid, TileGrid tileGrid, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        // Вся тяжёлая часть (перекрытия + видимость каждой камеры) считается один раз
        // на структурное изменение грида — см. GetCameraCoverage.
        var coverage = GetCameraCoverage(grid, tileGrid);
        if (coverage.Cameras.Count == 0) return;

        // Клетки собираем только от камер, чья зона вообще пересекает кадр: у камеры
        // дальше 6.5+запас клеток от видимой области закрашивать нечего.
        const int cullMargin = 9;
        var covered = new HashSet<(int x, int y)>();
        foreach (var kv in coverage.VisibleByCamera)
        {
            var (cx, cy) = kv.Key;
            if (cx + cullMargin < visibleRect.Left || cx - cullMargin > visibleRect.Right ||
                cy + cullMargin < visibleRect.Top || cy - cullMargin > visibleRect.Bottom) continue;

            foreach (var tile in kv.Value)
            {
                if (!IsTileVisible(tile.x, tile.y, visibleRect)) continue;
                covered.Add(tile);
            }
        }

        if (covered.Count == 0) return;

        using var brush = new SolidBrush(Color.FromArgb(45, 60, 140, 230));
        FillTileSpans(g, covered, tileSize, viewOffset, gridOffset, brush);
    }

    /// <summary>
    /// Перекрытия для камер и их зоны обзора, с кэшем по гриду. Пересчитывается только
    /// после InvalidateTileGrid: камеры и стены между кадрами не двигаются, а раньше
    /// обход всех сущностей карты и волна видимости для каждой камеры выполнялись
    /// на каждый кадр (на стандартной карте SS14 — десятки камер, ~85 мс/кадр).
    /// </summary>
    private CameraCoverageCache GetCameraCoverage(Grid grid, TileGrid tileGrid)
    {
        if (!_dirtyCameraCoverage.Contains(grid.Uid) &&
            _cameraCoverageCache.TryGetValue(grid.Uid, out var cached))
        {
            return cached;
        }

        // Половина стороны квадрата: 6.5 клетки дают 13×13 тайлов.
        const float halfSideTiles = 6.5f;

        var cache = new CameraCoverageCache();
        var entityOccluders = new HashSet<(int x, int y)>();

        foreach (var entity in grid.Entities)
        {
            // «Привязка»-камера хранит прототип в CameraProto, обычная сущность — в Proto
            string proto = entity is CameraEntity camera ? camera.CameraProto : entity.Proto;
            if (IsCameraProto(proto))
                cache.Cameras.Add((entity.X, entity.Y));

            if (string.IsNullOrEmpty(proto) || _indexer == null) continue;

            // Occluder резолвится в кэше метаданных прототипа (обход parent: — один
            // раз на прототип, а не на каждую сущность в каждом кадре).
            if (GetProtoMeta(proto).IsOccluder)
                entityOccluders.Add(((int)Math.Floor(entity.X), (int)Math.Floor(entity.Y)));
        }

        if (cache.Cameras.Count > 0)
        {
            var occluders = CollectCameraOccluders(grid, tileGrid, cache.Cameras, entityOccluders);
            foreach (var (camX, camY) in cache.Cameras)
            {
                var key = ((int)Math.Floor(camX), (int)Math.Floor(camY));
                if (cache.VisibleByCamera.ContainsKey(key)) continue; // две камеры в одной клетке
                cache.VisibleByCamera[key] = new List<(int x, int y)>(
                    GetCameraVisibleTiles(occluders, camX, camY, halfSideTiles));
            }
        }

        _cameraCoverageCache[grid.Uid] = cache;
        _dirtyCameraCoverage.Remove(grid.Uid);
        return cache;
    }

    /// <summary>
    /// Заливка набора клеток горизонтальными полосами: вместо FillRectangle на каждую
    /// клетку — один прямоугольник на непрерывный ряд (зона обзора — почти квадрат,
    /// так что вызовов становится в ~13 раз меньше при том же объёме пикселей).
    /// </summary>
    private static void FillTileSpans(Graphics g, HashSet<(int x, int y)> tiles, int tileSize,
        PointF viewOffset, PointF gridOffset, Brush brush)
    {
        var sorted = new (int x, int y)[tiles.Count];
        tiles.CopyTo(sorted);
        Array.Sort(sorted, static (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        int i = 0;
        while (i < sorted.Length)
        {
            int y = sorted[i].y;
            int startX = sorted[i].x;
            int endX = startX;
            i++;
            for (; i < sorted.Length && sorted[i].y == y; i++)
            {
                if (sorted[i].x == endX + 1)
                {
                    endX = sorted[i].x;
                    continue;
                }
                FillRun(y, startX, endX);
                startX = endX = sorted[i].x;
            }
            FillRun(y, startX, endX);
        }

        void FillRun(int row, int x0, int x1)
        {
            float sx = (x0 + gridOffset.X) * tileSize - viewOffset.X;
            float sy = (row + gridOffset.Y) * tileSize - viewOffset.Y;
            g.FillRectangle(brush, sx, sy, (x1 - x0 + 1) * tileSize, tileSize);
        }
    }

    /// <summary>
    /// Видимые клетки для одной камеры — порт StationAiVisionSystem.ViewJob (игра):
    ///  1) диагональная волна vis2: клетка на расстоянии d+1 по Чебышёву видна,
    ///     если виден любой из 8 её соседей на глубине d; непрозрачная клетка
    ///     помечается -1 (сама видна, но дальше не пропускает);
    ///  2) прямая волна vis1: клетка на манхэттенском расстоянии d+1 видна, если
    ///     виден сосед на глубине d и клетка есть в vis2 — это и обрезает
    ///     «затекание» обзора за стены, сохраняя раскрытие под 45°;
    ///  3) углы: непрозрачные клетки на стыке двух стен добавляются в видимые,
    ///     чтобы за диагональными стыками не оставалось дыр.
    /// Клетки вне квадрата обзора (viewport) в результат не попадают — как и в игре.
    /// </summary>
    private static HashSet<(int x, int y)> GetCameraVisibleTiles(HashSet<(int x, int y)> occluders,
        float camX, float camY, float halfSideTiles)
    {
        int eyeX = (int)Math.Floor(camX);
        int eyeY = (int)Math.Floor(camY);

        // Квадрат обзора: те же 6.5 клетки по Чебышёву (тайлы с шагом до 6).
        const int maxScan = 8;
        var seedTiles = new HashSet<(int x, int y)>();
        int maxDepth = 0;
        int maxSum = 0;

        for (int dx = -maxScan; dx <= maxScan; dx++)
        {
            int tx = eyeX + dx;
            if (Math.Abs(tx + 0.5f - camX) > halfSideTiles) continue;

            for (int dy = -maxScan; dy <= maxScan; dy++)
            {
                int ty = eyeY + dy;
                if (Math.Abs(ty + 0.5f - camY) > halfSideTiles) continue;

                seedTiles.Add((tx, ty));
                maxDepth = Math.Max(maxDepth, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                maxSum = Math.Max(maxSum, Math.Abs(dx) + Math.Abs(dy));
            }
        }

        var vis1 = new Dictionary<(int x, int y), int>();
        var vis2 = new Dictionary<(int x, int y), int>();

        // Шаг 1: диагональная теневая волна (глубина — Чебышёв).
        for (int d = 0; d < maxDepth; d++)
        {
            foreach (var tile in seedTiles)
            {
                int depth = Math.Max(Math.Abs(tile.x - eyeX), Math.Abs(tile.y - eyeY));
                if (depth != d + 1) continue;
                if (!HasVisibleCameraNeighbor(vis2, tile, d)) continue;

                vis2[tile] = occluders.Contains(tile) ? -1 : d + 1;
            }
        }

        // Шаг 2: прямая теневая волна (глубина — манхэттенское расстояние).
        for (int d = 0; d < maxSum; d++)
        {
            foreach (var tile in seedTiles)
            {
                int depth = Math.Abs(tile.x - eyeX) + Math.Abs(tile.y - eyeY);
                if (depth != d + 1) continue;
                if (!HasVisibleCameraNeighbor(vis1, tile, d)) continue;

                if (occluders.Contains(tile))
                    vis1[tile] = -1;
                else if (vis2.GetValueOrDefault(tile) != 0)
                    vis1[tile] = d + 1;
            }
        }

        vis1[(eyeX, eyeY)] = 1;

        // Шаг 3: углы стен — иначе за диагональными стыками остаются дыры.
        foreach (var tile in seedTiles)
        {
            if (!occluders.Contains(tile)) continue;
            if (vis1.GetValueOrDefault(tile) != 0) continue;

            if (IsCameraVisionCorner(seedTiles, occluders, vis1, tile, (1, 1)) ||
                IsCameraVisionCorner(seedTiles, occluders, vis1, tile, (1, -1)) ||
                IsCameraVisionCorner(seedTiles, occluders, vis1, tile, (-1, -1)) ||
                IsCameraVisionCorner(seedTiles, occluders, vis1, tile, (-1, 1)))
            {
                vis1[tile] = -1;
            }
        }

        var visible = new HashSet<(int x, int y)>();
        foreach (var tile in seedTiles)
        {
            if (vis1.GetValueOrDefault(tile) != 0)
                visible.Add(tile);
        }

        return visible;
    }

    /// <summary>
    /// Есть ли у клетки сосед (из 8) с ровно такой глубиной в карте видимости.
    /// Непроставленные значения считаются нулём — на этом держится первый шаг
    /// волны (d = 0): в игре это тоже GetValueOrDefault == 0.
    /// </summary>
    private static bool HasVisibleCameraNeighbor(Dictionary<(int x, int y), int> vis, (int x, int y) tile, int depth)
    {
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                if (ox == 0 && oy == 0) continue;
                if (vis.GetValueOrDefault((tile.x + ox, tile.y + oy), 0) == depth) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// «Угол» по терминологии StationAiVisionSystem: непрозрачная клетка, у которой
    /// диагональ (delta) видна, обе смежные с ней по стороне клетки — тоже видимые
    /// стены, а сама диагональ не перекрыта. Такой стык добавляется в видимые тайлы.
    /// </summary>
    private static bool IsCameraVisionCorner(HashSet<(int x, int y)> tiles, HashSet<(int x, int y)> blocked,
        Dictionary<(int x, int y), int> vis1, (int x, int y) index, (int dx, int dy) delta)
    {
        var diagonal = (x: index.x + delta.dx, y: index.y + delta.dy);
        if (!tiles.Contains(diagonal)) return false;

        var cardinal1 = (x: index.x, y: diagonal.y);
        var cardinal2 = (x: diagonal.x, y: index.y);

        return vis1.GetValueOrDefault(diagonal) != 0 &&
               vis1.GetValueOrDefault(cardinal1) != 0 &&
               vis1.GetValueOrDefault(cardinal2) != 0 &&
               blocked.Contains(cardinal1) &&
               blocked.Contains(cardinal2) &&
               !blocked.Contains(diagonal);
    }

    /// <summary>
    /// Клетки, непрозрачные для камер (аналог _opaque из StationAiVisionSystem):
    /// тайлы-стены и окна (TileContent.Wall), закрытые двери (TileContent.Door и
    /// свободные двери вне комнат) и сущности с OccluderComponent — шторы, книжные
    /// шкафы, кустарник, болты, надувные стены и т.п. (см. DrawCameraCoverageOverlay).
    /// Открытое состояние двери в редакторе не моделируется — экспортируемый шлюз
    /// всегда закрыт, поэтому дверь считается перекрывающей.
    /// </summary>
    private static HashSet<(int x, int y)> CollectCameraOccluders(Grid grid, TileGrid tileGrid,
        List<(float X, float Y)> cameras, HashSet<(int x, int y)> entityOccluders)
    {
        var occluders = new HashSet<(int x, int y)>(entityOccluders);

        // Тайлы-стены/окна и двери собираем только вокруг камер: полный перебор тайлов
        // карты на каждый кадр оверлею не нужен — дальше края обзора они не влияют.
        const int margin = 8; // 6.5 клетки обзора + запас под соседей на границе
        foreach (var (camX, camY) in cameras)
        {
            int cx = (int)Math.Floor(camX);
            int cy = (int)Math.Floor(camY);

            for (int dx = -margin; dx <= margin; dx++)
            {
                for (int dy = -margin; dy <= margin; dy++)
                {
                    var tile = tileGrid.GetTile(cx + dx, cy + dy);
                    if (tile == null) continue;
                    if (tile.Content != TileContent.Wall && tile.Content != TileContent.Door) continue;
                    occluders.Add((tile.X, tile.Y));
                }
            }
        }

        // Двери вне комнат в тайл-сетку не попадают: TileBuilder строит её из комнат.
        foreach (var door in grid.LooseDoors)
            occluders.Add((door.X, door.Y));

        return occluders;
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
                var srcRect = GetSourceRect(protoId, texture);
                if (rotation != 0f && TryDrawBaked(g, protoId, 0, texture, rect, srcRect, rotation, null, preserveAspect: false))
                    continue;
                WithRotation(g, cx, cy, rotation, () =>
                {
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

        // Сущности-участники сети (кнопки/рычаги/устройства) хранят X/Y как дробный
        // ЦЕНТР тайла (при размещении +0.5, см. PlaceLinkedEntity), поэтому линия идёт
        // прямо от их центра — без дополнительного +0.5, иначе начало уезжало бы в угол.
        int NodeScreenX(float wx) => (int)((wx + gridOffset.X) * tileSize - viewOffset.X);
        int NodeScreenY(float wy) => (int)((wy + gridOffset.Y) * tileSize - viewOffset.Y);

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

    // Помощник: нарисует один тайл пола в чанк-графике (запекание) без учёта сортировки.
    private void DrawFloorTileInto(Graphics tg, float worldX, float worldY, string protoId, int tileSize)
    {
        // В чанке X/Y = локальные координаты тайла, поэтому рисуем от угла клетки.
        var rect = new Rectangle((int)(worldX * tileSize), (int)(worldY * tileSize), tileSize, tileSize);
        var texture = GetOrLoadTexture(protoId);
        var srcRect = texture != null ? GetSourceRect(protoId, texture, 0f) : Rectangle.Empty;

        if (texture != null)
        {
            tg.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
        }
        else
        {
            // Фолбэк-заглушка, как в DrawSingleTile (пол, opacity=1 при запекании).
            using var brush = new SolidBrush(Color.FromArgb(150, 200, 200, 200));
            tg.FillRectangle(brush, rect);
        }
    }

    // Рисует CTAТИЧНЫЙ слой пола через чанковый кэш. Возвращает список используется ли вообще
    // (пустой tileGrid пола -> ничеrmo рисуется). Интегрируется в z-порядок: пол запекается и
    // выводится целиком ПЕРВЫМ, до декалей/стен/дверей/сущностей, т.к. пол всегда внизу.
    private void DrawStaticFloorChunks(Graphics g, TileGrid tileGrid, int gridUid, int tileSize, PointF viewOffset, PointF gridOffset, RectangleF visibleRect)
    {
        // Число клеток в чанке зависит от пиксельного размера тайла: при сильном
        // приближении (тайл 128–256px) большие чанки раздували битмапы до 64 МБ и печь их
        // было очень дорого. Ключ включает tileSize, поэтому смену зума чистить не нужно —
        // записи под разные размеры не пересекаются, вытесняет LRU/лимит.
        int chunkTiles = GetChunkTiles(tileSize);

        // Диапазон видимых чанков по осям (в мировых координатах, инклюзивно).
        int chunkMinX = (int)Math.Floor(visibleRect.Left / chunkTiles);
        int chunkMaxX = (int)Math.Floor(visibleRect.Right / chunkTiles);
        int chunkMinY = (int)Math.Floor(visibleRect.Top / chunkTiles);
        int chunkMaxY = (int)Math.Floor(visibleRect.Bottom / chunkTiles);

        for (int cy = chunkMinY; cy <= chunkMaxY; cy++)
        {
            for (int cx = chunkMinX; cx <= chunkMaxX; cx++)
            {
                var key = (gridUid, tileSize, cx, cy);
                if (!_floorChunkCache.TryGetValue(key, out var chunk))
                {
                    if (_emptyFloorChunks.Contains(key))
                        continue;

                    chunk = BakeFloorChunk(tileGrid, key, tileSize);
                    if (chunk == null)
                    {
                        _emptyFloorChunks.Add(key);
                        continue;
                    }
                    InsertFloorChunk(key, chunk);
                }

                if (chunk == null) continue;

                // Экранные координаты левого верхнего угла чанка.
                int sx = (int)((cx * chunkTiles + gridOffset.X) * tileSize - viewOffset.X);
                int sy = (int)((cy * chunkTiles + gridOffset.Y) * tileSize - viewOffset.Y);
                g.DrawImage(chunk, sx, sy);
            }
        }
    }

    private void InsertFloorChunk((int gridUid, int tileSize, int cx, int cy) key, Bitmap chunk)
    {
        _floorChunkCache[key] = chunk;
        _floorChunkLru.AddLast(key);
        // LRU-обрезка: на сверхбольших картах не копим бесконечные чанки, если карта
        // «пролистывается» — выкидываем давно не использованные, начиная с головы очереди.
        while (_floorChunkCache.Count > StaticChunkMaxBitmaps && _floorChunkLru.Count > 0)
        {
            var lruKey = _floorChunkLru.First!.Value;
            _floorChunkLru.RemoveFirst();
            if (_floorChunkCache.TryGetValue(lruKey, out var old))
            {
                old.Dispose();
                _floorChunkCache.Remove(lruKey);
            }
        }
    }

    // Запекает чанк пола (GetChunkTiles(tileSize)×GetChunkTiles(tileSize) тайлов) в Bitmap.
    // Возвращает null, если в чанке нет ни одного тайла пола (чик пустой — не создаём битмап).
    private Bitmap? BakeFloorChunk(TileGrid tileGrid, (int gridUid, int tileSize, int cx, int cy) key, int tileSize)
    {
        int chunkTiles = GetChunkTiles(tileSize);
        int chunkPx = chunkTiles * tileSize;
        int baseX = key.cx * chunkTiles;
        int baseY = key.cy * chunkTiles;

        // Быстрая проверка: есть ли в этом чанке хоть один тайл пола (или пол под дверью).
        bool any = false;
        for (int ly = 0; ly < chunkTiles; ly++)
        {
            for (int lx = 0; lx < chunkTiles; lx++)
            {
                var tile = tileGrid.GetTile(baseX + lx, baseY + ly);
                if (tile != null &&
                    (tile.Content == TileContent.Floor ||
                     (tile.Content == TileContent.Door && tile.HasFloorUnder && !string.IsNullOrEmpty(tile.FloorProtoUnder))))
                { any = true; break; }
            }
            if (any) break;
        }
        if (!any) return null;

        var bmp = new Bitmap(chunkPx, chunkPx, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var tg = Graphics.FromImage(bmp))
        {
            tg.InterpolationMode = _interpolationMode;
            tg.SmoothingMode = SmoothingMode.None;
            tg.PixelOffsetMode = PixelOffsetMode.Half;

            for (int ly = 0; ly < chunkTiles; ly++)
            {
                for (int lx = 0; lx < chunkTiles; lx++)
                {
                    var tile = tileGrid.GetTile(baseX + lx, baseY + ly);
                    if (tile == null) continue;
                    if (tile.Content == TileContent.Floor)
                    {
                        DrawFloorTileInto(tg, lx, ly, tile.ProtoId ?? "Plating", tileSize);
                    }
                    else if (tile.Content == TileContent.Door && tile.HasFloorUnder && !string.IsNullOrEmpty(tile.FloorProtoUnder))
                    {
                        // Пол под дверью (прежний цикл «Пол под дверями»): чтобы под дверью
                        // не было пустоты, печём его в чанк — дверь рисуется позже поверх.
                        DrawFloorTileInto(tg, lx, ly, tile.FloorProtoUnder!, tileSize);
                    }
                }
            }
        }
        return bmp;
    }

    private void DrawRenderLayer(Graphics g, TileGrid tileGrid, Grid grid, int tileSize, PointF viewOffset, PointF gridOffset, float opacity, RectangleF visibleRect)
    {
        // Индекс IconSmooth (тайл → smooth-ключи): собирает И тайлы-стены, И сущности, чтобы
        // стены из генератора комнат и из панели репозитория сглаживались друг с другом.
        // Кэшируется по гриду — перестраивается один раз на структурное изменение
        // (тот же сигнал, что инвалидирует кэши рендера), а не на каждый кадр рендера
        // (индекс не зависит от области видимости, поэтому при панорамировании
        // переиспользуется без пересборки).
        bool rebuildRenderCaches = !_smoothIndexCache.TryGetValue(grid.Uid, out var smoothIndex) ||
                                   _dirtyRenderCaches.Contains(grid.Uid);

        if (rebuildRenderCaches)
        {
            smoothIndex = BuildIconSmoothIndex(grid, tileGrid);
            _smoothIndexCache[grid.Uid] = smoothIndex;

            // Статичные чанки пола тоже зависят от содержимого грида — при структурном
            // изменении выкидываем их, чтобы не рисовать устаревший пол.
            ClearGridFloorChunks(grid.Uid);
        }

        // Индекс кабелей-сущностей зависит от тех же структурных изменений, что и
        // сглаживание: пересобираем его в том же проходе, чтобы DrawSingleEntity
        // мог посчитать маску соседей.
        if (rebuildRenderCaches || !_cableIndexCache.TryGetValue(grid.Uid, out var cableIndex))
        {
            cableIndex = BuildCableIndex(grid);
            _cableIndexCache[grid.Uid] = cableIndex;
        }

        if (rebuildRenderCaches)
            _dirtyRenderCaches.Remove(grid.Uid);

        _smoothKeysByTile = smoothIndex!;
        _cableKeysByTile = cableIndex;

        // Пол — через чанковый кэш (инстанцирование для GDI+): тысячи одинаковых тайлов
        // выпекаются в битмапы по чанкам один раз на структурное изменение и выводятся
        // на кадр по одному DrawImage на видимый чанк. Пол всегда внизу z-порядка, поэтому
        // выводится ПЕРВЫМ — до декалей/стен/дверей/сущностей, что совпадает с прежним
        // поведением (dd=FloorTiles, самый нижний слой, без индивидуальной сортировки выше декалей).
        DrawStaticFloorChunks(g, tileGrid, grid.Uid, tileSize, viewOffset, gridOffset, visibleRect);

        // Очередь отрисовки собирается и сортируется один раз на структурное изменение
        // (см. GetDrawItems): в кадре остаётся только фильтр по видимой области и сам
        // вывод. Раньше на КАЖДЫЙ кадр шли несколько проходов по всем сущностям карты,
        // замыкания на каждый видимый объект и повторная сортировка.
        foreach (var item in GetDrawItems(grid, tileGrid, rebuildRenderCaches))
        {
            if (item.Kind is DrawItemKind.WallTile or DrawItemKind.DoorTile)
            {
                if (!IsTileVisible((int)item.X, (int)item.Y, visibleRect)) continue;
            }
            else if (item.Kind != DrawItemKind.PipeEndpoints)
            {
                if (!IsPointVisible(item.X, item.Y, visibleRect)) continue;
            }

            switch (item.Kind)
            {
                case DrawItemKind.WallTile:
                {
                    var tile = (TileData)item.Data;
                    DrawWallTile(g, tileGrid, tile.X, tile.Y, tile.ProtoId ?? "WallSolid",
                                 tileSize, viewOffset, gridOffset, opacity);
                    break;
                }
                case DrawItemKind.DoorTile:
                {
                    var tile = (TileData)item.Data;
                    DrawSingleDoor(g, tile.X, tile.Y, tile.ProtoId ?? "Airlock", tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.PipeEndpoints:
                {
                    // Концы атмос-труб при выключенном оверлее труб: напольные детали
                    // над декалями и под коврами (см. GetDrawItems).
                    if (ShowPipeOverlay) break;
                    var pipes = (List<PipeEntity>)item.Data;
                    var visiblePipes = new List<PipeEntity>();
                    foreach (var p in pipes)
                        if (IsPointVisible(p.X, p.Y, visibleRect)) visiblePipes.Add(p);
                    if (visiblePipes.Count > 0)
                        DrawEndpointMarkers(g, visiblePipes, pipes, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Decal:
                    DrawSingleDecal(g, (PlacedDecal)item.Data, tileSize, viewOffset, gridOffset, opacity);
                    break;
                case DrawItemKind.Carpet:
                case DrawItemKind.Entity:
                {
                    var e = (MapEntity)item.Data;
                    DrawSingleEntity(g, e.Proto, e.X, e.Y, e.Rotation, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Firelock:
                    DrawSingleFirelock(g, (FirelockEntity)item.Data, tileSize, viewOffset, gridOffset);
                    break;
                case DrawItemKind.AirAlarm:
                    DrawSingleAlarm(g, (MapEntity)item.Data, tileSize, viewOffset, gridOffset,
                                    "AirAlarm", Color.FromArgb(200, 255, 200, 100));
                    break;
                case DrawItemKind.FireAlarm:
                    DrawSingleAlarm(g, (MapEntity)item.Data, tileSize, viewOffset, gridOffset,
                                    "FireAlarm", Color.FromArgb(200, 255, 100, 100));
                    break;
                case DrawItemKind.Holopad:
                {
                    var h = (HolopadEntity)item.Data;
                    DrawSingleHolopad(g, h.HolopadProto, h.X, h.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Locker:
                {
                    var l = (LockerEntity)item.Data;
                    DrawSingleLocker(g, l.LockerProto, l.X, l.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.SuitStorage:
                {
                    var s = (SuitStorageEntity)item.Data;
                    DrawSingleSuitStorage(g, s.SuitStorageProto, s.X, s.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Bed:
                {
                    var b = (BedEntity)item.Data;
                    DrawSingleBed(g, b.BedProto, b.BedsheetProto, b.X, b.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Camera:
                {
                    var c = (CameraEntity)item.Data;
                    DrawSingleCamera(g, c.CameraProto, c.Rotation, c.X, c.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Dresser:
                {
                    var d = (DresserEntity)item.Data;
                    DrawSingleDresser(g, d.DresserProto, d.X, d.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Intercom:
                {
                    var i = (IntercomEntity)item.Data;
                    DrawSingleIntercom(g, i.IntercomProto, i.Rotation, i.X, i.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
                case DrawItemKind.Fax:
                {
                    var f = (FaxMachineEntity)item.Data;
                    DrawSingleFax(g, f.FaxProto, f.X, f.Y, tileSize, viewOffset, gridOffset);
                    break;
                }
            }
        }

        // Связи беспроводной сети — поверх всего слоя
        DrawEntityNetworkLinks(g, grid, tileSize, viewOffset, gridOffset);
    }

    // ===== Очередь отрисовки (кэш на структурное изменение) =====

    private enum DrawItemKind : byte
    {
        WallTile,
        DoorTile,
        PipeEndpoints,
        Decal,
        Carpet,
        Entity,
        Firelock,
        AirAlarm,
        FireAlarm,
        Holopad,
        Locker,
        SuitStorage,
        Bed,
        Camera,
        Dresser,
        Intercom,
        Fax
    }

    private readonly struct DrawItem
    {
        public readonly DrawItemKind Kind;
        public readonly int DrawDepth;
        public readonly int LayerOrder;
        public readonly float X;
        public readonly float Y;
        // Порядок вставки — финальный ключ сортировки: при равной глубине и равном Y
        // объекты рисуются стабильно (стены → двери → декали → ковры → сущности →
        // «привязки»), а не в том порядке, который случайно выдал бы introsort.
        public readonly int Order;
        public readonly object Data;

        public DrawItem(DrawItemKind kind, int drawDepth, int layerOrder, float x, float y, object data, int order = 0)
        {
            Kind = kind;
            DrawDepth = drawDepth;
            LayerOrder = layerOrder;
            X = x;
            Y = y;
            Data = data;
            Order = order;
        }
    }

    private sealed class DrawQueueCache
    {
        public DrawItem[] Items = Array.Empty<DrawItem>();
        public int EntityCount;
        public int DecalCount;
        public int FloorObjectsOffset;
    }

    private readonly Dictionary<int, DrawQueueCache> _drawQueueCache = new();

    /// <summary>
    /// Очередь отрисовки одного грида: собирается и сортируется один раз на структурное
    /// изменение грида и переиспользуется между кадрами. Порядок вставки и компаратор
    /// те же, что были у прежней очереди из замыканий, поэтому итоговый порядок
    /// отрисовки (и картинка) не меняются — исчезает только работа на каждый кадр.
    /// Счётчики сущностей/декалей и смещение слоя FloorObjects — страховка от правок,
    /// которые почему-то не вызвали InvalidateTileGrid.
    /// </summary>
    private DrawItem[] GetDrawItems(Grid grid, TileGrid tileGrid, bool rebuildRenderCaches)
    {
        int floorObjectsOffset = _drawDepthManager.GetOffset("FloorObjects");

        if (!rebuildRenderCaches &&
            _drawQueueCache.TryGetValue(grid.Uid, out var cached) &&
            cached.EntityCount == grid.Entities.Count &&
            cached.DecalCount == grid.Decals.Count &&
            cached.FloorObjectsOffset == floorObjectsOffset)
        {
            return cached.Items;
        }

        int decalShelfDepth = floorObjectsOffset - 1;
        int carpetShelfDepth = decalShelfDepth + 1;

        var items = new List<DrawItem>(grid.Entities.Count + grid.Decals.Count + 512);

        // Стены и двери — из тайл-сетки (строится генератором комнат).
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            int dd = ResolveDrawDepth(tile.DrawDepthOffset, tile.ProtoId ?? "WallSolid", "Walls");
            items.Add(new DrawItem(DrawItemKind.WallTile, dd, 0, tile.X, tile.Y, tile));
        }

        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            int dd = ResolveDrawDepth(tile.DrawDepthOffset, tile.ProtoId ?? "Airlock", "Doors");
            items.Add(new DrawItem(DrawItemKind.DoorTile, dd, 0, tile.X, tile.Y, tile));
        }

        // Концы атмос-труб (вентиляция и приёмники утилизации) при ВЫКЛЮЧЕННОМ оверлее
        // труб остаются видны, но как напольные детали: вся пачка лежит на полке декалей
        // с максимальным layerOrder — выше всех декалей, но ПОД коврами.
        if (_pipeBuilder != null)
        {
            var layerPipes = _pipeBuilder.GetPipes(grid);
            if (layerPipes.Count > 0)
                items.Add(new DrawItem(DrawItemKind.PipeEndpoints, decalShelfDepth, int.MaxValue,
                                       float.MinValue, float.MinValue, layerPipes));
        }

        // Декали — над тайлами пола и под любыми сущностями.
        foreach (var decal in grid.Decals)
            items.Add(new DrawItem(DrawItemKind.Decal, decalShelfDepth, decal.PatternLayerIndex,
                                   decal.X, decal.Y, decal));

        // Ковры (сущности "Carpet*") — отдельная полка ровно на слой выше декалей.
        foreach (var entity in grid.Entities)
        {
            if (entity.Proto == null ||
                !entity.Proto.StartsWith("Carpet", StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(new DrawItem(DrawItemKind.Carpet, carpetShelfDepth, 0, entity.X, entity.Y, entity));
        }

        foreach (var firelock in grid.Entities.OfType<FirelockEntity>())
        {
            int dd = firelock.DrawDepthOffset != 0 ? firelock.DrawDepthOffset : GetProtoDrawDepth(firelock.Proto);
            items.Add(new DrawItem(DrawItemKind.Firelock, dd, 0, firelock.X, firelock.Y, firelock));
        }

        foreach (var entity in grid.Entities.OfType<AirAlarmEntity>())
        {
            int dd = ResolveDrawDepth(entity.DrawDepthOffset, entity.Proto, "Objects");
            items.Add(new DrawItem(DrawItemKind.AirAlarm, dd, 0, entity.X, entity.Y, entity));
        }

        foreach (var entity in grid.Entities.OfType<FireAlarmEntity>())
        {
            int dd = ResolveDrawDepth(entity.DrawDepthOffset, entity.Proto, "Objects");
            items.Add(new DrawItem(DrawItemKind.FireAlarm, dd, 0, entity.X, entity.Y, entity));
        }

        // Обычные сущности: у труб, огнешлюзов, сигнализаций и «привязок» свои циклы
        // и свои якоря позиционирования (top-left вместо центра тайла).
        foreach (var entity in grid.Entities)
        {
            if (entity is PipeEntity or FirelockEntity or AirAlarmEntity or FireAlarmEntity
                or WireEntity or HolopadEntity or LockerEntity or SuitStorageEntity or BedEntity
                or CameraEntity or DresserEntity or IntercomEntity or FaxMachineEntity) continue;
            if (entity.Proto != null && entity.Proto.StartsWith("Carpet", StringComparison.OrdinalIgnoreCase)) continue;
            int dd = ResolveDrawDepth(entity.DrawDepthOffset, entity.Proto, "Objects");
            items.Add(new DrawItem(DrawItemKind.Entity, dd, 0, entity.X, entity.Y, entity));
        }

        // «Привязки» — отдельный слой над остальными объектами.
        int mountDepth = _drawDepthManager.GetOffset("Objects") + 1;
        foreach (var holopad in grid.Entities.OfType<HolopadEntity>())
            items.Add(new DrawItem(DrawItemKind.Holopad, mountDepth, 0, holopad.X, holopad.Y, holopad));
        foreach (var locker in grid.Entities.OfType<LockerEntity>())
            items.Add(new DrawItem(DrawItemKind.Locker, mountDepth, 0, locker.X, locker.Y, locker));
        foreach (var suit in grid.Entities.OfType<SuitStorageEntity>())
            items.Add(new DrawItem(DrawItemKind.SuitStorage, mountDepth, 0, suit.X, suit.Y, suit));
        foreach (var bed in grid.Entities.OfType<BedEntity>())
            items.Add(new DrawItem(DrawItemKind.Bed, mountDepth, 0, bed.X, bed.Y, bed));
        foreach (var camera in grid.Entities.OfType<CameraEntity>())
            items.Add(new DrawItem(DrawItemKind.Camera, mountDepth, 0, camera.X, camera.Y, camera));
        foreach (var dresser in grid.Entities.OfType<DresserEntity>())
            items.Add(new DrawItem(DrawItemKind.Dresser, mountDepth, 0, dresser.X, dresser.Y, dresser));
        foreach (var intercom in grid.Entities.OfType<IntercomEntity>())
            items.Add(new DrawItem(DrawItemKind.Intercom, mountDepth, 0, intercom.X, intercom.Y, intercom));
        foreach (var fax in grid.Entities.OfType<FaxMachineEntity>())
            items.Add(new DrawItem(DrawItemKind.Fax, mountDepth, 0, fax.X, fax.Y, fax));

        // Порядок вставки — финальный ключ сортировки: при равной глубине и равном Y
        // объекты рисуются в стабильном порядке (стены → двери → декали → ковры →
        // сущности → «привязки»), а не в том, который случайно выдал бы introsort.
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            items[i] = new DrawItem(it.Kind, it.DrawDepth, it.LayerOrder, it.X, it.Y, it.Data, i);
        }

        var array = items.ToArray();
        // Сортировка та же, что раньше: DrawDepth → LayerOrder → Y. Массив
        // сортируется тем же introsort'ом, что и прежний List<(...)>.Sort,
        // поэтому порядок при равных ключах совпадает.
        Array.Sort(array, static (a, b) =>
        {
            int ddComp = a.DrawDepth.CompareTo(b.DrawDepth);
            if (ddComp != 0) return ddComp;
            int layerComp = a.LayerOrder.CompareTo(b.LayerOrder);
            if (layerComp != 0) return layerComp;
            int yComp = ((double)a.Y).CompareTo(b.Y);
            if (yComp != 0) return yComp;
            return a.Order.CompareTo(b.Order);
        });

        _drawQueueCache[grid.Uid] = new DrawQueueCache
        {
            Items = array,
            EntityCount = grid.Entities.Count,
            DecalCount = grid.Decals.Count,
            FloorObjectsOffset = floorObjectsOffset
        };

        return array;
    }

    /// <summary>
    /// Стена-тайл: если прототип (или предок) имеет IconSmooth — рисует её сглаженной
    /// (как и сущности-столы), иначе — обычный тайл. Соседи стены — соседние тайлы
    /// TileContent.Wall в tileGrid (стены в этом редакторе лежат на тайлах, а не как сущности).
    /// </summary>
    private void DrawWallTile(Graphics g, TileGrid tileGrid, int x, int y, string protoId, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (GetProtoIconSmooth(protoId) is { Enabled: true, Base: not null } smoothInfo)
        {
            // Соседи по 8 направлениям ищем в едином индексе (_smoothKeysByTile), куда входят
            // и тайлы-стены (генератор комнат), и сущности (панель репозитория) — поэтому стены
            // из обоих источников сглаживаются друг с другом.
            int mask = SmoothNeighborMask(protoId, x, y);

            int quarter = 0; // стены SnapgridCenter, поворот не применяем
            var rect = ToRect(x, y, tileSize, viewOffset, gridOffset); // стена-тайл рисуется от угла клетки

            // 4 угловых слоя собраны в один битмап — один DrawImage на стену вместо четырёх
            var composite = GetSmoothComposite(protoId, smoothInfo, mask, quarter, tileSize);
            if (composite == null)
            {
                // Ни один state-кадр не загрузился — обычный рендер тайла
                DrawSingleTile(g, x, y, protoId, tileSize, viewOffset, gridOffset, false, opacity);
                return;
            }

            g.DrawImage(composite, rect);
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

        // Быстрый путь: тонирование (ImageAttributes — ~118 мкс на спрайт) и поворот
        // (Transform ровно на 90° — ~1 мс) выпекаются в битмап один раз на пару
        // (прототип, цвет, четверть оборота). На карте сотни и тысячи декалей,
        // поэтому именно это было основной просадкой кадра.
        if (TryDrawDecalBaked(g, decal, rect))
            return;

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
            }, decal.Rotation, string.IsNullOrEmpty(decal.Color) ? null : decal.Color);
        });
    }

    /// <summary>
    /// Рисует декаль заранее запечённым спрайтом (тонирование + поворот на четверть).
    /// false — текстуры нет либо угол произвольный: вызывающий рисует обычным путём
    /// (там же фолбэк-заглушка для декали без текстуры).
    /// </summary>
    private bool TryDrawDecalBaked(Graphics g, PlacedDecal decal, Rectangle rect)
    {
        if (string.IsNullOrEmpty(decal.Proto)) return false;

        var texture = GetOrLoadTexture(decal.Proto);
        if (texture == null) return false;

        var src = GetSourceRect(decal.Proto, texture, 0f);
        if (src.Width == 0 || src.Height == 0) return false;

        string? tintColor = string.IsNullOrEmpty(decal.Color) ? null : decal.Color;
        var baked = GetBakedSprite(decal.Proto, 0, texture, src, rect.Width, rect.Height, decal.Rotation, tintColor);
        if (baked == null) return false;

        DrawBakedSprite(g, baked, rect);
        return true;
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

    // Индекс кабелей: "тайл (x,y) → множество ключей связи" (NodeGroupID кабеля:
    // HVPower/MVPower/Apc/ExCable). Включает И кабели-сущности из панели репозитория,
    // И провода инструмента (WireEntity), поэтому маска соседей считается одинаково
    // и для тех, и для других — провода соединяются с кабелями-сущностями.
    private Dictionary<(int x, int y), HashSet<string>> _cableKeysByTile = new();

    // Кэш индекса кабелей по гриду — пересобирается вместе с индексом сглаживания
    // (на структурное изменение карты), а не на каждый кадр.
    private readonly Dictionary<int, Dictionary<(int x, int y), HashSet<string>>> _cableIndexCache = new();

    // Кэш индекса сглаживания по гриду (gridUid -> индекс). Индекс зависит только от
    // содержимого карты (стены/двери тайлов + сущности), а не от области видимости,
    // поэтому строится ОДИН раз на структурное изменение грида (тот же флаг _dirtyTileGrids,
    // что пересобирает TileGrid) и переиспользуется при панорамировании/выделении/зуме,
    // когда карта не менялась. Раньше BuildIconSmoothIndex фильтровал по visibleRect и
    // перестраивался на каждый кадр — на больших картах это была заметная постоянная нагрузка.
    private readonly Dictionary<int, Dictionary<(int x, int y), HashSet<string>>> _smoothIndexCache = new();

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
        var settings = GetProtoIconSmooth(protoId!);
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
    /// Индекс строится по ВСЕМУ гриду (не фильтруется по области видимости), потому что
    /// он переиспользуется между кадрами при панорамировании — соседи любого края экрана
    /// должны быть в индексе независимо от текущего viewOffset. Возвращает готовый словарь.
    /// </summary>
    private Dictionary<(int x, int y), HashSet<string>> BuildIconSmoothIndex(Grid grid, TileGrid tileGrid)
    {
        var index = new Dictionary<(int, int), HashSet<string>>();

        if (_indexer == null) return index;

        void AddKey(int x, int y, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (index.TryGetValue((x, y), out var set))
                set.Add(key);
            else
                index[(x, y)] = new HashSet<string>(StringComparer.Ordinal) { key };
        }

        // 1) Тайлы-стены (генератор комнат): ключ из прототипа стены на тайле.
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            var keys = GetSmoothKeysForProto(tile.ProtoId);
            if (keys == null) continue;
            foreach (var k in keys) AddKey(tile.X, tile.Y, k);
        }

        // 1b) Тайлы-двери (шлюзы). У шлюза в игре IconSmooth { key: walls, mode: NoSprite }:
        // своей сглаженной иконки он не рисует (base отсутствует — см. DrawWallTile), но для
        // соседних стен он такой же сосед, как стена, поэтому линия стены «слипается» через шлюз.
        // Двери лежат отдельным содержимым тайла (TileContent.Door), а не Wall, и без этого шага
        // стены по бокам от шлюза, поставленного привязкой комнаты, оставались с разрывом.
        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            var keys = GetSmoothKeysForProto(tile.ProtoId);
            if (keys == null) continue;
            foreach (var k in keys) AddKey(tile.X, tile.Y, k);
        }

        // 2) Сущности (панель репозитория).
        foreach (var entity in grid.Entities.OfType<Models.MapEntity>())
        {
            if (string.IsNullOrEmpty(entity.Proto)) continue;
            var keys = GetSmoothKeysForProto(entity.Proto);
            if (keys == null) continue;
            int tx = (int)Math.Floor(entity.X);
            int ty = (int)Math.Floor(entity.Y);
            foreach (var k in keys) AddKey(tx, ty, k);
        }

        return index;
    }

    /// <summary>
    /// Индекс кабелей: для каждого тайла собираем ключи связи (NodeGroupID: HVPower/
    /// MVPower/Apc/ExCable). Источники — И кабели-сущности из репозитория, И провода,
    /// поставленные инструментом «Электросеть» (WireEntity → CableHV/CableMV/
    /// CableApcExtension), чтобы они соединялись друг с другом так же, как в игре.
    /// </summary>
    private Dictionary<(int x, int y), HashSet<string>> BuildCableIndex(Grid grid)
    {
        var index = new Dictionary<(int, int), HashSet<string>>();
        if (_indexer == null) return index;

        void AddKey(int tx, int ty, string key)
        {
            if (index.TryGetValue((tx, ty), out var set))
                set.Add(key);
            else
                index[(tx, ty)] = new HashSet<string>(StringComparer.Ordinal) { key };
        }

        foreach (var entity in grid.Entities.OfType<Models.MapEntity>())
        {
            if (string.IsNullOrEmpty(entity.Proto)) continue;

            var info = GetCableRenderInfo(entity.Proto);
            if (info == null || string.IsNullOrEmpty(info.ConnectivityKey)) continue;

            AddKey((int)Math.Floor(entity.X), (int)Math.Floor(entity.Y), info.ConnectivityKey);
        }

        // Провода инструмента — тот же ключ связи, что у соответствующего прототипа кабеля.
        foreach (var wire in grid.Entities.OfType<WireEntity>())
        {
            string? proto = WireBuilder.GetCableProtoForWireType(wire.WireType);
            var info = proto != null ? GetCableRenderInfo(proto) : null;
            if (info == null || string.IsNullOrEmpty(info.ConnectivityKey)) continue;

            AddKey((int)Math.Floor(wire.X), (int)Math.Floor(wire.Y), info.ConnectivityKey);
        }

        return index;
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

    // 8 результатов MatchingEntity для 8 соседей одним битовым полем (бит i =
    // SmoothNeighborOffsets[i]). Раньше на каждый сглаженный тайл в кадре создавались
    // HashSet и bool[8] — при тысячах стен это заметный мусор в GC.
    private int SmoothNeighborMask(string protoId, int tx, int ty)
    {
        var ours = GetSmoothKeysForProto(protoId);
        if (ours == null) return 0;

        int mask = 0;
        for (int i = 0; i < 8; i++)
        {
            var (dx, dy) = SmoothNeighborOffsets[i];
            if (_smoothKeysByTile.TryGetValue((tx + dx, ty + dy), out var otherKeys) && KeysOverlap(ours, otherKeys))
                mask |= 1 << i;
        }
        return mask;
    }

    // Диагональный режим (mode: Diagonal): true, если все три соседа (E, NE, N) — «свои»
    // (соответствуют smooth-ключу окна). Поворот сущности поворачивает тройку проверяемых
    // соседей (как rotation.RotateVec в игре): quarter 0=юг,1=восток,2=север,3=запад.
    // Индексы базовых соседей из SmoothNeighborOffsets: N=0, NE=1, E=2.
    private static bool ComputeDiagonalState(int mask, int quarter)
    {
        int[] bits;
        switch (((quarter % 4) + 4) % 4)
        {
            case 0: bits = new[] { 2, 1, 0 }; break; // E, NE, N
            case 1: bits = new[] { 3, 2, 1 }; break; // SE, E, NE
            case 2: bits = new[] { 4, 3, 2 }; break; // S, SE, E
            case 3: bits = new[] { 5, 4, 3 }; break; // SW, S, SE
            default: bits = new[] { 2, 1, 0 }; break;
        }
        return (mask & (1 << bits[0])) != 0 &&
               (mask & (1 << bits[1])) != 0 &&
               (mask & (1 << bits[2])) != 0;
    }

    // Сглаженные углы клетки: [NE,NW,SW,SE] в именах функции игры.
    private readonly record struct CornerFills(int NE, int NW, int SW, int SE);

    /// <summary>
    /// Считает CornerFill (0–7) для углов (корнер индексы: 0=SE,1=NE,2=NW,3=SW) по занятости
    /// 8 соседей — точно как CalculateCornerFill в игре (стартовый проход без поворота).
    /// mask — результат SmoothNeighborMask (бит i = i-е смещение из SmoothNeighborOffsets).
    /// </summary>
    private static CornerFills ComputeCornerFills(int mask)
    {
        bool n  = (mask & 1) != 0, ne = (mask & 2) != 0, e  = (mask & 4) != 0, se = (mask & 8) != 0,
             s  = (mask & 16) != 0, sw = (mask & 32) != 0, w  = (mask & 64) != 0, nw = (mask & 128) != 0;

        int cornerNE = 0, cornerNW = 0, cornerSW = 0, cornerSE = 0;
        if (n)  { cornerNE |= 1; cornerNW |= 4; }
        if (ne) { cornerNE |= 2; }
        if (e)  { cornerNE |= 4; cornerSE |= 1; }
        if (se) { cornerSE |= 2; }
        if (s)  { cornerSE |= 4; cornerSW |= 1; }
        if (sw) { cornerSW |= 2; }
        if (w)  { cornerSW |= 4; cornerNW |= 1; }
        if (nw) { cornerNW |= 2; }
        return new CornerFills(cornerNE, cornerNW, cornerSW, cornerSE);
    }

    /// <summary>
    /// По направлению объекта возвращает, какое значение CornerFill (0–7) из [NE,NW,SW,SE]
    /// должен получить КАЖДЫЙ из 4 угловых ЛОКАЛЬНЫХ слоёв (0=SE,1=NE,2=NW,3=SW).
    /// Точное соответствие возврату CalculateCornerFill + LayerSetRsiState в игре (слой SE всегда
    /// получает cornerSE и т.д., поэтому даже у южной ориентации нужна перестановка из массива).
    /// quarter: 0=South,1=East,2=North,3=West (см. GetQuarter).
    /// Возвращает кортеж значений для слоёв 0..3 (он же — через LayerFillAt).
    /// </summary>
    private static (int a, int b, int c, int d) RotateLayerFills(CornerFills f, int quarter)
    {
        int NE = f.NE, NW = f.NW, SW = f.SW, SE = f.SE;
        return quarter switch
        {
            1 => (NE, NW, SW, SE), // East: локальные NE,NW,SW,SE = мировые SE,NE,NW,SW
            2 => (NW, SW, SE, NE), // North: локальные = SW,SE,NE,NW
            3 => (SW, SE, NE, NW), // West: локальные = SE,NE,NW,SW
            _ => (SE, NE, NW, SW)  // South: локальные = NE,NW,SW,SE
        };
    }

    /// <summary>Значение CornerFill для углового слоя index (0=SE,1=NE,2=NW,3=SW).</summary>
    private static int LayerFillAt((int a, int b, int c, int d) fills, int index) => index switch
    {
        0 => fills.a,
        1 => fills.b,
        2 => fills.c,
        _ => fills.d
    };

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
        var cacheKey = (protoId, state);
        string stateKey = $"{protoId}||{settings.Base}{state}";
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
                    _protoTextureDirCache[stateKey] = Path.GetDirectoryName(path) ?? "";
                    _protoStateNameCache[stateKey] = stateName;
                }
                catch { img = null; }
            }
        }
        _iconSmoothStateCache[cacheKey] = img;
        // Ключ для GetSourceRect (по нему ищется meta.json каталога и имя стейта)
        // строится один раз на (прототип, стейт), а не на каждый тайл каждый кадр.
        _iconSmoothStateKeys[cacheKey] = stateKey;
        return img;
    }

    /// <summary>
    /// State-кадр сглаживания вместе со строковым ключом для GetSourceRect. Ключ-кортеж
    /// в кэше — чтобы не склеивать строку на каждый из 4 углов каждого тайла в кадре.
    /// </summary>
    private (Image? img, string key) GetSmoothState(string protoId, IconSmoothSettings settings, int state)
    {
        var cacheKey = (protoId, state);
        if (!_iconSmoothStateCache.TryGetValue(cacheKey, out var img))
            img = GetOrLoadIconSmoothState(protoId, settings, state);

        _iconSmoothStateKeys.TryGetValue(cacheKey, out var key);
        return (img, key ?? (protoId + "||" + settings.Base + state));
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

        int quarter = GetQuarter(rotation);
        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);
        int mask = SmoothNeighborMask(protoId, tx, ty);

        // Диагональный режим (mode: Diagonal — диагональные окна): как CalculateNewSpriteDiagonal
        // в игре. Смотрит три соседа (E, NE, N), повёрнутых на поворот сущности; если все три
        // совпадают с нашим smooth-ключом — стейт "{base}1", иначе "{base}0". Это состояние
        // СВЯЗНОСТИ, а не направление кадра: state1 = окно встало вплотную к соседним стенам,
        // state0 = изолированное. Поворот сущности влияет на то, какие три соседа проверяются,
        // поэтому фактический поворот окна здесь учитывается (а не игнорируется, как раньше).
        if (settings.Mode.Equals("Diagonal", StringComparison.OrdinalIgnoreCase))
        {
            int state = ComputeDiagonalState(mask, quarter) ? 1 : 0;
            var (img, key) = GetSmoothState(protoId, settings, state);
            if (img == null || key == null) return;

            // У diagonal-режима стейт ("state0"/"state1") — это состояние СВЯЗНОСТИ
            // (окно вплотную к стенам или изолировано), а НЕ напрвление кадра: движок
            // (DiagonalSpriteSmoothing) выдаёт только base+0/base+1, и в meta.json у этих
            // стейтов нет полей directions — кадр один. Ориентация диагональной стены/окна
            // задаётся поворотом САМОЙ сущности (колёсиком), который движок применяет
            // визуально к спрайту. Раньше здесь рисовался голый DrawImage без поворота,
            // поэтому вращение колёсиком никак не меняло вид (в программе одно направление).
            var src = GetSourceRect(key, img, 0f);
            if (src.Width == 0 || src.Height == 0) return;

            if (rotation != 0f)
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                WithRotation(g, cx, cy, rotation, () => g.DrawImage(img, rect, src, GraphicsUnit.Pixel));
            }
            else
            {
                g.DrawImage(img, rect, src, GraphicsUnit.Pixel);
            }
            return;
        }

        // 4 угловых слоя собраны в один битмап — один DrawImage вместо четырёх.
        // Если ни один state-кадр не загрузился (нет RSI-папки/кадров у прототипа) —
        // не рисуем пустую клетку, как и раньше.
        var composite = GetSmoothComposite(protoId, settings, mask, quarter, tileSize);
        if (composite == null) return;

        g.DrawImage(composite, rect);
    }

    private void DrawSingleEntity(Graphics g, string protoId, float worldX, float worldY, float rotation, int tileSize, PointF viewOffset, PointF gridOffset, bool cableSmoothing = true)
    {
        // Кабели (CableHV/CableMV/CableApcExtension и их наследники) рисуются НЕ
        // статическим стейтом *_0, а по маске соседей — как CableVisualizer в игре.
        // Сюда же приходят и провода инструмента «Электросеть» (см. DrawWireCableSprites).
        if (cableSmoothing && _indexer != null && GetCableRenderInfo(protoId) is { } cableInfo)
        {
            if (DrawSingleCableEntity(g, protoId, cableInfo, worldX, worldY, tileSize, viewOffset, gridOffset))
                return;
            // Стейт не загрузился — ниже нарисуем обычный статический спрайт.
        }

        var rect = ToRect(worldX, worldY, tileSize, viewOffset, gridOffset, -0.5f, -0.5f);

        // IconSmooth: если у прототипа (или его предков) есть компонент IconSmooth —
        // рисуем сглаженным способом (4 угловых слоя), а не обычной текстурой.
        if (GetProtoIconSmooth(protoId) is { } smoothInfo)
        {
            if (smoothInfo.Enabled && smoothInfo.Base != null)
            {
                DrawSingleSmoothEntity(g, protoId, worldX, worldY, rotation, tileSize, viewOffset, gridOffset, smoothInfo);
                return;
            }
        }

        // Структуры без направленных кадров всегда смотрят на юг (оба признака — из
        // кэша метаданных, раньше здесь на каждую сущность читался meta.json с диска)
        rotation = NormalizeStructureRotation(protoId, rotation);

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
            var srcRect = GetSourceRect(protoId, texture);
            // Поворот кратен 90° — выводим заранее повёрнутый кадр (Transform ровно
            // на 90° в GDI+ стоит ~1 мс на объект против ~3 мкс у обычного DrawImage).
            if (rotation != 0f && TryDrawBaked(g, protoId, 0, texture, rect, srcRect, rotation, null, preserveAspect: false))
                return;
            WithRotation(g, cx, cy, rotation, () =>
            {
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
        // на каждый кадр рендера. Ключ кэша теперь кортеж (спрайт, четверть оборота):
        // строка не собирается вовсе.
        int quarter = directions >= 4 ? GetQuarterTurnOrRound(rotation) : 0;

        if (_sourceRectCache.TryGetValue((protoId, quarter), out var cached))
            return cached;

        var frameSize = GetRsiFrameSize(protoId, img);

        int col = 0, row = 0;
        if (directions >= 4)
        {
            // Порядок направлений, зашитый в сам движок Robust Toolbox: 0=юг, 1=север,
            // 2=восток, 3=запад (это порядок, в котором кадры направлений идут ПОДРЯД
            // в общей последовательности кадров стейта — а не "направление = своя строка").
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
        _sourceRectCache[(protoId, quarter)] = rect;
        return rect;
    }

    /// <summary>
    /// Четверть оборота (0–3) без требования кратности 90°: для кадров с directions>=4
    /// берётся ближайшая сторона, как и раньше (Math.Round угла / 90°).
    /// </summary>
    private static int GetQuarterTurnOrRound(float rotation)
    {
        float norm = rotation % (float)(2 * Math.PI);
        if (norm < 0) norm += (float)(2 * Math.PI);
        return (int)Math.Round(norm / (Math.PI / 2)) % 4;
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

    // === Метаданные прототипа (обход цепочки parent: один раз на protoId) ===
    // Всё, что ниже, раньше резолвилось на КАЖДЫЙ объект в КАЖДОМ кадре: слои
    // (GetLayers возвращал новый List даже когда слоёв нет), offset и IsStructure
    // (рекурсивный обход родителей), directions (GetStateDirections индексатора
    // читал meta.json с диска), drawdepth. На карте это тысячи вызовов на кадр.
    private ProtoRenderMeta GetProtoMeta(string protoId)
    {
        if (string.IsNullOrEmpty(protoId)) return new ProtoRenderMeta();

        if (_protoMetaCache.TryGetValue(protoId, out var meta)) return meta;

        meta = new ProtoRenderMeta();
        if (_indexer != null)
        {
            try
            {
                meta.IconSmooth = _indexer.GetIconSmoothInfo(protoId);

                var layers = _indexer.GetLayers(protoId);
                meta.Layers = layers.Count > 0 ? layers : null;

                meta.SpriteOffset = _indexer.GetSpriteOffset(protoId);

                var proto = _indexer.FindPrototype(protoId);
                meta.IsStructure = proto?.IsStructure == true;
                meta.StorageVisuals = _indexer.GetEntityStorageVisuals(protoId);

                // Перекрытие для обзора камер (Occluder) — тоже по цепочке parent:
                // раньше это проверялось для каждой сущности карты на каждый кадр.
                meta.IsOccluder = _indexer.HasComponent(protoId, "Occluder");

                // snapCardinals: спрайт не вращается при повороте (гироскоп и т.п.)
                meta.SnapCardinals = _indexer.GetSnapCardinals(protoId);

                // directions у индексатора — самый дорогой вызов (File.ReadAllText
                // meta.json + разбор JSON), поэтому и кэшируется здесь
                meta.StateDirections = _indexer.GetStateDirections(protoId);

                var ddName = _indexer.GetDrawDepth(protoId);
                if (!string.IsNullOrEmpty(ddName))
                    meta.DrawDepthOffset = _drawDepthManager.GetOffset(ddName);
            }
            catch
            {
                // Некорректный прототип/битый meta.json — работаем с пустыми значениями
            }
        }

        _protoMetaCache[protoId] = meta;
        return meta;
    }

    /// <summary>Слои спрайта (или null, если прототип однослойный) — из кэша метаданных.</summary>
    private List<Models.SpriteLayer>? GetProtoLayers(string protoId)
        => string.IsNullOrEmpty(protoId) ? null : GetProtoMeta(protoId).Layers;

    /// <summary>Смещение компонента Sprite (без поворота) — из кэша метаданных.</summary>
    private (float x, float y) GetProtoSpriteOffset(string protoId)
        => string.IsNullOrEmpty(protoId) ? (0f, 0f) : GetProtoMeta(protoId).SpriteOffset;

    /// <summary>IconSmooth-компонент прототипа (или null) — из кэша метаданных.</summary>
    private IconSmoothSettings? GetProtoIconSmooth(string protoId)
        => string.IsNullOrEmpty(protoId) ? null : GetProtoMeta(protoId).IconSmooth;

    /// <summary>DrawDepth прототипа в виде готового offset'а; 0 — слой не задан.</summary>
    private int GetProtoDrawDepth(string? protoId)
        => string.IsNullOrEmpty(protoId) ? 0 : GetProtoMeta(protoId).DrawDepthOffset;

    /// <summary>
    /// Итоговый DrawDepthOffset объекта: явно заданный у объекта → слой прототипа из
    /// YAML → слой по умолчанию. Слой прототипа берётся из кэша метаданных (раньше
    /// на каждый объект в кадре рекурсивно обходилась цепочка parent:).
    /// </summary>
    private int ResolveDrawDepth(int explicitOffset, string? protoId, string fallbackLayer)
    {
        if (explicitOffset != 0) return explicitOffset;
        int dd = GetProtoDrawDepth(protoId);
        return dd != 0 ? dd : _drawDepthManager.GetOffset(fallbackLayer);
    }

    /// <summary>
    /// Поворот структуры обнуляется, если у её иконки нет направленных кадров
    /// (directions &lt; 4): обычные структуры в игре всегда смотрят на юг. Оба признака
    /// берутся из кэша метаданных — раньше здесь на каждую сущность в кадре читался
    /// meta.json. Исключение — створки и взрывные двери (Shutters*/BlastDoor*): они
    /// ставятся инструментом «Заменить на окно» как свободные объекты и вращаются
    /// колёсиком независимо от стен (прямо как камера), поэтому их поворот сохраняется,
    /// даже несмотря на directions &lt; 4.
    /// </summary>
    private float NormalizeStructureRotation(string? protoId, float rotation)
    {
        if (string.IsNullOrEmpty(protoId)) return rotation;
        if (IsFreeRotatableShutterBlast(protoId!)) return rotation;
        var meta = GetProtoMeta(protoId!);
        // snapCardinals: true — спрайт симметричный, вращение не отображается (гироскоп)
        if (meta.SnapCardinals) return 0f;
        // Диагональные стены/окна (IconSmooth mode: Diagonal): у них directions у
        // state0/state1 нет (==1), но они РАЗВОРАЧИВАЮТСЯ поворотом сущности колёсиком —
        // спрайт остаётся тем же, просто аффинно поворачивается. Поэтому для них поворот
        // не обнуляем, как и для створок/взрывных дверей ниже. Проверяем и наличие
        // диагонального IconSmooth, и само имя прототипа (все диагональные стены/окна
        // содержат "Diagonal" в id) — на случай, если компонент не отресолвился.
        bool isDiagonalSmooth = meta.IconSmooth != null &&
            meta.IconSmooth.Mode.Equals("Diagonal", StringComparison.OrdinalIgnoreCase)
            || protoId!.Contains("Diagonal", StringComparison.OrdinalIgnoreCase);
        if (isDiagonalSmooth) return rotation;
        return meta.IsStructure && meta.StateDirections < 4 ? 0f : rotation;
    }

    /// <summary>
    /// Створка (Shutters) или взрывная дверь (BlastDoor): свободно ставящаяся структура,
    /// которая вращается колёсиком независимо от стен (см. MainForm.IsFreePlaceableShutterBlast).
    /// </summary>
    private static bool IsFreeRotatableShutterBlast(string id) =>
        id.StartsWith("Shutters", StringComparison.Ordinal) ||
        id.StartsWith("BlastDoor", StringComparison.Ordinal);

    // Кэш ImageAttributes по цвету декали — пересоздавать ColorMatrix на каждый DrawImage
    // накладно, а цветов у декалей на карте обычно немного (одни и те же несколько цветов
    // из палитры повторяются на десятках декалей)
    private readonly Dictionary<string, ImageAttributes> _decalTintCache = new();

    /// <summary>
    /// Парсит цвет декали в формате "#RRGGBBAA" (как хранится в PlacedDecal.Color
    /// и экспортируется в DecalGrid). При ошибке — непрозрачный белый (нет тонирования).
    /// </summary>
    private static Color ParseDecalColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Color.White;
        var v = value.Trim();

        if (v.StartsWith('#'))
        {
            try
            {
                var h = v.TrimStart('#');
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

        // Имя цвета как в YAML Sprite.color ("Yellow", "Green", ...). Color.FromName
        // понимает стандартные имена без учёта регистра; для неизвестного имени
        // возвращает A=0 — это и есть признак «не распознали» (не тонируем).
        try
        {
            var named = Color.FromName(v);
            if (named.A != 0) return named;
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
        _smoothIndexCache.Clear();
        _cableIndexCache.Clear();
        _cableRenderInfoCache.Clear();
        ClearFloorChunks();

        // Бейки спрайтов и метаданные прототипов тоже завязаны на индекс репозитория:
        // после его перезагрузки и текстуры, и слои/offset/drawdepth другие.
        ClearBakedSprites();
        _protoMetaCache.Clear();
        _iconSmoothStateCache.Clear();
        _iconSmoothStateKeys.Clear();
        _layerInfoCache.Clear();
        ClearLayeredComposites();
        ClearSmoothComposites();
        _cameraCoverageCache.Clear();
        _dirtyCameraCoverage.Clear();

        foreach (var kvp in _decalTintCache)
            kvp.Value.Dispose();
        _decalTintCache.Clear();
    }

    private void ClearFloorChunks()
    {
        foreach (var kvp in _floorChunkCache)
            kvp.Value.Dispose();
        _floorChunkCache.Clear();
        _floorChunkLru.Clear();
        _emptyFloorChunks.Clear();
    }

    // Выкидывает чанки пола конкретного грида (при структурном изменении карты).
    private void ClearGridFloorChunks(int gridUid)
    {
        bool any = false;
        var keys = _floorChunkCache.Keys.Where(k => k.gridUid == gridUid).ToList();
        foreach (var k in keys)
        {
            if (_floorChunkCache.TryGetValue(k, out var bmp))
                bmp.Dispose();
            _floorChunkCache.Remove(k);
            any = true;
        }
        any |= _emptyFloorChunks.RemoveWhere(k => k.gridUid == gridUid) > 0;
        if (any) RebuildFloorChunkLru();
    }

    private void RebuildFloorChunkLru()
    {
        _floorChunkLru.Clear();
        foreach (var k in _floorChunkCache.Keys)
            _floorChunkLru.AddLast(k);
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
            // Подпись размера ставим в тайл, ближайший к левому-верхнему углу
            // ограничивающего прямоугольника и при этом находящийся ВНУТРИ
            // периметра комнаты (Contains учитывает RemovedCells) — иначе у
            // нерямоугольных комнат цифры оказывались бы вне стен, поверх пустоты.
            int? labelX = null, labelY = null;
            int bestDist = int.MaxValue;
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (!room.Contains(x, y)) continue;
                    int dist = (x - room.X) + (y - room.Y);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        labelX = x;
                        labelY = y;
                    }
                }
            }

            if (labelX.HasValue && labelY.HasValue)
            {
                float startX = (labelX.Value + gridOffset.X) * tileSize - viewOffset.X + tileSize / 2f;
                float startY = (labelY.Value + gridOffset.Y) * tileSize - viewOffset.Y + tileSize / 2f;

                using var font = new Font("Arial", Math.Min(10, tileSize / 3));
                Color textColor = GetContrastColor(room.FillColor);
                int alpha = (int)(200 * opacity);
                using var brush = new SolidBrush(Color.FromArgb(alpha, textColor));

                int innerWidth = Math.Max(0, room.Width - 2);
                int innerHeight = Math.Max(0, room.Height - 2);
                g.DrawString($"{innerWidth}×{innerHeight}", font, brush, startX + 2, startY + 2);
            }
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

            // Связи пожарных сигнализаций — тёмно-оранжевые, воздушных остаются зелёными
            // по умолчанию (connection.LineColor).
            Color lineColor = connection.Source is FireAlarmDevice
                ? Color.FromArgb(255, 200, 90, 0)
                : connection.LineColor;

            using (var pen = new Pen(lineColor, connection.LineWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, sx, sy, tx, ty);
            }

            float dotSize = 4;
            using (var brush = new SolidBrush(lineColor))
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
        _dirtyRenderCaches.Add(gridUid);
        _dirtyCameraCoverage.Add(gridUid);
    }

    /// <summary>
    /// Помечает только кэши РЕНДЕРА грида грязными, не трогая TileGrid: для операций,
    /// которые меняют набор/позиции/типы декалей (пересчёт узора декальрула), но не
    /// тайлы-стены/пол/двери. Очередь отрисовки (GetDrawItems) кэшируется по числу
    /// сущностей/декалей, и при замене одних декалей другими их число может не
    /// измениться — без инвалидации рендер показывал бы старый узор, пока слой не
    /// переключили. Заодно сбрасываем чанки пола, которые тоже лежат в кэше рендера.
    /// </summary>
    public void InvalidateRenderCache(int gridUid)
    {
        _dirtyRenderCaches.Add(gridUid);
        ClearGridFloorChunks(gridUid);
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

        // Красная рамка тайла — только когда ставить нельзя (нет комнаты/прототипа для
        // этой комнаты или клетка уже занята): тогда спрайт не рисуется вовсе и видно,
        // почему не ставится. У валидного превью тайл ничем не закрашивается — под
        // спрайтом не должно быть плашки.
        if (!_previewEntityValid)
        {
            using (var fillBrush = new SolidBrush(Color.FromArgb(80, 220, 60, 60)))
                g.FillRectangle(fillBrush, rect);
            using (var framePen = new Pen(Color.FromArgb(230, 200, 40, 40), 2f))
                g.DrawRectangle(framePen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        if (!_previewEntityValid || string.IsNullOrEmpty(_previewEntityProto)) return;



        // Структуры (BaseStructure) всегда смотрят на юг, независимо от rotation,
        // НО если у иконки в meta.json directions: 4 —尊重 rotation (направленная иконка)
        float previewRotation = NormalizeStructureRotation(_previewEntityProto, _previewEntityRotation);

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
            float rotation = NormalizeStructureRotation(protoId, entity.Rotation);

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

        Color fillColor = _deleteAreaDividerStyle
            ? Color.FromArgb(50, 70, 210, 90)
            : Color.FromArgb(50, 230, 70, 70);
        using var fillBrush = new SolidBrush(fillColor);
        g.FillRectangle(fillBrush, left, top, width, height);

        Color penColor = _deleteAreaDividerStyle
            ? Color.FromArgb(255, 60, 200, 90)
            : Color.FromArgb(255, 230, 60, 60);
        using var pen = new Pen(penColor, 2f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, left, top, width, height);
    }


    /// <summary>
    /// Рисует накопленные области разделения инструмента «Разделитель гридов»
    /// активного слоя. Хранятся конкретные клетки: заливка рисуется объединёнными
    /// горизонтальными отрезками (быстро даже на больших областях), а граница —
    /// единым контуром по внешним рёбрам клеток: соседние рёбра сливаются в
    /// отрезки, внутренние границы между выделенными клетками не рисуются.
    /// Отрисовывается постоянно, пока область сохранена в активном гриде.
    /// </summary>
    private void DrawDividerAreasOverlay(Graphics g, float scale, PointF viewOffset)
    {
        if (_currentMap?.ActiveGrid == null) return;
        var grid = _currentMap.ActiveGrid;
        if (grid.DividerAreas == null || grid.DividerAreas.Count == 0) return;
        if (!grid.DividerAreas.Any(a => !a.IsEmpty)) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        if (tileSize <= 0) return;
        int activeIndex = _currentMap.Grids.IndexOf(grid);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = grid.Position.X * tileSize;
        float gridOffsetY = (grid.Position.Y + layerOffsetY) * tileSize;

        using var fill = new SolidBrush(Color.FromArgb(45, 70, 210, 90));
        using var edgePen = new Pen(Color.FromArgb(220, 60, 200, 90), 2f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
            StartCap = System.Drawing.Drawing2D.LineCap.Flat,
            EndCap = System.Drawing.Drawing2D.LineCap.Flat
        };

        float ScreenX(int x) => x * tileSize + gridOffsetX - viewOffset.X;
        float ScreenY(int y) => y * tileSize + gridOffsetY - viewOffset.Y;

        // Заливка: соседние клетки строки сливаются в один прямоугольник.
        void FillArea(GridDividerArea area)
        {
            foreach (var row in area.Cells.GroupBy(c => c.Y))
            {
                var xs = row.Select(c => c.X).OrderBy(x => x).ToList();
                if (xs.Count == 0) continue;

                int runStart = xs[0];
                int prev = xs[0];
                for (int i = 1; i <= xs.Count; i++)
                {
                    int x = i < xs.Count ? xs[i] : int.MinValue;
                    if (i < xs.Count && x == prev + 1)
                    {
                        prev = x;
                        continue;
                    }

                    g.FillRectangle(fill, ScreenX(runStart), ScreenY(row.Key),
                        (prev - runStart + 1) * tileSize, tileSize);

                    if (i < xs.Count)
                    {
                        runStart = x;
                        prev = x;
                    }
                }
            }
        }

        // Граница: собираем внешние рёбра клеток (соседняя клетка не выделена),
        // затем сливаем коллинеарные соседние рёбра в один отрезок.
        void StrokeArea(GridDividerArea area)
        {
            var horizontal = new Dictionary<int, List<int>>();
            var vertical = new Dictionary<int, List<int>>();

            void AddEdge(Dictionary<int, List<int>> map, int line, int pos)
            {
                if (!map.TryGetValue(line, out var list))
                {
                    list = new List<int>();
                    map[line] = list;
                }
                list.Add(pos);
            }

            foreach (var (x, y) in area.Cells)
            {
                if (!area.Contains(x, y - 1)) AddEdge(horizontal, y, x);
                if (!area.Contains(x, y + 1)) AddEdge(horizontal, y + 1, x);
                if (!area.Contains(x - 1, y)) AddEdge(vertical, x, y);
                if (!area.Contains(x + 1, y)) AddEdge(vertical, x + 1, y);
            }

            foreach (var (line, positions) in horizontal)
            {
                positions.Sort();
                int runStart = positions[0];
                int prev = positions[0];
                for (int i = 1; i <= positions.Count; i++)
                {
                    int pos = i < positions.Count ? positions[i] : int.MinValue;
                    if (i < positions.Count && pos == prev + 1)
                    {
                        prev = pos;
                        continue;
                    }

                    float y = ScreenY(line);
                    g.DrawLine(edgePen, ScreenX(runStart), y, ScreenX(prev + 1), y);

                    if (i < positions.Count)
                    {
                        runStart = pos;
                        prev = pos;
                    }
                }
            }

            foreach (var (line, positions) in vertical)
            {
                positions.Sort();
                int runStart = positions[0];
                int prev = positions[0];
                for (int i = 1; i <= positions.Count; i++)
                {
                    int pos = i < positions.Count ? positions[i] : int.MinValue;
                    if (i < positions.Count && pos == prev + 1)
                    {
                        prev = pos;
                        continue;
                    }

                    float x = ScreenX(line);
                    g.DrawLine(edgePen, x, ScreenY(runStart), x, ScreenY(prev + 1));

                    if (i < positions.Count)
                    {
                        runStart = pos;
                        prev = pos;
                    }
                }
            }
        }

        foreach (var area in grid.DividerAreas)
        {
            if (area.IsEmpty) continue;
            FillArea(area);
            StrokeArea(area);
        }
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






