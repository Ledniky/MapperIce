// Forms/MainForm.cs

using MapperIce.Models;
using MapperIce.Services;
using System.Text.Json;

namespace MapperIce.Forms;

public partial class MainForm : Form
{
    private MapData _map = new();
    private Room? _currentRoom = null;
    private Renderer _renderer;
    private ToolManager _toolManager = new();
    private UndoManager _undo = new();
    private RepositoryManager _repoManager = new();
    private PrototypeIndexer _indexer = new();
    private RoomTypeManager _roomTypeManager = new();
    private DoorUpdater _doorUpdater = null!;
    private TileBuilder _tileBuilder = null!;
    private TileGrid _tileGrid = new();
    private PipeBuilder _pipeBuilder = null!;
    private PipeTypeManager _pipeTypeManager = new();
    private WireBuilder _wireBuilder = null!;
    private WireTypeManager _wireTypeManager = new();
    private AlarmNetwork? _cachedAlarmNetwork;
    private DecalPackManager _decalPackManager = new();
    private DecalInheritanceManager _decalInheritanceManager = new();
    private DecalPatternBuilder _decalPatternBuilder;
    private Button? _btnDecalInheritance;
    private Form? _decalInheritanceForm = null;
    private Point _startPoint;
    private bool _isDrawing = false;

    private PointF _viewOffset = new PointF(0, 0);
    private PointF _panStart;
    private bool _isPanning = false;
    private float _scale = 1.0f;

    private PictureBox _canvas = null!;
    // Последний отрисованный кадр: пока окошко лупы просто двигается, карта не меняется,
    // поэтому OnPaint рисует этот кадр, а не гоняет полный рендер карты на каждую перерисовку
    private Bitmap? _lastFrame;
    private bool _reuseLastFrame = false;
    private Panel _toolPanel = null!;
    private Panel _repoPanel = null!;
    private ComboBox _roomTypeCombo = null!;
    private Button _btnCreateRoom = null!;
    private Button _btnDelete = null!;
    private Button _btnRoomSettings = null!;
    // Путь к проекту, с которым работаем в этом сеансе: последний открытый .ice
    // или последний путь «Сохранить как». Используется Ctrl+S для быстрого
    // сохранения в тот же файл. null — .ice ещё ни разу не открывался/не сохранялся.
    private string? _currentProjectPath;
    // Путь к открытому/сохранённому в этом сеансе YAML (если источником карты был
    // .yml/.yaml). Если задан — Ctrl+S экспортирует в него же, а не в .ice.
    private string? _currentYamlPath;
    private ComboBox _doorToolCombo = null!;
    private ComboBox _windowToolCombo = null!;
    // Ключ — подпись пункта в списке, значение — инструмент (как _utilToolMap)
    private readonly Dictionary<string, ToolManager.Tool> _doorToolMap = new()
    {
        { "Дверь", ToolManager.Tool.Door },
        { "Дверь (стекло)", ToolManager.Tool.DoorGlass },
        { "Проход", ToolManager.Tool.Passage },
        { "Восстановить стену", ToolManager.Tool.RestoreWall }
    };
    // Для пунктов с реальным прототипом (двери) — Id для GetCachedProtoIcon.
    // ProtoId == null -> у пункта нет спрайта, всегда рисуется Fallback-эмодзи
    // (так было и раньше: Проход/Восстановить стену — просто глифы на кнопках)
    private readonly Dictionary<string, (string? ProtoId, string Fallback)> _doorToolIcons = new()
    {
        { "Дверь", ("Airlock", "🚪") },
        { "Дверь (стекло)", ("AirlockGlass", "🔲") },
        { "Проход", (null, "▢") },
        { "Восстановить стену", (null, "🧱") },
        { "Заменить на окно", (null, "🪟") }
    };
    // Готовые заранее (синхронно) иконки для _doorToolCombo — в отличие от
    // GetCachedProtoIcon, без асинхронной догрузки, чтобы не ждать повторного
    // открытия/наведения на список для появления текстур
    private Image? _doorIconAirlock;
    private Image? _doorIconAirlockGlass;
    // Кэш иконок для _powerJunctionCombo — грузится синхронно в
    // LoadWireJunctionIcons сразу после заполнения списков, как и door-иконки,
    // чтобы не зависеть от ленивой фоновой подгрузки и лишнего Invalidate
    private readonly Dictionary<string, Image?> _wireJunctionIconCache = new();
    // Кэш иконок для пунктов-окон в _doorToolCombo (ключ — текст пункта,
    // напр. "🪟 Reinforced") — грузится синхронно в UpdateWindowCombo,
    // как и остальные комбо-иконки, чтобы не зависеть от ленивой подгрузки
    private readonly Dictionary<string, Image?> _windowIconCache = new();
    // Готовые иконки для _wireTypeCombo — CableHV, CableMV, CableApcExtension
    private Image? _wireIconHV;
    private Image? _wireIconMV;
    private Image? _wireIconLV;
    private Button _btnSubtractRoom = null!;
    private Button _btnRestoreRoom = null!;
    private Button _btnAddAll = null!;
    private Room? _restoreTargetRoom = null;
    private Button _btnPipeDistra = null!;
    private Button _btnPipeWaste = null!;
    private Button _btnPipeNormal = null!;
    private Button _btnPipeUtil = null!;
    private ComboBox _utilToolCombo = null!;
    // Ключ — подпись пункта в выпадающем списке, значение — инструмент,
    // который он активирует (единая точка соответствия строка↔инструмент,
    // используется и при заполнении списка, и при обработке выбора)
    private readonly Dictionary<string, ToolManager.Tool> _utilToolMap = new()
    {
        { "🔧 Направление", ToolManager.Tool.PipeUtilSettings },
        { "🔧🔧 Фильтр", ToolManager.Tool.UtilWrenches },
        { "⚙ Настройки", ToolManager.Tool.UtilNodeLabel }
    };
    private ComboBox _wireTypeCombo = null!;
    private Button? _btnWireSettings;
    private Form? _wireSettingsForm = null;
    private ComboBox _powerJunctionCombo = null!;
    private ComboBox _repoSelector = null!;
    private Button _btnAddRepo = null!;
    private Button _btnRemoveRepo = null!;
    private Button _btnIndexRepo = null!;
    private ListBox _protoList = null!;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Image?> _protoIconCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _iconLoadInFlight = new();
    private readonly SemaphoreSlim _iconLoadSemaphore = new SemaphoreSlim(4);
    private volatile bool _pendingIconRedraw = false;
    private System.Windows.Forms.Timer? _iconRedrawTimer; private TextBox _searchBox = null!;
    private ComboBox _filterCombo = null!;
    private string _currentFilter = "all";
    private ToolTip _roomTypeTooltip = null!;

    private Form? _roomTypeForm = null;
    private Form? _filterLabelForm = null;
    private CancellationTokenSource? _searchCts;
    private bool _hideRoomOverlay = false;
    private bool _showPipeOverlay = true;
    private bool _showWires = true;
    private bool _showCameraAreas = true;
    private bool _showNetworkLinks = false;
    // Рабочие настройки слоёв труб — копии встроенных (PipeSettings.DefaultLayers),
    // а не те же самые объекты: иначе сброс слоя и «Отмена» присваивали значения
    // объекту самому себе и ничего не меняли
    private Dictionary<string, PipeSettings> _pipeLayers = PipeSettings.CreateDefaultLayers();
    private Form? _pipeSettingsForm = null;
    private Form? _utilSettingsForm = null;
    private Button _btnPipeSettings = null!;
    private bool _snapToGrid = true;
    private Dictionary<string, AlarmSettings> _alarmSettings = new(AlarmSettings.DefaultAlarms);
    private Form? _alarmSettingsForm = null;
    private Button _btnAlarmSettings = null!;
    private string _currentPipeLayer = "Distra";
    private string _currentWireLayer = "HV";
    private string? _selectedPowerJunctionProto = null;
    private string? _selectedPowerJunctionType = null; // "APC" или "Substation"
    private string? _selectedWindowProto = null;
    private Button _btnAirAlarm = null!;
    private Button _btnFireAlarm = null!;
    private Button _btnPlaceHolopad = null!;
    private Image? _holopadIcon;
    private Button _btnPlaceLocker = null!;
    private Image? _lockerIcon;
    private Button _btnPlaceSuitStorage = null!;
    private Image? _suitStorageIcon;
    private Button _btnPlaceBed = null!;
    private Image? _bedIcon;
    private Button _btnPlaceCamera = null!;
    private Image? _cameraIcon;
    private Button _btnPlaceDresser = null!;
    private Image? _dresserIcon;
    private Button _btnPlaceIntercom = null!;
    private Image? _intercomIcon;
    private Button _btnPlaceFax = null!;
    private Image? _faxIcon;
    private Button _btnDeleteArea = null!;
    private Button _btnDeleteSettings = null!;
    private Point _deleteStartPoint;
    private Point? _deleteEndPoint;
    private bool _isDeletingArea = false;
    private DeleteSettings _deleteSettings = new DeleteSettings();
    private Form? _deleteSettingsForm = null;
    private bool _showAlarmConnections = true;
    private Point _lastMousePosition;
    private string? _protoToPlace = null;
    private Button? _btnPlaceProto;
    private bool _snapEntityToCenter = false;
    private Button? _btnSnapEntityCenter;
    private Button? _btnCenterSettings;
    private PointF _centerOffset = new PointF(0.5f, 0.5f);
    private Form? _centerSettingsForm = null;
    private bool _snapEntityRotation = false;
    private Button? _btnEntityRotationSnap;
    private float _currentEntityRotation = 0f;
    // Ручной поворот размещаемого объекта (колёсико мыши): у интеркома переопределяет
    // автоматическое направление вовнутрь комнаты, у ЛКП задаёт поворот самой сущности.
    private float _placeRotation = 0f;
    private bool _placeRotationManual = false;

    // ===== Инструмент "Перемещение" =====
    private Button? _btnMove;
    private Button? _btnMoveSettings;
    private Button? _btnDecalRule;
    private MoveSettings _moveSettings = new MoveSettings();
    private Form? _moveSettingsForm = null;
    private List<object> _selectedObjects = new();
    private (int x, int y)? _lastClickTile = null;

    // ===== Инструмент "Лупа" =====
    private Button? _btnMagnifier;
    private Panel? _magnifierPanel;
    private Label? _magnifierHeaderLabel;
    private FlowLayoutPanel? _magnifierListPanel;
    private Button? _btnMagnifierUnpin;
    private bool _magnifierPinned = false;
    private (int x, int y)? _magnifierPinnedTile = null;
    private (int x, int y) _lastMagnifierTile = (-1, -1);
    private bool _isMovingSelection = false;
    private bool _isBoxSelecting = false;
    private bool _boxSelectAdditive = false;
    private Point _boxStartScreen;
    private Point _boxEndScreen;
    private (float x, float y) _moveDragStartWorld;
    private bool _moveDidMove = false;
    private List<MoveSnapshotItem> _moveSnapshot = new();
    private string _decalColor = "#FFFFFFFF";
    private bool _decalCleanable = false;
    private readonly DrawDepthManager _drawDepthManager = new();
    private Form? _projectSettingsForm = null;
    private Button? _btnProjectSettings;
    private Button? _btnSpyCapabilities;
    private Form? _spyCapabilitiesForm;
    private Button? _btnNetworkConfigurator;
    private Form? _networkConfiguratorForm;
    private Button? _btnGridDividerAdd;
    private Button? _btnGridDividerSubtract;
    // Режим выбора объекта на канвасе (кнопка «Выделить» в конфигураторе сетей)
    private bool _networkPickMode = false;
    // ===== Настройки внешнего вида =====
    private bool _darkTheme = false;
    // Цвета фона/сетки рендера раздельно для светлой и тёмной темы. Активные
    // (_renderBackground/_gridColor) подменяются на набор текущей темы при переключении.
    private Color _renderBgLight = Color.FromArgb(245, 245, 245);
    private Color _renderBgDark = Color.FromArgb(30, 30, 33);
    private Color _gridColorLight = Color.FromArgb(180, 200, 200, 200);
    private Color _gridColorDark = Color.FromArgb(150, 150, 150, 155);
    private Color _renderBackground = Color.White;                 // активный фон рендера
    private Form? _settingsForm;
    // Окна, которым уже применена текущая тема — чтобы не перекрашивать повторно
    // и при этом автоматически темизировать диалоги, открытые ПОСЛЕ переключения.
    private readonly HashSet<Form> _themedForms = new();
    // Цвет линий сетки на канвасе (может задаваться пользователем) 
    private Color _gridColor = Color.FromArgb(180, 200, 200, 200);
    // === Регулировка ширины боковых панелей ===
    // true — ширины панелей зафиксированы (по умолчанию); false — их можно менять
    // перетаскиванием за правый/левый край. Флажок и ширины кешируются между запусками.
    private bool _panelsLocked = true;
    private const int _repoPanelDefaultWidth = 240;
    private const int _toolPanelDefaultWidth = 200;
    private const int _panelMinWidth = 270; // минимальная ширина левой панели (репозиторий)
    private const int _toolPanelMinWidth = 200; // минимальная ширина правой панели (инструменты)
    private int _repoPanelCurrentWidth;
    private int _toolPanelCurrentWidth;
    private bool _resizingLeftPanel = false;  // тащим правый край левой (репозиторий) панели
    private bool _resizingRightPanel = false; // тащим левый край правой (инструменты) панели
    private const int _resizeHandleWidth = 4;
    private Panel? _resizeHandleLeft;   // узкая полоска на правом краю левой панели
    private Panel? _resizeHandleRight;  // узкая полоска на левом краю правой панели
    private int _resizeStartX;
    private int _resizeStartLeftWidth;
    private int _resizeStartRightWidth;


    private int _resizeNewWidth;                 // ширина, которая будет применена при отпускании мыши
    private ResizeGhostForm? _resizeGhost;       // полоска-призрак, пока идёт перетаскивание

    // Безрамочное окно без активации: двигается силами ОС и не вызывает перерисовку холста
    private sealed class ResizeGhostForm : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public ResizeGhostForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.DodgerBlue;
            Opacity = 0.5;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }
    // Позиции клеток (тайлов), участвующих в связях выбранного устройства —
    // для подсветки линий сети на канвасе
    private readonly HashSet<(int x, int y)> _networkHighlightPositions = new();
    // ===== Интерактивное редактирование ручной области декалей на канвасе =====
    private ManualDecalArea? _editingDecalArea = null;
    private Room? _editingDecalAreaRoom = null;
    private Action? _editingDecalAreaApplyCallback = null;
    private int _draggingDecalCornerIndex = -1;
    private bool _isDraggingDecalWholeArea = false;
    private ManualDecalArea? _decalAreaDragSnapshot = null;
    private (int x, int y) _decalAreaDragStartMouseTile;
    // Новую ручную область растягиваем мышью (как рамку выделения), а не кладём
    // сразу на всю комнату. _isDrawingDecalArea — активен режим растягивания,
    // _decalAreaDrawStart — угол, от которого тянем.
    private bool _isDrawingDecalArea = false;
    private (int x, int y) _decalAreaDrawStart;
    private bool _decalAreaDrawMouseDown = false;
    // Вызывается при завершении растягивания НОВОЙ области — добавляет её в слой
    // и перерисовывает список (кроме отображения в канвасе).
    private Action? _editingDecalAreaCommitAction = null;
    // Восстановление UI при отмене (Esc) режима растягивания — напр. вернуть подпись кнопки
    private Action? _editingDecalAreaCancelAction = null;
    // Область, которую сейчас растягиваем (ещё не добавлена в слой).
    private ManualDecalArea? _decalAreaPending = null;
    // Слой, в который добавится растягиваемая область — нужен для живого
    // предпросмотра декалей пока область ещё не закоммичена.
    private DecalLayer? _decalAreaPendingLayer = null;

    // Режим потайлёвой вырезки декалей (кнопка «✖» в окне «Ручные области»):
    // клики по клеткам области переключают их в CutCells (декали там не рисуются).
    private bool _isCuttingDecalCells = false;
    private ManualDecalArea? _cuttingDecalArea = null;
    private (int x, int y) _cuttingDecalStartTile;
    private bool _cuttingDecalMouseDown = false;
    private (int x, int y) _cuttingDecalDrawStart;

    // Режим потайлового изменения ФОРМЫ области (кнопка «➖ Вычесть» в окне «Ручные области»):
    // клики по клеткам области удаляют их из формы (RemovedCells) — клетка перестаёт быть
    // частью области, контур/декали вокруг пересчитываются. Это инструмент формы, а не удаления
    // декалей — в отличие от «✖» (убирает только декаль, форма остаётся).
    private bool _isSubtractingDecalCells = false;
    private ManualDecalArea? _subtractingDecalArea = null;
    private bool _subtractingDecalMouseDown = false;
    private (int x, int y) _subtractingDecalDrawStart;


    private class MoveSnapshotItem
    {
        public object Target = null!;
        public float OrigX;
        public float OrigY;
    }


    private static int FloorToInt(float v) => (int)Math.Floor(v);


    private bool IsObjectIncludedForMove(object obj)
    {
        return obj switch
        {
            Room => _moveSettings.IncludeRooms,
            PlacedTile => _moveSettings.IncludeTiles,        // тайлы из репозитория
            PlacedDecal => _moveSettings.IncludeDecals,      // декали из репозитория
            PipeEntity => _moveSettings.IncludePipes,        // трубы (газовые и утилизационные)
            WireEntity => _moveSettings.IncludeWires,        // электропроводка
            AirAlarmEntity => _moveSettings.IncludeAlarms,
            FireAlarmEntity => _moveSettings.IncludeAlarms,
            FirelockEntity => _moveSettings.IncludeOther,    // пожарные шлюзы — "другое"
            MapEntity e when e.GetType() == typeof(MapEntity) => _moveSettings.IncludePrototypes, // прототипы из репозитория
            MapEntity => _moveSettings.IncludeOther,         // прочие подтипы MapEntity — "другое"
            _ => _moveSettings.IncludeOther
        };
    }


    public MainForm()
    {
        Text = "MapperIce";
        Icon = AppIcon.Value;
        Size = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _pipeTypeManager = new PipeTypeManager();
        _decalPatternBuilder = new DecalPatternBuilder(_decalPackManager);
        _pipeBuilder = new PipeBuilder(_pipeTypeManager);
        _wireBuilder = new WireBuilder(_wireTypeManager);
        _doorUpdater = new DoorUpdater(_roomTypeManager);
        _tileBuilder = new TileBuilder(_roomTypeManager, _doorUpdater, _drawDepthManager);
        _tileGrid = new TileGrid();
        _renderer = new Renderer(Width, Height, _indexer, _drawDepthManager, _tileBuilder, _pipeBuilder, _wireBuilder, _wireTypeManager);

        LoadPanelSettings();
        LoadAppearanceSettings();

        CreateRepositoryPanel();
        CreateToolPanel();
        CreateLinkedEntitiesPanel();
        CreateGridPanel();
        CreateCanvas();
        CreateMagnifierPanel();
        CreateMenu();
        CreateResizeHandles();

        // Авто-темизация окон: любой диалог, открытый после переключения темы,
        // подхватит её из Application.Idle (уже оформленные окна пропускаются).
        Application.Idle += OnApplicationIdle;

        _toolManager.ToolChanged += OnToolChanged;

        var defaultGrid = new Grid
        {
            Uid = 2,
            Name = "Слой 2",
            Position = new PointF(0, 0),
            Color = Color.Blue
        };
        _map.AddGrid(defaultGrid);
        InitGridTabs();

        _repoManager.OnRepositoriesChanged += () => { UpdateRepoSelector(); };
        _indexer.OnIndexingComplete += () =>
        {
            ClearProtoIconCache();
            UpdatePrototypeList();
            UpdateWireJunctionCombos();
            UpdateWindowCombo();
            LoadDoorComboIcons();
            LoadWireTypeIcons();
            Render();
        };

        UpdateRepoSelector();
        LoadDoorComboIcons();
        LoadWireTypeIcons();
        _roomTypeTooltip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 1000, ReshowDelay = 100, ForeColor = Color.Black, BackColor = Color.WhiteSmoke }; UpdateBuffer();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // ComboBox, привязанный через DataSource, начинает работать через
        // BindingContext формы только после подключения к окну, поэтому выбор
        // типа комнаты повторно применяем здесь — иначе отображался бы самый
        // верхний пункт списка («Синдикат»), а не реально активный тип спавна
        SyncRoomTypeComboSelection();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Применяем сохранённую тему в момент показа окна (после создания всех панелей):
        // ApplyTheme подменяет цвета фона/сетки на набор нужной темы и перекрашивает окна.
        ApplyTheme();
        Render();
        // Подсказка при запуске (случайная из списка) — плавно появляется и исчезает
        ToastNotification.Show(this, StartupTips[Random.Shared.Next(StartupTips.Length)], "Подсказка", 8000);
    }

    // Короткие подсказки по возможностям редактора — при запуске показывается
    // одна случайная. Держим их здесь, рядом с местом показа, чтобы текст не
    // расходился с реальным поведением хоткеев и инструментов.
    private static readonly string[] StartupTips =
    {
        "Ctrl+Z — отменить действие, Ctrl+Y — вернуть отменённое.",
        "Ctrl+S — быстро сохранить проект в текущий файл.",
        "В окне «Лупа» нажмите левой кнопкой мыши по id прототипа — он выберется как прототип для установки в проект."
    };


    // === UNDO/REDO ===
    private void SaveState()
    {
        if (_map.ActiveGrid == null) return;
        _undo.AddState(_map.ActiveGrid);
    }


    private void RestoreState(GridSnapshot snapshot)
    {
        if (_map.ActiveGrid == null) return;

        // Режим редактирования/рисования области и окна Decal Rule держат ссылки на
        // старые экземпляры Room/ManualDecalArea, которые сейчас будут пересозданы.
        if (_editingDecalArea != null) EndEditDecalArea();
        CancelDrawDecalArea();
        if (_decalRuleForm != null && !_decalRuleForm.IsDisposed)
            _decalRuleForm.Close(); // «Ручные области» — дочернее окно, закроется вместе с ним

        snapshot.RestoreTo(_map.ActiveGrid);

        // Объекты, выделенные инструментом "Перемещение", ссылаются на старые экземпляры,
        // которые после отката пересозданы или удалены — сбрасываем выделение, чтобы
        // не остаться с "призрачной" рамкой на несуществующих объектах
        _selectedObjects.Clear();
        _lastClickTile = null;
        _isMovingSelection = false;
        _moveSnapshot.Clear();
        _renderer.SetSelection(_selectedObjects);

        // Undo/redo пересоздаёт комнаты (room.Clone() глубоко копирует AutoDecalRule
        // и ManualAreas), поэтому любые текущие ссылки на редактируемую/растягиваемую
        // ручную область указывают на осиротевшие объекты, не входящие в живой грид.
        // Сбрасываем это состояние, иначе выделение "ломается" и декали не строятся.
        _isDrawingDecalArea = false;
        _decalAreaDrawMouseDown = false;
        _decalAreaPending = null;
        _decalAreaPendingLayer = null;
        _editingDecalAreaCommitAction = null;
        _editingDecalAreaCancelAction = null;
        _editingDecalArea = null;
        _editingDecalAreaRoom = null;
        _editingDecalAreaApplyCallback = null;
        _draggingDecalCornerIndex = -1;
        _isDraggingDecalWholeArea = false;
        _decalAreaDragSnapshot = null;
        _renderer.ClearDecalAreaEditRect();
        _renderer.ClearDecalAreaDrawRect();
        _isCuttingDecalCells = false;
        _cuttingDecalArea = null;
        _cuttingDecalMouseDown = false;
        _renderer.ClearDecalAreaCutDragRect();
        _isSubtractingDecalCells = false;
        _subtractingDecalArea = null;
        _subtractingDecalMouseDown = false;
        _renderer.ClearDecalAreaSubtractDragRect();

        // Откат заново создал комнаты и их облаcти (room.Clone() в GridSnapshot).
        // Снимок уже хранит САМ декали узора (GridSnapshot.Decals), согласованные с
        // этими областями, поэтому пересчитывать их тут НЕ нужно — это был основной
        // источник фриза на крупных картах при многократном Ctrl+Z (дорогой скан
        // PurgeLegacyOrphanDecals # O(комнаты × слои × площадь)). Берём декали как есть.
        _renderer.SetSelection(_selectedObjects);

        UpdateTileGrid();
        Render();

        // Undo/redo пересоздала сущности грида — пере-привязать выбранные сущности
        // в окне «Конфигуратор сетей», чтобы оранжевая подсветка «привязок» не пропала.
        if (_networkConfiguratorForm is NetworkConfiguratorDialog netDlg && !netDlg.IsDisposed)
            netDlg.ResyncAfterUndo();
    }


    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _isDrawingDecalArea)
        {
            CancelDrawDecalArea();
            return true;
        }

        if (keyData == Keys.Escape && _isCuttingDecalCells)
        {
            CancelDecalCellCut();
            return true;
        }

        if (keyData == Keys.Escape && _isSubtractingDecalCells)
        {
            CancelDecalCellSubtract();
            return true;
        }

        if (keyData == Keys.Escape && _editingDecalArea != null)
        {
            EndEditDecalArea();
            return true;
        }

        // Ctrl+S — быстрое сохранение в текущий файл (открытый или последний
        // сохранённый в этом сеансе) либо в диалог «Сохранить как» для нового проекта
        if (keyData == (Keys.Control | Keys.S))
        {
            QuickSaveProject();
            return true;
        }

        if (_map.ActiveGrid == null) return base.ProcessCmdKey(ref msg, keyData);

        int uid = _map.ActiveGrid.Uid;

        if (keyData == (Keys.Control | Keys.Z))
        {
            if (_undo.CanUndo(uid))
            {
                var snapshot = _undo.Undo(uid);
                RestoreState(snapshot);
            }
            return true;
        }

        if (keyData == (Keys.Control | Keys.Y))
        {
            if (_undo.CanRedo(uid))
            {
                var snapshot = _undo.Redo(uid);
                RestoreState(snapshot);
            }
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }


    // === TILE GRID ===
    private void UpdateTileGrid()
    {
        if (_map.ActiveGrid != null)
        {
            _tileBuilder.UpdateTileGrid(_map.ActiveGrid, _tileGrid);
            _renderer.InvalidateTileGrid(_map.ActiveGrid.Uid);

            var networkBuilder = new AlarmNetworkBuilder(_alarmSettings);
            _cachedAlarmNetwork = networkBuilder.BuildNetwork(_map.ActiveGrid);
        }
    }
    // КОСТЫЛЬ против «вечных» дублей декалей: после какого-либо действия часть
    // декалей узора может остаться с PatternOwnerId комнаты, которой больше нет
    // (или которая пересоздавалась через клон после загрузки/undo), и обычный
    // пересчёт их не видит. Здесь за один проход убираем ВСЕ декали, у которых
    // владелец (room.Id) сейчас не существует ни в одной комнате грида.
    // Ничейные ручные декали (PatternOwnerId == null) не трогаем.
    private void PurgeOrphanedDecals(Grid grid)
    {
        if (grid.Decals.Count == 0) return;
        var aliveOwnerIds = new HashSet<int>(grid.Rooms.Count);
        foreach (var room in grid.Rooms)
            aliveOwnerIds.Add(room.Id);
        grid.Decals.RemoveAll(d => d.PatternOwnerId.HasValue && !aliveOwnerIds.Contains(d.PatternOwnerId.Value));
    }

    private void RecalculateDecalPatterns()
    {
        if (_map.ActiveGrid == null) return;
        var grid = _map.ActiveGrid;

        // Если растягивается НОВАЯ ручная область — пересчитываем ТОЛЬКО комнату-владельца
        // слоя, временно вставляя pending-область в слой. Это заметно дешевле полного
        // RecalculateAll и потому не тормозит каждое движение мыши (инпут-лаг).
        if (_decalAreaPendingLayer != null && _decalAreaPending != null)
        {
            var room = grid.Rooms.FirstOrDefault(r => r.AutoDecalRule.Layers.Contains(_decalAreaPendingLayer));
            var pending = _decalAreaPending;
            var areas = _decalAreaPendingLayer.ManualAreas;
            if (room != null && _decalAreaPendingLayer.Mode == DecalPatternMode.Manual && _decalAreaPendingLayer.Enabled)
            {
                bool inserted = !areas.Contains(pending);
                if (inserted) areas.Add(pending);
                _decalPatternBuilder.RecalculateForRoom(grid, room);
                if (inserted) areas.Remove(pending);
            }
            else if (room != null)
            {
                _decalPatternBuilder.RecalculateForRoom(grid, room);
            }
            return;
        }

        // Если редактируется существующая область — тоже пересчитываем только её комнату.
        if (_editingDecalAreaRoom != null)
        {
            _decalPatternBuilder.RecalculateForRoom(grid, _editingDecalAreaRoom);
            return;
        }

        _decalPatternBuilder.RecalculateAll(grid);
    }

    // === ХОЛСТ ===
    private void CreateCanvas()
    {
        _canvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            AllowDrop = true
        };
        _canvas.MouseDown += OnMouseDown;
        _canvas.MouseMove += OnMouseMove;
        _canvas.MouseUp += OnMouseUp;
        _canvas.Paint += OnPaint;
        _canvas.Resize += OnResize;
        _canvas.MouseWheel += OnMouseWheel;
        _canvas.MouseLeave += OnMouseLeave;
        _canvas.DragEnter += Canvas_DragOver;
        _canvas.DragOver += Canvas_DragOver;
        Controls.Add(_canvas);
    }


    // Effect = Copy выставляется только если под курсором реально есть комната —
    // иначе GiveFeedback (см. ProtoList_GiveFeedback) покажет обычную стрелку,
    // а не системный значок "вставки" над пустотой холста.
    private void Canvas_DragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;

        if (e.Data == null || !e.Data.GetDataPresent(typeof(string))) return;

        var grid = _map.ActiveGrid;
        if (grid == null) return;

        var clientPoint = _canvas.PointToClient(new Point(e.X, e.Y));
        var (tileX, tileY) = GetTilePosition(clientPoint);

        var room = grid.Rooms.FirstOrDefault(r => r.Contains(tileX, tileY));
        if (room != null)
            e.Effect = DragDropEffects.Copy;
    }


    // === МЕНЮ ===
    private void CreateMenu()
    {
        var menu = new MenuStrip();
        menu.BackColor = Color.FromArgb(240, 240, 240);
        menu.Font = new Font("Arial", 9);

        var fileMenu = new ToolStripMenuItem("Файл");
        fileMenu.DropDownItems.Add("Сохранить проект", null, (s, e) => SaveProject());
        fileMenu.DropDownItems.Add("Загрузить проект", null, (s, e) => LoadProject());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Сохранить в YML", null, (s, e) => ExportToYAML());
        fileMenu.DropDownItems.Add("Загрузить из YML", null, (s, e) => LoadMapFromYAML());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Рендер в PNG", null, (s, e) => RenderToPng());
        menu.Items.Add(fileMenu);

        var settingsBtn = new ToolStripMenuItem("⚙ Настройки", null, (s, e) => ShowSettingsMenu());
        menu.Items.Add(settingsBtn);

        var resetBtn = new ToolStripMenuItem("↺ Сбросить вид", null, (s, e) =>
        {
            _scale = 1.0f;
            _viewOffset = new PointF(0, 0);
            Render();
        });
        menu.Items.Add(resetBtn);

        Controls.Add(menu);
        MainMenuStrip = menu;
    }


    // === НАСТРОЙКИ ===
    // Окно настроек (немодальное — не блокирует основное окно): выбор цвета фона
    // рендера, цвета сетки, чекбокс тёмной темы и фиксации ширины панелей.
    private void ShowSettingsMenu()
    {
        // Если окно уже открыто — просто вывести его на передний план
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.BringToFront();
            _settingsForm.Focus();
            return;
        }

        var dlg = new Form
        {
            Text = "Настройки",
            StartPosition = FormStartPosition.CenterParent,
            Font = new Font("Arial", 9),
            Size = new Size(400, 300),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ShowIcon = false
        };
        dlg.Owner = this;
        _settingsForm = dlg;
        AppTheme.ApplyToForm(dlg);

        // --- Цвет фона рендера ---
        var bgLabel = new Label
        {
            Text = "Цвет фона рендера:",
            Location = new Point(20, 20),
            Size = new Size(160, 20),
            BackColor = Color.Transparent
        };
        dlg.Controls.Add(bgLabel);

        var bgColorPreview = new Panel
        {
            Location = new Point(190, 18),
            Size = new Size(24, 24),
            BackColor = _renderBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        // Превью показывает цвет настройки — это данные, тема его фон не меняет
        AppTheme.MarkDataBack(bgColorPreview);
        dlg.Controls.Add(bgColorPreview);

        var bgColorBtn = new Button
        {
            Text = "Выбрать...",
            Location = new Point(220, 16),
            Size = new Size(120, 28)
        };
        bgColorBtn.Click += (s, e) =>
        {
            using var picker = new ColorDialog
            {
                Color = _renderBackground,
                FullOpen = true
            };
            if (picker.ShowDialog(dlg) == DialogResult.OK)
            {
                SetRenderBackground(picker.Color);
                bgColorPreview.BackColor = _renderBackground;
                SaveAppearanceSettings();
                Render();
            }
        };
        dlg.Controls.Add(bgColorBtn);

        // --- Цвет сетки ---
        var gridLabel = new Label
        {
            Text = "Цвет сетки:",
            Location = new Point(20, 60),
            Size = new Size(160, 20),
            BackColor = Color.Transparent
        };
        dlg.Controls.Add(gridLabel);

        var gridColorPreview = new Panel
        {
            Location = new Point(190, 58),
            Size = new Size(24, 24),
            BackColor = _gridColor,
            BorderStyle = BorderStyle.FixedSingle
        };
        // Превью показывает цвет настройки — это данные, тема его фон не меняет
        AppTheme.MarkDataBack(gridColorPreview);
        dlg.Controls.Add(gridColorPreview);

        var gridColorBtn = new Button
        {
            Text = "Выбрать...",
            Location = new Point(220, 56),
            Size = new Size(120, 28)
        };
        gridColorBtn.Click += (s, e) =>
        {
            using var picker = new ColorDialog
            {
                Color = _gridColor,
                FullOpen = true
            };
            if (picker.ShowDialog(dlg) == DialogResult.OK)
            {
                SetGridColor(picker.Color);
                gridColorPreview.BackColor = _gridColor;
                SaveAppearanceSettings();
                Render();
            }
        };
        dlg.Controls.Add(gridColorBtn);

        // --- Тёмная тема (меняет не только сетку: весь интерфейс во всех окнах) ---
        var darkTheme = new CheckBox
        {
            Text = "Тёмная тема",
            Location = new Point(20, 100),
            Size = new Size(320, 24),
            Checked = _darkTheme,
            BackColor = Color.Transparent
        };
        darkTheme.CheckedChanged += (s, e) =>
        {
            _darkTheme = darkTheme.Checked;
            // ApplyTheme подменяет цвета фона/сетки на набор новой темы;
            // превью в этом окне показываем уже новые значения
            ApplyTheme();
            bgColorPreview.BackColor = _renderBackground;
            gridColorPreview.BackColor = _gridColor;
            SaveAppearanceSettings();
            Render();
        };
        dlg.Controls.Add(darkTheme);

        // --- Фиксировать ширину панелей ---
        var lockPanels = new CheckBox
        {
            Text = "Фиксировать ширину панелей",
            Location = new Point(20, 130),
            Size = new Size(320, 24),
            Checked = _panelsLocked,
            BackColor = Color.Transparent
        };
        lockPanels.CheckedChanged += (s, e) =>
        {
            _panelsLocked = lockPanels.Checked;
            SavePanelSettings();
            RefreshResizeHandles();
        };
        dlg.Controls.Add(lockPanels);

        var closeBtn = new Button
        {
            Text = "Закрыть",
            DialogResult = DialogResult.OK,
            Location = new Point(dlg.ClientSize.Width - 110, dlg.ClientSize.Height - 55),
            Size = new Size(90, 28)
        };
        dlg.Controls.Add(closeBtn);

        // Немодальный показ: основное окно продолжает работать, пока открыты настройки
        dlg.FormClosed += (s, e) => _settingsForm = null;
        dlg.Show(this);
    }


    // === ТЁМНАЯ ТЕМА ===
    // Переключает тему приложения и перекрашивает главное окно + все открытые
    // дочерние окна. Цвета берутся из AppTheme (в тёмной теме — серовато-белый текст,
    // тёмные поля всех интерактивных контролов).
    private void ApplyTheme()
    {
        AppTheme.Dark = _darkTheme;

        // Подменяем цвета фона и сетки рендера на набор текущей темы
        ApplyActiveRenderColors();

        // Тема сменилась — перекрашиваем все открытые окна заново
        _themedForms.Clear();
        ThemeOpenForms();

        // Вкладки слоёв берут цвета из палитры (AppTheme.TabActiveBack/TabInactiveBack)
        // и темой намеренно не перекрашиваются — иначе активная и неактивная вкладки
        // стали бы одного цвета. Поэтому пересобираем их уже под новую тему.
        RefreshTabStrip();

        Invalidate();
    }

    // Подставляет цвета фона/сетки активной темы в рандерер и канвас.
    private void ApplyActiveRenderColors()
    {
        _renderBackground = _darkTheme ? _renderBgDark : _renderBgLight;
        _gridColor = _darkTheme ? _gridColorDark : _gridColorLight;
        _renderer.BackgroundColor = _renderBackground;
        _renderer.GridColor = _gridColor;
        if (_canvas != null) _canvas.BackColor = _renderBackground;
    }

    // Меняет цвет фона рендера для ТЕКУЩЕЙ темы (светлой или тёмной — по флагу)
    // и сразу применяет его.
    private void SetRenderBackground(Color color)
    {
        if (_darkTheme) _renderBgDark = color; else _renderBgLight = color;
        _renderBackground = color;
        _renderer.BackgroundColor = color;
        if (_canvas != null) _canvas.BackColor = color;
    }

    // Меняет цвет сетки для ТЕКУЩЕЙ темы и сразу применяет его.
    private void SetGridColor(Color color)
    {
        if (_darkTheme) _gridColorDark = color; else _gridColorLight = color;
        _gridColor = color;
        _renderer.GridColor = color;
    }

    // Темизирует все открытые окна приложения (главное, диалоги — модальные и нет).
    // Вызывается постоянно из Application.Idle, поэтому уже оформленные окна
    // пропускаются, а новые (открытые после переключения темы) подхватываются
    // автоматически. Заголовки окон красятся отдельно — см. AppTheme.ApplyTitleBar.
    private void ThemeOpenForms()
    {
        // Убираем ссылки на уже закрытые окна, чтобы не держать их в памяти
        _themedForms.RemoveWhere(f => f.IsDisposed);

        foreach (Form f in Application.OpenForms)
        {
            if (f == null || f.IsDisposed) continue;
            if (_themedForms.Contains(f)) continue;

            AppTheme.ApplyToForm(f);
            AppIcon.ApplyToForm(f);
            _themedForms.Add(f);
        }
    }

    private void OnApplicationIdle(object? sender, EventArgs e)
    {
        ThemeOpenForms();
    }


    // === НАСТРОЙКИ ШИРИНЫ ПАНЕЛЕЙ ===
    [System.Serializable]
    private class PanelSettings
    {
        public bool PanelsLocked { get; set; } = true;
        public int RepoPanelWidth { get; set; } = 240;
        public int ToolPanelWidth { get; set; } = 200;
    }

    private static string GetPanelSettingsPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MapperIce");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "panel_settings.json");
    }

    // Загружает закешированные из прошлого запуска настройки ширины панелей.
    private void LoadPanelSettings()
    {
        try
        {
            var path = GetPanelSettingsPath();
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<PanelSettings>(json);
            if (s == null) return;
            _panelsLocked = s.PanelsLocked;
            _repoPanelCurrentWidth = ClampPanelWidth(s.RepoPanelWidth, _panelMinWidth);
            _toolPanelCurrentWidth = ClampPanelWidth(s.ToolPanelWidth, _toolPanelMinWidth);
        }
        catch
        {
            // Не критично: повреждённый файл настроек — берём значения по умолчанию
        }
    }

    // Пишет настройки ширины панелей для сохранения между запусками (атомарно).
    private void SavePanelSettings()
    {
        try
        {
            var s = new PanelSettings
            {
                PanelsLocked = _panelsLocked,
                RepoPanelWidth = _repoPanel.Width,
                ToolPanelWidth = _toolPanel.Width
            };
            var path = GetPanelSettingsPath();
            var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            // Игнорируем ошибки записи — настройки не критичны
        }
    }

    // === Персистентность настроек внешнего вида (тема/цвета) ===
    [System.Serializable]
    private class AppearanceSettings
    {
        public bool DarkTheme { get; set; }
        // Цвета фона/сетки рендера для СВЕТЛОЙ темы
        public int RenderBgArgb { get; set; } = Color.White.ToArgb();
        public int GridColorArgb { get; set; } = Color.FromArgb(180, 200, 200, 200).ToArgb();
        // Цвета фона/сетки рендера для ТЁМНОЙ темы
        public int RenderBgDarkArgb { get; set; } = Color.FromArgb(30, 30, 33).ToArgb();
        public int GridColorDarkArgb { get; set; } = Color.FromArgb(150, 150, 150, 155).ToArgb();
    }

    private static string GetAppearanceSettingsPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MapperIce");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "appearance_settings.json");
    }

    private void LoadAppearanceSettings()
    {
        try
        {
            var path = GetAppearanceSettingsPath();
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<AppearanceSettings>(json);
            if (s == null) return;
            _darkTheme = s.DarkTheme;
            _renderBgLight = Color.FromArgb(s.RenderBgArgb);
            _gridColorLight = Color.FromArgb(s.GridColorArgb);
            _renderBgDark = Color.FromArgb(s.RenderBgDarkArgb);
            _gridColorDark = Color.FromArgb(s.GridColorDarkArgb);
        }
        catch
        {
            // Не критично — берём значения по умолчанию
        }
    }

    private void SaveAppearanceSettings()
    {
        try
        {
            var s = new AppearanceSettings
            {
                DarkTheme = _darkTheme,
                RenderBgArgb = _renderBgLight.ToArgb(),
                GridColorArgb = _gridColorLight.ToArgb(),
                RenderBgDarkArgb = _renderBgDark.ToArgb(),
                GridColorDarkArgb = _gridColorDark.ToArgb()
            };
            var path = GetAppearanceSettingsPath();
            var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            // Игнорируем ошибки записи
        }
    }

    private static int ClampPanelWidth(int value, int min)
    {
        if (value < min) return min;
        return value;
    }

    // Создаёт тонкие невидимые полоски-захваты на границах боковых панелей,
    // которыми можно перетаскивать их ширину, пока фиксация выключена.
    private void CreateResizeHandles()
    {
        // Захват левой панели: внутри неё, у правого края, левее серой линии (1px)
        _resizeHandleLeft = new Panel
        {
            Bounds = new Rectangle(_repoPanel.Width - _resizeHandleWidth - 1, 0,
                _resizeHandleWidth, _repoPanel.Height),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = _repoPanel.BackColor,
            TabStop = false,
            Cursor = Cursors.SizeWE
        };
        _resizeHandleLeft.MouseDown += ResizeHandleLeft_MouseDown;
        _resizeHandleLeft.MouseMove += ResizeHandleLeft_MouseMove;
        _resizeHandleLeft.MouseUp += ResizeHandleLeft_MouseUp;
        _repoPanel.Controls.Add(_resizeHandleLeft);

        // Захват правой панели: внутри неё, у левого края, правее серой линии (1px)
        _resizeHandleRight = new Panel
        {
            Bounds = new Rectangle(1, 0, _resizeHandleWidth, _toolPanel.Height),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = _toolPanel.BackColor,
            TabStop = false,
            Cursor = Cursors.SizeWE
        };
        _resizeHandleRight.MouseDown += ResizeHandleRight_MouseDown;
        _resizeHandleRight.MouseMove += ResizeHandleRight_MouseMove;
        _resizeHandleRight.MouseUp += ResizeHandleRight_MouseUp;
        _toolPanel.Controls.Add(_resizeHandleRight);

        RefreshResizeHandles();
    }
    // Позиционирует полоски-захваты на краях панелей и показывает их только
    // когда ширина панелей не зафиксирована (кнопка в меню снята).
    private void RefreshResizeHandles()
    {
        if (_resizeHandleLeft == null || _resizeHandleRight == null) return;

        bool show = !_panelsLocked;
        _resizeHandleLeft.Visible = show;
        _resizeHandleRight.Visible = show;

        if (show)
        {
            _resizeHandleLeft.BringToFront();
            _resizeHandleRight.BringToFront();
        }
    }


    private void ResizeHandleLeft_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_panelsLocked) return;
        _resizingLeftPanel = true;
        _resizeStartX = Control.MousePosition.X;
        _resizeStartLeftWidth = _repoPanel.Width;
        _resizeNewWidth = _repoPanel.Width;
    }

    private void ResizeHandleLeft_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_resizingLeftPanel || _panelsLocked) return;
        int delta = Control.MousePosition.X - _resizeStartX;
        _resizeNewWidth = Math.Max(_panelMinWidth, Math.Min(600, _resizeStartLeftWidth + delta));
        UpdateResizeGhost(_repoPanel, _repoPanel.PointToScreen(new Point(_resizeNewWidth, 0)).X);
    }

    private void ResizeHandleLeft_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_resizingLeftPanel) return;
        _resizingLeftPanel = false;
        CloseResizeGhost();
        _repoPanel.Width = _resizeNewWidth;
        RefreshResizeHandles();
        _repoPanelCurrentWidth = _repoPanel.Width;
        SavePanelSettings();
    }

    private void ResizeHandleRight_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_panelsLocked) return;
        _resizingRightPanel = true;
        // экранные координаты: сам захват теперь сдвигается вместе с краем панели
        _resizeStartX = Control.MousePosition.X;
                _resizeStartRightWidth = _toolPanel.Width;
        _resizeNewWidth = _toolPanel.Width;
    }

    private void ResizeHandleRight_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_resizingRightPanel || _panelsLocked) return;
        int delta = _resizeStartX - Control.MousePosition.X;
        _resizeNewWidth = Math.Max(_toolPanelMinWidth, Math.Min(600, _resizeStartRightWidth + delta));
        UpdateResizeGhost(_toolPanel, _toolPanel.PointToScreen(new Point(_toolPanel.Width - _resizeNewWidth, 0)).X);
    
    }

    private void ResizeHandleRight_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_resizingRightPanel) return;
        _resizingRightPanel = false;
        CloseResizeGhost();
        _toolPanel.Width = _resizeNewWidth;
        _toolPanelCurrentWidth = _toolPanel.Width;
        SavePanelSettings();    
    }

    // Показывает/двигает полоску-призрак на заданной экранной координате X по высоте панели
    private void UpdateResizeGhost(Control panel, int screenX)
    {
        var bounds = new Rectangle(screenX - 1, panel.PointToScreen(Point.Empty).Y, 3, panel.Height);
        if (_resizeGhost == null)
        {
            _resizeGhost = new ResizeGhostForm { Bounds = bounds };
            _resizeGhost.Show(this);
        }
        else
        {
            _resizeGhost.Bounds = bounds;
        }
    }

    private void CloseResizeGhost()
    {
        _resizeGhost?.Close();
        _resizeGhost = null;
    }

    // === ОТРИСОВКА ===
    private void OnToolChanged(ToolManager.Tool tool)
    {
        _btnCreateRoom.BackColor = AppTheme.ButtonBack;
        _btnSubtractRoom.BackColor = AppTheme.ButtonBack;
        _btnRestoreRoom.BackColor = AppTheme.ButtonBack;
        _btnAddAll.BackColor = AppTheme.ButtonBack;
        _btnDelete.BackColor = AppTheme.ButtonBack;
        _btnDeleteArea.BackColor = AppTheme.ButtonBack;
        _btnDeleteSettings.BackColor = AppTheme.ButtonBack;
        if (_doorToolCombo != null) _doorToolCombo.BackColor = AppTheme.BackField;
        if (_windowToolCombo != null) _windowToolCombo.BackColor = AppTheme.BackField;
        if (_btnPipeDistra != null) _btnPipeDistra.BackColor = AppTheme.ButtonBack;
        if (_btnPipeWaste != null) _btnPipeWaste.BackColor = AppTheme.ButtonBack;
        if (_btnPipeNormal != null) _btnPipeNormal.BackColor = AppTheme.ButtonBack;
        if (_btnPipeUtil != null) _btnPipeUtil.BackColor = AppTheme.ButtonBack;
        if (_utilToolCombo != null) _utilToolCombo.BackColor = AppTheme.BackField;
        if (_wireTypeCombo != null) _wireTypeCombo.BackColor = AppTheme.BackField;
        if (_powerJunctionCombo != null) _powerJunctionCombo.BackColor = AppTheme.BackField;
        if (_btnMove != null) _btnMove.BackColor = AppTheme.ButtonBack;

        // Наследование декалей — не инструмент канвы, но переключение НА любой
        // инструмент (включая "Узор по периметру") должно закрывать это окно,
        // чтобы два режима не путались и не оставались активными одновременно
        if (_decalInheritanceForm != null && !_decalInheritanceForm.IsDisposed)
            _decalInheritanceForm.Close();
        if (_btnDecalRule != null) _btnDecalRule.BackColor = AppTheme.ButtonBack;
        if (_btnNetworkConfigurator != null) _btnNetworkConfigurator.BackColor = AppTheme.ButtonBack;
        if (_btnGridDividerAdd != null) _btnGridDividerAdd.BackColor = AppTheme.ButtonBack;
        if (_btnGridDividerSubtract != null) _btnGridDividerSubtract.BackColor = AppTheme.ButtonBack;
        if (_btnAirAlarm != null) _btnAirAlarm.BackColor = AppTheme.ButtonBack;
        if (_btnFireAlarm != null) _btnFireAlarm.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceHolopad != null) _btnPlaceHolopad.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceLocker != null) _btnPlaceLocker.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceSuitStorage != null) _btnPlaceSuitStorage.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceBed != null) _btnPlaceBed.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceCamera != null) _btnPlaceCamera.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceDresser != null) _btnPlaceDresser.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceIntercom != null) _btnPlaceIntercom.BackColor = AppTheme.ButtonBack;
        if (_btnPlaceFax != null) _btnPlaceFax.BackColor = AppTheme.ButtonBack;
        if (_btnMagnifier != null) _btnMagnifier.BackColor = AppTheme.ButtonBack;
        if (tool != ToolManager.Tool.PlacePrototype) _protoToPlace = null;

        // Ручной поворот привязки (колёсико) сбрасывается при смене инструмента:
        // новая привязка снова разворачивается автоматически. Превью привязки живёт
        // только пока выбран её инструмент.
        _placeRotationManual = false;
        _placeRotation = 0f;
        if (!IsPlacementPreviewTool(tool)) _renderer.ClearEntityPreview();

        if (tool != ToolManager.Tool.Move)
        {
            _selectedObjects.Clear();
            _lastClickTile = null;
            _isMovingSelection = false;
            _isBoxSelecting = false;
            _moveSnapshot.Clear();
            _renderer.SetSelection(_selectedObjects);
            _renderer.ClearSelectionBox();
        }

        // Сброс активной рамки удаления области при смене инструмента
        _isDeletingArea = false;
        _deleteEndPoint = null;
        _renderer.ClearDeleteAreaRect();

        switch (tool)
        {
            case ToolManager.Tool.CreateRoom:
                _btnCreateRoom.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.SubtractRoom:
                _btnSubtractRoom.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.RestoreRoom:
                _btnRestoreRoom.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.ExpandRoom:
                _btnAddAll.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.Delete:
                _btnDelete.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.DeleteArea:
                _btnDeleteArea.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.DeleteSettings:
                _btnDeleteSettings.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.Door:
                if (_doorToolCombo != null)
                {
                    _doorToolCombo.BackColor = AppTheme.SelectedBack;
                    _doorToolCombo.SelectedItem = "Дверь";
                }
                break;
            case ToolManager.Tool.DoorGlass:
                if (_doorToolCombo != null)
                {
                    _doorToolCombo.BackColor = AppTheme.SelectedBack;
                    _doorToolCombo.SelectedItem = "Дверь (стекло)";
                }
                break;
            case ToolManager.Tool.Passage:
                if (_doorToolCombo != null)
                {
                    _doorToolCombo.BackColor = AppTheme.SelectedBack;
                    _doorToolCombo.SelectedItem = "Проход";
                }
                break;
            case ToolManager.Tool.RestoreWall:
                if (_doorToolCombo != null)
                {
                    _doorToolCombo.BackColor = AppTheme.SelectedBack;
                    _doorToolCombo.SelectedItem = "Восстановить стену";
                }
                break;
            case ToolManager.Tool.ReplaceWallWithWindow:
                if (_windowToolCombo != null)
                {
                    _windowToolCombo.BackColor = AppTheme.SelectedBack;
                }
                break;
            case ToolManager.Tool.PipeDistra:
                if (_btnPipeDistra != null) _btnPipeDistra.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PipeWaste:
                if (_btnPipeWaste != null) _btnPipeWaste.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PipeNormal:
                if (_btnPipeNormal != null) _btnPipeNormal.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PipeUtil:
                if (_btnPipeUtil != null) _btnPipeUtil.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PipeUtilSettings:
                if (_utilToolCombo != null)
                {
                    _utilToolCombo.BackColor = AppTheme.SelectedBack;
                    _utilToolCombo.SelectedItem = "🔧 Направление";
                }
                break;
            case ToolManager.Tool.UtilWrenches:
                if (_utilToolCombo != null)
                {
                    _utilToolCombo.BackColor = AppTheme.SelectedBack;
                    _utilToolCombo.SelectedItem = "🔧🔧 Фильтр";
                }
                break;
            case ToolManager.Tool.UtilNodeLabel:
                if (_utilToolCombo != null)
                {
                    _utilToolCombo.BackColor = AppTheme.SelectedBack;
                    _utilToolCombo.SelectedItem = "⚙ Настройки";
                }
                break;
            case ToolManager.Tool.AirAlarm:
                if (_btnAirAlarm != null) _btnAirAlarm.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.FireAlarm:
                if (_btnFireAlarm != null) _btnFireAlarm.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceHolopad:
                if (_btnPlaceHolopad != null) _btnPlaceHolopad.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceLocker:
                if (_btnPlaceLocker != null) _btnPlaceLocker.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceSuitStorage:
                if (_btnPlaceSuitStorage != null) _btnPlaceSuitStorage.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceBed:
                if (_btnPlaceBed != null) _btnPlaceBed.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceCamera:
                if (_btnPlaceCamera != null) _btnPlaceCamera.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceDresser:
                if (_btnPlaceDresser != null) _btnPlaceDresser.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceIntercom:
                if (_btnPlaceIntercom != null) _btnPlaceIntercom.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceFax:
                if (_btnPlaceFax != null) _btnPlaceFax.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlacePrototype:
                break;
            case ToolManager.Tool.Move:
                if (_btnMove != null) _btnMove.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.DecalRule:
                if (_btnDecalRule != null) _btnDecalRule.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.NetworkConfigurator:
                if (_btnNetworkConfigurator != null) _btnNetworkConfigurator.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.GridDividerAdd:
                if (_btnGridDividerAdd != null) _btnGridDividerAdd.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.GridDividerSubtract:
                if (_btnGridDividerSubtract != null) _btnGridDividerSubtract.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.Magnifier:
                if (_btnMagnifier != null) _btnMagnifier.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.WireHV:
            case ToolManager.Tool.WireMV:
            case ToolManager.Tool.WireLV:
                if (_wireTypeCombo != null) _wireTypeCombo.BackColor = AppTheme.SelectedBack;
                break;
            case ToolManager.Tool.PlaceSubstation or ToolManager.Tool.PlaceApc:
                if (_powerJunctionCombo != null) _powerJunctionCombo.BackColor = AppTheme.SelectedBack;
                break;

        }

        Cursor = tool switch
        {
            ToolManager.Tool.CreateRoom or ToolManager.Tool.SubtractRoom or ToolManager.Tool.RestoreRoom => Cursors.Cross,
            ToolManager.Tool.Delete or ToolManager.Tool.DeleteArea or ToolManager.Tool.DeleteSettings => Cursors.Hand,
            ToolManager.Tool.DecalRule or ToolManager.Tool.NetworkConfigurator => Cursors.Hand,
            ToolManager.Tool.Magnifier => Cursors.Cross,
            ToolManager.Tool.Door or ToolManager.Tool.DoorGlass or ToolManager.Tool.Passage or ToolManager.Tool.RestoreWall or ToolManager.Tool.ExpandRoom or ToolManager.Tool.ReplaceWallWithWindow => Cursors.Help,
            ToolManager.Tool.PipeDistra or ToolManager.Tool.PipeWaste or ToolManager.Tool.PipeNormal or ToolManager.Tool.PipeUtil => Cursors.Help,
            ToolManager.Tool.PipeUtilSettings => Cursors.Help,
            ToolManager.Tool.UtilWrenches => Cursors.Help,
            ToolManager.Tool.UtilNodeLabel => Cursors.Help,
            ToolManager.Tool.AirAlarm or ToolManager.Tool.FireAlarm => Cursors.Help,
            ToolManager.Tool.PlaceHolopad => Cursors.Help,
            ToolManager.Tool.PlaceLocker => Cursors.Help,
            ToolManager.Tool.PlaceSuitStorage => Cursors.Help,
            ToolManager.Tool.PlaceBed => Cursors.Help,
            ToolManager.Tool.PlaceCamera => Cursors.Help,
            ToolManager.Tool.PlaceDresser => Cursors.Help,
            ToolManager.Tool.PlaceIntercom => Cursors.Help,
            ToolManager.Tool.PlaceFax => Cursors.Help,
            ToolManager.Tool.WireHV or ToolManager.Tool.WireMV or ToolManager.Tool.WireLV => Cursors.Help,
            ToolManager.Tool.PlaceSubstation or ToolManager.Tool.PlaceApc => Cursors.Help,
            _ => Cursors.Default
        };

        if (_magnifierPanel != null && _magnifierForm != null)
        {
            bool showMagnifier = tool == ToolManager.Tool.Magnifier;
            if (showMagnifier)
            {
                // Каждое новое включение инструмента начинается с "живого" (неприкреплённого)
                // режима — иначе после переключения на другой инструмент и обратно
                // пользователь мог бы неожиданно увидеть старую зафиксированную клетку
                _magnifierPinned = false;
                _magnifierPinnedTile = null;
                _lastMagnifierTile = (-1, -1);
                RepositionMagnifierPanel();
                UpdateMagnifierPanel();
                _magnifierFollowTimer?.Start();
                if (!_magnifierForm.Visible)
                    _magnifierForm.Show(this);
            }
            else
            {
                _magnifierFollowTimer?.Stop();
                if (_magnifierForm.Visible)
                    _magnifierForm.Hide();
            }
        }

        Render();
    }


    private void OnResize(object? sender, EventArgs e)
    {
        _renderer.Resize(_canvas.Width, _canvas.Height);
        RepositionMagnifierPanel();
        RefreshResizeHandles();
        Render();
    }


    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (_renderer != null)
        {
            if (!_reuseLastFrame || _lastFrame == null)
            {
                _lastFrame = _renderer.Render(_map, _scale, _viewOffset, _currentRoom,
                    _toolManager.CurrentTool.ToString());
            }
            e.Graphics.DrawImage(_lastFrame, 0, 0);
        }
    }


    private void Render()
    {
        // Любой явный Render() означает, что карта изменилась — кадр нужно строить заново
        _reuseLastFrame = false;

        _renderer.HideRoomOverlay = _hideRoomOverlay;
        _renderer.ShowPipeOverlay = _showPipeOverlay;
        _renderer.ShowAlarmConnections = _showAlarmConnections;
        _renderer.ShowWires = _showWires;
        _renderer.ShowCameraAreas = _showCameraAreas;
        _renderer.ShowNetworkLinks = _showNetworkLinks;
        _renderer.NetworkHighlightPositions = _networkHighlightPositions;
        _renderer.BackgroundColor = _renderBackground;
        _renderer.GridColor = _gridColor;

        if (_map.ActiveGrid != null)
        {
            // Обычно уже посчитано в UpdateTileGrid() при последнем структурном
            // изменении. null бывает только на самом первом рендере до первого
            // редактирования — тогда считаем один раз лениво
            if (_cachedAlarmNetwork == null)
            {
                var networkBuilder = new AlarmNetworkBuilder(_alarmSettings);
                _cachedAlarmNetwork = networkBuilder.BuildNetwork(_map.ActiveGrid);
            }
            _renderer.SetAlarmNetwork(_cachedAlarmNetwork);
        }
        else
        {
            _renderer.SetAlarmNetwork(null!);
        }

        if (_toolManager.CurrentTool == ToolManager.Tool.Magnifier)
        {
            UpdateMagnifierPanel();
        }

        _canvas.Invalidate();
    }

    private void UpdateBuffer()
    {
        _renderer.Resize(_canvas.Width, _canvas.Height);
    }


    // === ОБРАБОТКА МЫШИ ===
    private (int x, int y) GetTilePosition(Point mouseLocation)
    {
        if (_map.ActiveGrid == null) return (0, 0);

        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        int activeIndex = _map.Grids.IndexOf(_map.ActiveGrid);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = (_map.ActiveGrid.Position.Y + layerOffsetY) * tileSize;
        float worldX = (mouseLocation.X + _viewOffset.X - gridOffsetX) / tileSize;
        float worldY = (mouseLocation.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        return ((int)Math.Floor(worldX), (int)Math.Floor(worldY));
    }


    private (float x, float y) GetPrecisePosition(Point mouseLocation)
    {
        if (_map.ActiveGrid == null) return (0f, 0f);

        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        int activeIndex = _map.Grids.IndexOf(_map.ActiveGrid);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = (_map.ActiveGrid.Position.Y + layerOffsetY) * tileSize;
        float worldX = (mouseLocation.X + _viewOffset.X - gridOffsetX) / tileSize;
        float worldY = (mouseLocation.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        return (worldX, worldY);
    }

    /// <summary>
    /// Режим «Выделить» из конфигуратора сетей: находит ближайшую к клику сущность
    /// (по мировому расстоянию) и передаёт её в окно конфигуратора для выделения.
    /// Работает независимо от того, целая координата у сущности или центр тайла.
    /// </summary>
    private void PickEntityByClick(Point clientPoint)
    {
        if (_map.ActiveGrid == null) return;
        var w = GetPrecisePosition(clientPoint);

        MapEntity? best = null;
        float bestD = float.MaxValue;
        foreach (var ent in _map.ActiveGrid.Entities.OfType<MapEntity>())
        {
            if (string.IsNullOrEmpty(ent.Proto)) continue;
            float dx = ent.X - w.x;
            float dy = ent.Y - w.y;
            float d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = ent; }
        }

        if (best != null && bestD <= 0.5f && _networkConfiguratorForm is NetworkConfiguratorDialog dlg)
        {
            // Ctrl/Shift — «и» (добавить к выделению), без модификатора — «или» (заменить)
            bool add = (ModifierKeys & (Keys.Shift | Keys.Control)) != 0;
            dlg.SelectEntity(best, add);
        }
    }


    private string GetPipeTypeFromTool(ToolManager.Tool tool)
    {
        return tool switch
        {
            ToolManager.Tool.PipeDistra => "Distra",
            ToolManager.Tool.PipeWaste => "Waste",
            ToolManager.Tool.PipeNormal => "Normal",
            ToolManager.Tool.PipeUtil => "Util",
            _ => "Distra"
        };
    }


    private void UpdateTypeLabel()
    {
        // Больше не используется — тип комнаты выбирается через ComboBox
    }
}
