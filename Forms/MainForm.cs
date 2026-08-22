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
    private Panel _toolPanel = null!;
    private Button _btnCreateRoom = null!;
    private Button _btnDelete = null!;
    private Button _btnRoomSettings = null!;
    private Button _btnAirlock = null!;
    private Button _btnAirlockGlass = null!;
    private Button _btnSubtractRoom = null!;
    private Button _btnPipeDistra = null!;
    private Button _btnPipeWaste = null!;
    private Button _btnPipeNormal = null!;
    private ComboBox _gridSelector = null!;
    private ComboBox _repoSelector = null!;
    private Button _btnAddRepo = null!;
    private Button _btnRemoveRepo = null!;
    private Button _btnIndexRepo = null!;
    private ListBox _protoList = null!;
    private TextBox _searchBox = null!;
    private ComboBox _filterCombo = null!;
    private string _currentFilter = "all";

    private Form? _roomTypeForm = null;
    private Label _typeLabel = null!;
    private CancellationTokenSource? _searchCts;
    private bool _hideRoomOverlay = false;
    private bool _showPipeOverlay = true;
    private Dictionary<string, PipeSettings> _pipeLayers = new(PipeSettings.DefaultLayers);
    private Form? _pipeSettingsForm = null;
    private Button _btnPipeSettings = null!;
    private bool _snapToGrid = true;
    private Dictionary<string, AlarmSettings> _alarmSettings = new(AlarmSettings.DefaultAlarms);
    private Form? _alarmSettingsForm = null;
    private Button _btnAlarmSettings = null!;
    private string _currentPipeLayer = "Distra";
    private Button _btnAirAlarm = null!;
    private Button _btnFireAlarm = null!;
    private float _currentAlarmRotation = 0;
    private Button _btnDeleteArea = null!;
    private Button _btnDeleteSettings = null!;
    private Point _deleteStartPoint;
    private Point? _deleteEndPoint;
    private bool _isDeletingArea = false;
    private DeleteSettings _deleteSettings = new DeleteSettings();
    private Form? _deleteSettingsForm = null;
    private bool _showAlarmConnections = true;
    private Point _lastMousePosition;
    private bool _showAlarmPreview = false;
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

    // ===== Инструмент "Перемещение" =====
    private Button? _btnMove;
    private Button? _btnMoveSettings;
    private Button? _btnDecalRule;
    private MoveSettings _moveSettings = new MoveSettings();
    private Form? _moveSettingsForm = null;
    private List<object> _selectedObjects = new();
    private (int x, int y)? _lastClickTile = null;

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
    // ===== Интерактивное редактирование ручной области декалей на канвасе =====
    private ManualDecalArea? _editingDecalArea = null;
    private Room? _editingDecalAreaRoom = null;
    private Action? _editingDecalAreaApplyCallback = null;
    private int _draggingDecalCornerIndex = -1;
    private bool _isDraggingDecalWholeArea = false;
    private ManualDecalArea? _decalAreaDragSnapshot = null;
    private (int x, int y) _decalAreaDragStartMouseTile;


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
            PlacedTile => _moveSettings.IncludeTiles,
            PlacedDecal => _moveSettings.IncludeDecals,
            PipeEntity => _moveSettings.IncludePipes,
            AirAlarmEntity => _moveSettings.IncludeAlarms,
            FireAlarmEntity => _moveSettings.IncludeAlarms,
            FirelockEntity => _moveSettings.IncludeFirelocks,
            MapEntity e when e.GetType() == typeof(MapEntity) => _moveSettings.IncludeEntities,
            MapEntity => _moveSettings.IncludeOther,
            _ => true
        };
    }


    public MainForm()
    {
        Text = "MapperIce";
        Size = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _pipeTypeManager = new PipeTypeManager();
        _decalPatternBuilder = new DecalPatternBuilder(_decalPackManager);
        _pipeBuilder = new PipeBuilder(_pipeTypeManager);
        _doorUpdater = new DoorUpdater(_roomTypeManager);
        _tileBuilder = new TileBuilder(_roomTypeManager, _doorUpdater);
        _tileGrid = new TileGrid();
        _renderer = new Renderer(Width, Height, _indexer, _tileBuilder, _pipeBuilder);

        CreateRepositoryPanel();
        CreateToolPanel();
        CreateGridPanel();
        CreateCanvas();
        CreateMenu();

        _toolManager.ToolChanged += OnToolChanged;

        var defaultGrid = new Grid
        {
            Uid = 2,
            Name = "Грид 2",
            Position = new PointF(0, 0),
            Color = Color.Blue
        };
        _map.AddGrid(defaultGrid);
        UpdateGridSelector();

        _repoManager.OnRepositoriesChanged += () => { UpdateRepoSelector(); };
        _indexer.OnIndexingComplete += () =>
        {
            UpdatePrototypeList();
            UpdateDoorIcons();
            Render();
        };

        UpdateRepoSelector();
        LoadDoorIcons();
        SaveState();
        UpdateBuffer();
    }


    // === UNDO/REDO ===
    private void SaveState()
    {
        if (_map.ActiveGrid == null) return;
        _undo.AddState(_map.ActiveGrid);
    }


    private void RestoreState(GridSnapshot snapshot)
    {
        if (_map.ActiveGrid == null) return;
        snapshot.RestoreTo(_map.ActiveGrid);

        // Объекты, выделенные инструментом "Перемещение", ссылаются на старые экземпляры,
        // которые после отката пересозданы или удалены — сбрасываем выделение, чтобы
        // не остаться с "призрачной" рамкой на несуществующих объектах
        _selectedObjects.Clear();
        _lastClickTile = null;
        _isMovingSelection = false;
        _moveSnapshot.Clear();
        _renderer.SetSelection(_selectedObjects);

        UpdateTileGrid();
        Render();
    }


    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _editingDecalArea != null)
        {
            EndEditDecalArea();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Z))
        {
            if (_undo.CanUndo)
            {
                var snapshot = _undo.Undo();
                RestoreState(snapshot);
            }
            return true;
        }

        if (keyData == (Keys.Control | Keys.Y))
        {
            if (_undo.CanRedo)
            {
                var snapshot = _undo.Redo();
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
    private void RecalculateDecalPatterns()
    {
        if (_map.ActiveGrid == null) return;
        _decalPatternBuilder.RecalculateAll(_map.ActiveGrid);
    }

    // === ХОЛСТ ===
    private void CreateCanvas()
    {
        _canvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _canvas.MouseDown += OnMouseDown;
        _canvas.MouseMove += OnMouseMove;
        _canvas.MouseUp += OnMouseUp;
        _canvas.Paint += OnPaint;
        _canvas.Resize += OnResize;
        _canvas.MouseWheel += OnMouseWheel;
        _canvas.MouseLeave += OnMouseLeave;
        Controls.Add(_canvas);
    }


    // === МЕНЮ ===
    private void CreateMenu()
    {
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("Файл");
        fileMenu.DropDownItems.Add("Сохранить проект", null, (s, e) => SaveProject());
        fileMenu.DropDownItems.Add("Загрузить проект", null, (s, e) => LoadProject());
        fileMenu.DropDownItems.Add("Экспорт в YAML", null, (s, e) => ExportToYAML());
        fileMenu.DropDownItems.Add("Загрузить карту (YAML)", null, (s, e) => LoadMapFromYAML());
        menu.Items.Add(fileMenu);

        Controls.Add(menu);
        MainMenuStrip = menu;

        var toolStrip = new ToolStrip();
        toolStrip.Items.Add(new ToolStripButton("Сбросить вид", null, (s, e) =>
        {
            _scale = 1.0f;
            _viewOffset = new PointF(0, 0);
            Render();
        }));
        Controls.Add(toolStrip);
    }


    // === ОТРИСОВКА ===
    private void OnToolChanged(ToolManager.Tool tool)
    {
        _btnCreateRoom.BackColor = Color.White;
        _btnSubtractRoom.BackColor = Color.White;
        _btnDelete.BackColor = Color.White;
        _btnDeleteArea.BackColor = Color.White;
        _btnDeleteSettings.BackColor = Color.White;
        _btnAirlock.BackColor = Color.White;
        _btnAirlockGlass.BackColor = Color.White;
        _btnPipeDistra.BackColor = Color.White;
        _btnPipeWaste.BackColor = Color.White;
        _btnPipeNormal.BackColor = Color.White;
        if (_btnMove != null) _btnMove.BackColor = Color.White;

        // Наследование декалей — не инструмент канвы, но переключение НА любой
        // инструмент (включая "Узор по периметру") должно закрывать это окно,
        // чтобы два режима не путались и не оставались активными одновременно
        if (_decalInheritanceForm != null && !_decalInheritanceForm.IsDisposed)
            _decalInheritanceForm.Close();
        if (_btnDecalRule != null) _btnDecalRule.BackColor = Color.White;
        if (_btnAirAlarm != null) _btnAirAlarm.BackColor = Color.White;
        if (_btnFireAlarm != null) _btnFireAlarm.BackColor = Color.White;
        if (tool != ToolManager.Tool.PlacePrototype) _protoToPlace = null;

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

        switch (tool)
        {
            case ToolManager.Tool.CreateRoom:
                _btnCreateRoom.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Комната: {_roomTypeManager.SelectedType}, ур: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}";
                break;
            case ToolManager.Tool.SubtractRoom:
                _btnSubtractRoom.BackColor = Color.LightBlue;
                _typeLabel.Text = "Вычитание: выделите область, которую вырезать из существующих комнат";
                break;
            case ToolManager.Tool.Delete:
                _btnDelete.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Удаление (клик по объекту)";
                break;
            case ToolManager.Tool.DeleteArea:
                _btnDeleteArea.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Удаление области (выделите прямоугольник)";
                break;
            case ToolManager.Tool.DeleteSettings:
                _btnDeleteSettings.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Настройки удаления";
                break;
            case ToolManager.Tool.Door:
                _btnAirlock.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Дверь: Airlock";
                break;
            case ToolManager.Tool.DoorGlass:
                _btnAirlockGlass.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Дверь: AirlockGlass";
                break;
            case ToolManager.Tool.PipeDistra:
                _btnPipeDistra.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Труба: Distra";
                break;
            case ToolManager.Tool.PipeWaste:
                _btnPipeWaste.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Труба: Waste";
                break;
            case ToolManager.Tool.PipeNormal:
                _btnPipeNormal.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Труба: Normal";
                break;
            case ToolManager.Tool.AirAlarm:
                if (_btnAirAlarm != null) _btnAirAlarm.BackColor = Color.LightBlue;
                float airDegrees = _currentAlarmRotation * 180 / (float)Math.PI;
                _typeLabel.Text = $"Воздушная сигнализация: {airDegrees:F0}° (СКМ для вращения)";
                break;
            case ToolManager.Tool.FireAlarm:
                if (_btnFireAlarm != null) _btnFireAlarm.BackColor = Color.LightBlue;
                float fireDegrees = _currentAlarmRotation * 180 / (float)Math.PI;
                _typeLabel.Text = $"Пожарная сигнализация: {fireDegrees:F0}° (СКМ для вращения)";
                break;
            case ToolManager.Tool.PlacePrototype:
                float protoDegrees = _currentEntityRotation * 180 / (float)Math.PI;
                _typeLabel.Text = $"Размещение: {_protoToPlace}  {protoDegrees:F0}° (CTRL+колесо — вращение)";
                break;
            case ToolManager.Tool.Move:
                if (_btnMove != null) _btnMove.BackColor = Color.LightBlue;
                _typeLabel.Text = $"Перемещение: выделено {_selectedObjects.Count}  (ЛКМ — выбрать, CTRL — добавить, SHIFT — область)";
                break;
            case ToolManager.Tool.DecalRule:
                if (_btnDecalRule != null) _btnDecalRule.BackColor = Color.LightBlue;
                _typeLabel.Text = "Decal Rule: кликните по комнате, чтобы настроить узор";
                break;

            default:
                _typeLabel.Text = $"Комната: {_roomTypeManager.SelectedType}, ур: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}";
                break;
        }

        Cursor = tool switch
        {
            ToolManager.Tool.CreateRoom or ToolManager.Tool.SubtractRoom => Cursors.Cross,
            ToolManager.Tool.Delete or ToolManager.Tool.DeleteArea or ToolManager.Tool.DeleteSettings => Cursors.Hand,
            ToolManager.Tool.DecalRule => Cursors.Hand,
            ToolManager.Tool.Door or ToolManager.Tool.DoorGlass => Cursors.Help,
            ToolManager.Tool.PipeDistra or ToolManager.Tool.PipeWaste or ToolManager.Tool.PipeNormal => Cursors.Help,
            ToolManager.Tool.AirAlarm or ToolManager.Tool.FireAlarm => Cursors.Help,
            _ => Cursors.Default
        };

        Render();
    }


    private void OnResize(object? sender, EventArgs e)
    {
        _renderer.Resize(_canvas.Width, _canvas.Height);
        Render();
    }


    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (_renderer != null)
        {
            e.Graphics.DrawImage(_renderer.Render(_map, _scale, _viewOffset, _currentRoom,
                _toolManager.CurrentTool.ToString()), 0, 0);
        }
    }


    private void Render()
    {
        _renderer.HideRoomOverlay = _hideRoomOverlay;
        _renderer.ShowPipeOverlay = _showPipeOverlay;
        _renderer.ShowAlarmConnections = _showAlarmConnections;

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
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _map.ActiveGrid.Position.Y * tileSize;
        float worldX = (mouseLocation.X + _viewOffset.X - gridOffsetX) / tileSize;
        float worldY = (mouseLocation.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        return ((int)Math.Floor(worldX), (int)Math.Floor(worldY));
    }


    private (float x, float y) GetPrecisePosition(Point mouseLocation)
    {
        if (_map.ActiveGrid == null) return (0f, 0f);

        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _map.ActiveGrid.Position.Y * tileSize;
        float worldX = (mouseLocation.X + _viewOffset.X - gridOffsetX) / tileSize;
        float worldY = (mouseLocation.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        return (worldX, worldY);
    }


    private string GetPipeTypeFromTool(ToolManager.Tool tool)
    {
        return tool switch
        {
            ToolManager.Tool.PipeDistra => "Distra",
            ToolManager.Tool.PipeWaste => "Waste",
            ToolManager.Tool.PipeNormal => "Normal",
            _ => "Distra"
        };
    }


    private void UpdateTypeLabel()
    {
        if (_typeLabel != null)
            _typeLabel.Text = $"Тип: {_roomTypeManager.SelectedType}  Приоритет: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}";
    }
}
