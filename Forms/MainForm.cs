using MapperIce.Models;
using MapperIce.Services;
using System.Text.Json;

namespace MapperIce.Forms;

public class MainForm : Form
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
        }
    }

    // === ПАНЕЛЬ РЕПОЗИТОРИЕВ ===
    private void CreateRepositoryPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None
        };

        var rightLine = new Panel
        {
            Location = new Point(panel.Width - 1, 0),
            Width = 1,
            Height = panel.Height,
            BackColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
        };
        panel.Controls.Add(rightLine);

        var listContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };
        _protoList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            IntegralHeight = false
        };
        _protoList.DoubleClick += OnPrototypeDoubleClick;
        listContainer.Controls.Add(_protoList);
        panel.Controls.Add(listContainer);

        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 2, 0, 2)
        };

        _searchBox = new TextBox
        {
            Location = new Point(3, 2),
            Width = 155,
            Height = 22,
            Text = "Поиск прототипов...",
            Enabled = false
        };
        _searchBox.KeyUp += (s, e) => UpdatePrototypeList(_searchBox.Text);
        _searchBox.Enter += (s, e) => { if (_searchBox.Text == "Поиск прототипов...") _searchBox.Text = ""; };
        _searchBox.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(_searchBox.Text)) _searchBox.Text = "Поиск прототипов..."; };
        searchPanel.Controls.Add(_searchBox);

        _filterCombo = new ComboBox
        {
            Location = new Point(163, 2),
            Width = 75,
            Height = 22,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8),
            Enabled = false
        };


        _filterCombo.Items.AddRange(new object[] { "Все", "Тайлы", "Структура", "Спавнер", "Декали" });
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (s, e) =>
        {
            _currentFilter = _filterCombo.SelectedItem?.ToString()?.ToLower() ?? "all";
            UpdatePrototypeList(_searchBox.Text);
        };
        searchPanel.Controls.Add(_filterCombo);
        panel.Controls.Add(searchPanel);

        // ============================================================
        // ПАНЕЛЬ КНОПОК РЕПОЗИТОРИЯ
        // ============================================================
        var btnPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(0, 2, 0, 2) };

        // ============================================================
        // СТРОКА 1: Управление репозиториями
        // ============================================================
        _btnAddRepo = new Button { Text = "➕", Location = new Point(5, 5), Width = 30, Height = 25 };
        _btnAddRepo.Click += (s, e) => AddRepository();
        btnPanel.Controls.Add(_btnAddRepo);

        _btnRemoveRepo = new Button { Text = "🗑", Location = new Point(40, 5), Width = 30, Height = 25, Enabled = false };
        _btnRemoveRepo.Click += (s, e) => RemoveRepository();
        btnPanel.Controls.Add(_btnRemoveRepo);

        _btnIndexRepo = new Button
        {
            Text = "🔄 Обновить",
            Location = new Point(75, 5),
            Width = 40,
            Height = 25,
            Enabled = false,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat
        };
        _btnIndexRepo.Click += (s, e) => IndexSelectedRepository();
        btnPanel.Controls.Add(_btnIndexRepo);

        // ============================================================
        // СТРОКА 2: Размещение прототипа + центрирование + настройки
        // ============================================================
        _btnPlaceProto = new Button
        {
            Text = "🔒",
            Location = new Point(3, 35),
            Width = 60,
            Height = 25,
            Enabled = false,
            BackColor = Color.FromArgb(220, 220, 220),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };

        _btnPlaceProto.EnabledChanged += (s, e) =>
        {
            if (_btnPlaceProto.Enabled)
            {
                _btnPlaceProto.Text = "➕";
                _btnPlaceProto.BackColor = Color.FromArgb(255, 245, 200);
                _btnPlaceProto.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }
            else
            {
                _btnPlaceProto.Text = "🔒";
                _btnPlaceProto.BackColor = Color.FromArgb(220, 220, 220);
                _btnPlaceProto.Font = new Font("Segoe UI", 8);
            }
        };

        _btnPlaceProto.Click += (s, e) => ArmPrototypePlacement();
        btnPanel.Controls.Add(_btnPlaceProto);

        _btnSnapEntityCenter = new Button
        {
            Text = "🔲",
            Location = new Point(68, 35),
            Width = 60,
            Height = 25,
            BackColor = _snapEntityToCenter ? Color.LightGreen : Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        _btnSnapEntityCenter.Click += (s, e) =>
        {
            _snapEntityToCenter = !_snapEntityToCenter;
            _btnSnapEntityCenter.BackColor = _snapEntityToCenter ? Color.LightGreen : Color.White;
        };
        btnPanel.Controls.Add(_btnSnapEntityCenter);

        _btnCenterSettings = new Button
        {
            Text = "⚙ 0.5/0.5",
            Location = new Point(133, 35),
            Width = 60,
            Height = 25,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnCenterSettings.Click += (s, e) => ShowCenterSettingsDialog();
        btnPanel.Controls.Add(_btnCenterSettings);








        _btnEntityRotationSnap = new Button
        {
            Text = "📐",
            Location = new Point(198, 35),
            Width = 30,
            Height = 25,
            BackColor = _snapEntityRotation ? Color.LightGreen : Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnEntityRotationSnap.Click += (s, e) =>
        {
            _snapEntityRotation = !_snapEntityRotation;
            _btnEntityRotationSnap.BackColor = _snapEntityRotation ? Color.LightGreen : Color.White;

            if (_snapEntityRotation)
            {
                float step = (float)(Math.PI / 2);
                _currentEntityRotation = (float)(Math.Round(_currentEntityRotation / step) * step);
                Render();
            }
        };
        btnPanel.Controls.Add(_btnEntityRotationSnap);





        // ============================================================
        // КОНЕЦ ПАНЕЛИ КНОПОК
        // ============================================================
        panel.Controls.Add(btnPanel);

        _protoList.SelectedIndexChanged += (s, e) =>
        {
            var id = _protoList.SelectedItem?.ToString();
            bool valid = !string.IsNullOrEmpty(id) &&
                         !id.StartsWith("(") && !id.StartsWith("⚠") &&
                         !id.StartsWith("⏳") && !id.StartsWith("Ошибка") &&
                         !id.StartsWith("Нажмите");
            if (_btnPlaceProto != null) _btnPlaceProto.Enabled = valid;

            if (valid && _toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                _protoToPlace = id;
                _typeLabel.Text = $"Размещение: {_protoToPlace}  (клик — поставить)";
            }
        };

        _repoSelector = new ComboBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 5, 0, 5)
        };
        _repoSelector.SelectedIndexChanged += OnRepoSelected;
        panel.Controls.Add(_repoSelector);

        var title = new Label
        {
            Text = "Репозитории",
            Font = new Font("Arial", 12, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(title);

        Controls.Add(panel);
    }

    private void UpdateRepoSelector()
    {
        _repoSelector.Items.Clear();
        foreach (var repo in _repoManager.Repositories)
        {
            _repoSelector.Items.Add(repo);
        }
        if (_repoSelector.Items.Count > 0)
        {
            var savedId = _repoManager.SelectedRepositoryId;
            var match = _repoManager.Repositories.FirstOrDefault(r => r.Id == savedId);
            _repoSelector.SelectedItem = match ?? _repoManager.Repositories[0];
        }
        else
        {
            _protoList.Items.Clear();
            _protoList.Items.Add("(нет репозиториев)");
            _searchBox.Enabled = false;
            _filterCombo.Enabled = false;
            _btnRemoveRepo.Enabled = false;
            _btnIndexRepo.Enabled = false;
        }
    }

    private void OnRepoSelected(object? sender, EventArgs e)
    {
        var repo = _repoSelector.SelectedItem as Repository;
        bool hasRepo = repo != null;

        _btnRemoveRepo.Enabled = hasRepo;
        _btnIndexRepo.Enabled = hasRepo;
        _searchBox.Enabled = hasRepo;
        _filterCombo.Enabled = hasRepo;

        if (hasRepo)
        {
            _repoManager.SetSelectedRepository(repo!.Id);
        }

        if (hasRepo && repo!.IsIndexed)
        {
            _indexer.IndexRepository(repo);
        }
        else if (hasRepo)
        {
            _protoList.Items.Clear();
            _protoList.Items.Add("⚠️ Репозиторий не \nпроиндексирован");
            _protoList.Items.Add("Нажмите 'Обновить' \nдля загрузки");
        }
    }

    private void AddRepository()
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = @"Выберите репозиторий, например D:\_Goob-Station";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _repoManager.AddRepository(dialog.SelectedPath);
        }
    }

    private void RemoveRepository()
    {
        if (_repoSelector.SelectedItem is Repository repo)
        {
            if (MessageBox.Show($"Удалить репозиторий '{repo.Name}'?", "Подтверждение",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _repoManager.RemoveRepository(repo.Id);
            }
        }
    }

    private void IndexSelectedRepository()
    {
        if (_repoSelector.SelectedItem is Repository repo)
        {
            _indexer.ReindexFromDisk(repo);
            int count = _indexer.GetPrototypeIds().Count;
            _repoManager.MarkAsIndexed(repo.Id, count);
            UpdateRepoSelector();

            UpdateDoorIcons();

            MessageBox.Show($"Проиндексировано {count} прототипов");
        }
    }

    private void UpdatePrototypeList(string filter = "")
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _protoList.Items.Clear();
        _protoList.Items.Add("⏳ Поиск...");

        Task.Run(() =>
        {
            try
            {
                var allIds = string.IsNullOrEmpty(filter) || filter == "Поиск прототипов..."
                    ? _indexer.GetPrototypeIds()
                    : _indexer.SearchPrototypes(filter);

                if (token.IsCancellationRequested) return;

                var filteredIds = allIds;

                switch (_currentFilter)
                {
                    case "all":
                        // Декали по умолчанию скрыты из общего списка — слишком много "мусора"
                        // (сотни BrickTile*, RoadLine* и т.п.), видны только через отдельный фильтр
                        filteredIds = allIds.Where(id =>
                            _indexer.FindPrototype(id)?.Type != "decal"
                        ).ToList();
                        break;
                    case "тайл":
                    case "tiles":
                        filteredIds = allIds.Where(id =>
                            id.Contains("tile", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("floor", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("plating", StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        break;
                    case "структура":
                    case "structures":
                        filteredIds = allIds.Where(id =>
                            id.Contains("wall", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("door", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("window", StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        break;
                    case "спавнер":
                    case "spawner":
                        filteredIds = allIds.Where(id =>
                            id.Contains("spawn", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("spawner", StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        break;
                    case "декали":
                    case "decals":
                        // Декали ищем по реальному Type из YAML (type: decal), а не по подстроке в id —
                        // имена декалей (BrickTileDarkBox и т.п.) никак не намекают на то, что это декаль
                        filteredIds = allIds.Where(id =>
                            _indexer.FindPrototype(id)?.Type == "decal"
                        ).ToList();
                        break;
                }




                filteredIds = filteredIds
                    .Where(id => !id.StartsWith("*"))           // исключаем начинающиеся с *
                    .Where(id => !id.Contains("Action"))        // исключаем содержащие "Action"
                    .ToList();

                var result = filteredIds.Take(100000).ToList();

                if (token.IsCancellationRequested) return;

                _protoList.Invoke(() =>
                {
                    _protoList.Items.Clear();
                    if (result.Count == 0)
                        _protoList.Items.Add("(нет прототипов)");
                    else
                        foreach (var id in result)
                            _protoList.Items.Add(id);
                });
            }
            catch (Exception ex)
            {
                _protoList.Invoke(() =>
                {
                    _protoList.Items.Clear();
                    _protoList.Items.Add($"Ошибка: {ex.Message}");
                });
            }
        }, token);
    }

    private void OnPrototypeDoubleClick(object? sender, EventArgs e)
    {
        if (_protoList.SelectedItem == null) return;
        string? id = _protoList.SelectedItem.ToString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

        var proto = _indexer.FindPrototype(id);
        var path = _indexer.GetFullTexturePath(id);

        bool fileExists = path != null && File.Exists(path);

        string message = $"ID: {id}\n";
        message += $"SpritePath: {proto?.SpritePath ?? "(нет)"}\n";
        message += $"FilePath: {proto?.FilePath ?? "(нет)"}\n";
        message += $"\n--- АВТОМАТИЧЕСКИЙ ПУТЬ ---\n{path ?? "НЕ НАЙДЕН"}\n";
        message += $"Файл существует: {(fileExists ? "✅ ДА" : "❌ НЕТ")}";

        MessageBox.Show(message, "Информация о прототипе");
    }

    // === ПАНЕЛЬ ИНСТРУМЕНТОВ ===
    private void CreateToolPanel()
    {
        _toolPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.None,
            Padding = new Padding(0)
        };

        var leftLine = new Panel
        {
            Dock = DockStyle.Left,
            Width = 1,
            BackColor = Color.Gray
        };
        _toolPanel.Controls.Add(leftLine);

        int leftMargin = 2;
        int rightMargin = 2;
        int y = leftMargin;
        int contentWidth = 200 - leftMargin - rightMargin;

        var title = new Label
        {
            Text = "Инструменты",
            Font = new Font("Arial", 14, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 35,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(220, 220, 220)
        };
        _toolPanel.Controls.Add(title);
        y += 35 + 2;

        _typeLabel = new Label
        {
            Text = $"Комната: {_roomTypeManager.SelectedType}, ур: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DarkGray,
            Font = new Font("Arial", 8)
        };
        _toolPanel.Controls.Add(_typeLabel);
        y += 25 + 2;

        _btnCreateRoom = new Button
        {
            Text = "🟦 Создать",
            Location = new Point(leftMargin + 2, y),
            Width = 149,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnCreateRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.CreateRoom);
        };
        _toolPanel.Controls.Add(_btnCreateRoom);

        _btnRoomSettings = new Button
        {
            Text = "⚙",
            Location = new Point(leftMargin + 151 + 2, y),
            Width = contentWidth - 155,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnRoomSettings.Click += (s, e) => ShowRoomTypeDialog();
        _toolPanel.Controls.Add(_btnRoomSettings);
        y += 40 + 2;

        // ДВЕРИ
        var doorPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        _btnAirlock = new Button
        {
            Location = new Point(0, 0),
            Width = (doorPanel.Width / 2) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Airlock",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "",
            Padding = new Padding(0)
        };
        _btnAirlock.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Door);
        };
        doorPanel.Controls.Add(_btnAirlock);

        _btnAirlockGlass = new Button
        {
            Location = new Point((doorPanel.Width / 2) + 1, 0),
            Width = (doorPanel.Width / 2) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "AirlockGlass",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "",
            Padding = new Padding(0)
        };
        _btnAirlockGlass.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.DoorGlass);
        };
        doorPanel.Controls.Add(_btnAirlockGlass);

        doorPanel.Resize += (s, e) =>
        {
            int halfWidth = doorPanel.Width / 2;
            _btnAirlock.Width = halfWidth - 1;
            _btnAirlockGlass.Location = new Point(halfWidth + 1, 0);
            _btnAirlockGlass.Width = halfWidth - 1;
        };

        _toolPanel.Controls.Add(doorPanel);
        y += 40 + 2;

        // ТРУБЫ
        var pipeLabel = new Label
        {
            Text = "Трубы:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(pipeLabel);
        y += 20 + 2;

        var pipePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int buttonWidth = pipePanel.Width / 4;

        _btnPipeDistra = new Button
        {
            Location = new Point(0, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Distra",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "D",
            Padding = new Padding(0)
        };
        _btnPipeDistra.Click += (s, e) =>
        {
            _currentPipeLayer = "Distra";
            _toolManager.SetTool(ToolManager.Tool.PipeDistra);
        };
        pipePanel.Controls.Add(_btnPipeDistra);

        _btnPipeNormal = new Button
        {
            Location = new Point(buttonWidth, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Normal",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "N",
            Padding = new Padding(0)
        };
        _btnPipeNormal.Click += (s, e) =>
        {
            _currentPipeLayer = "Normal";
            _toolManager.SetTool(ToolManager.Tool.PipeNormal);
        };
        pipePanel.Controls.Add(_btnPipeNormal);

        _btnPipeWaste = new Button
        {
            Location = new Point(buttonWidth * 2, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Waste",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "W",
            Padding = new Padding(0)
        };
        _btnPipeWaste.Click += (s, e) =>
        {
            _currentPipeLayer = "Waste";
            _toolManager.SetTool(ToolManager.Tool.PipeWaste);
        };
        pipePanel.Controls.Add(_btnPipeWaste);

        _btnPipeSettings = new Button
        {
            Text = "⚙",
            Location = new Point(buttonWidth * 3, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnPipeSettings.Click += (s, e) => ShowPipeSettingsDialog();
        pipePanel.Controls.Add(_btnPipeSettings);

        pipePanel.Resize += (s, e) =>
        {
            int bw = pipePanel.Width / 4;
            _btnPipeDistra.Width = bw - 1;
            _btnPipeNormal.Location = new Point(bw, 0);
            _btnPipeNormal.Width = bw - 1;
            _btnPipeWaste.Location = new Point(bw * 2, 0);
            _btnPipeWaste.Width = bw - 1;
            _btnPipeSettings.Location = new Point(bw * 3, 0);
            _btnPipeSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(pipePanel);
        y += 40 + 2;

        // === СИГНАЛИЗАЦИЯ ===
        var alarmLabel = new Label
        {
            Text = "Сигнализация:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(alarmLabel);
        y += 20 + 2;

        var alarmPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int alarmButtonWidth = alarmPanel.Width / 3;

        _btnAirAlarm = new Button
        {
            Location = new Point(0, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔊",
            Font = new Font("Segoe UI", 14)
        };
        _btnAirAlarm.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.AirAlarm);
        };
        alarmPanel.Controls.Add(_btnAirAlarm);

        _btnFireAlarm = new Button
        {
            Location = new Point(alarmButtonWidth, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔥",
            Font = new Font("Segoe UI", 14)
        };
        _btnFireAlarm.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.FireAlarm);
        };
        alarmPanel.Controls.Add(_btnFireAlarm);

        _btnAlarmSettings = new Button
        {
            Text = "⚙",
            Location = new Point(alarmButtonWidth * 2, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnAlarmSettings.Click += (s, e) => ShowAlarmSettingsDialog();
        alarmPanel.Controls.Add(_btnAlarmSettings);

        alarmPanel.Resize += (s, e) =>
        {
            int bw = alarmPanel.Width / 3;
            _btnAirAlarm.Width = bw - 1;
            _btnFireAlarm.Location = new Point(bw, 0);
            _btnFireAlarm.Width = bw - 1;
            _btnAlarmSettings.Location = new Point(bw * 2, 0);
            _btnAlarmSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(alarmPanel);
        y += 40 + 2;




        // === ПЕРЕМЕЩЕНИЕ ===
        var moveLabel = new Label
        {
            Text = "Перемещение:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(moveLabel);
        y += 20 + 2;

        var movePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        _btnMove = new Button
        {
            Text = "✥ Переместить",
            Location = new Point(0, 0),
            Width = movePanel.Width - 42,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnMove.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Move);
        };
        movePanel.Controls.Add(_btnMove);

        _btnMoveSettings = new Button
        {
            Text = "⚙",
            Location = new Point(movePanel.Width - 40, 0),
            Width = 40,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnMoveSettings.Click += (s, e) => ShowMoveSettingsDialog();
        movePanel.Controls.Add(_btnMoveSettings);

        movePanel.Resize += (s, e) =>
        {
            _btnMove.Width = movePanel.Width - 42;
            _btnMoveSettings.Location = new Point(movePanel.Width - 40, 0);
        };

        _toolPanel.Controls.Add(movePanel);
        y += 40 + 2;










        // === УДАЛЕНИЕ ===
        var deleteLabel = new Label
        {
            Text = "Удаление:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(deleteLabel);
        y += 20 + 2;

        var deletePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int deleteButtonWidth = deletePanel.Width / 3;

        _btnDelete = new Button
        {
            Text = "🗑",
            Location = new Point(0, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDelete.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Delete);
        };
        deletePanel.Controls.Add(_btnDelete);

        _btnDeleteArea = new Button
        {
            Text = "🧹",
            Location = new Point(deleteButtonWidth, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDeleteArea.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.DeleteArea);
        };
        deletePanel.Controls.Add(_btnDeleteArea);

        _btnDeleteSettings = new Button
        {
            Text = "⚙",
            Location = new Point(deleteButtonWidth * 2, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDeleteSettings.Click += (s, e) => ShowDeleteSettingsDialog();
        deletePanel.Controls.Add(_btnDeleteSettings);

        deletePanel.Resize += (s, e) =>
        {
            int bw = deletePanel.Width / 3;
            _btnDelete.Width = bw - 1;
            _btnDeleteArea.Location = new Point(bw, 0);
            _btnDeleteArea.Width = bw - 1;
            _btnDeleteSettings.Location = new Point(bw * 2, 0);
            _btnDeleteSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(deletePanel);
        y += 40 + 2;

        _toolPanel.Controls.Add(new Label
        {
            Text = "Повторное нажатие\nсбрасывает инструмент",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Arial", 9),
            BackColor = Color.FromArgb(230, 230, 230)
        });

        Controls.Add(_toolPanel);
    }

    private void LoadDoorIcons()
    {
        if (_btnAirlock != null)
        {
            var icon = GetPrototypeIcon("Airlock");
            if (icon != null)
            {
                _btnAirlock.Image = icon;
                _btnAirlock.Text = "";
            }
            else
            {
                _btnAirlock.Text = "🚪";
                _btnAirlock.TextAlign = ContentAlignment.MiddleCenter;
                _btnAirlock.Font = new Font("Segoe UI", 16);
            }
        }

        if (_btnAirlockGlass != null)
        {
            var icon = GetPrototypeIcon("AirlockGlass");
            if (icon != null)
            {
                _btnAirlockGlass.Image = icon;
                _btnAirlockGlass.Text = "";
            }
            else
            {
                _btnAirlockGlass.Text = "🔲";
                _btnAirlockGlass.TextAlign = ContentAlignment.MiddleCenter;
                _btnAirlockGlass.Font = new Font("Segoe UI", 16);
            }
        }
    }

    private Image? GetPrototypeIcon(string protoId)
    {
        try
        {
            var path = _indexer.GetFullTexturePath(protoId);
            if (path != null && File.Exists(path))
            {
                using var original = Image.FromFile(path);
                var icon = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(icon))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, new Rectangle(0, 0, 32, 32));
                }
                return icon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки иконки для {protoId}: {ex.Message}");
        }

        return null;
    }

    private void UpdateDoorIcons()
    {
        if (_btnAirlock != null)
        {
            var icon = GetPrototypeIcon("Airlock");
            if (icon != null)
            {
                _btnAirlock.Image = icon;
                _btnAirlock.Text = "";
            }
        }

        if (_btnAirlockGlass != null)
        {
            var icon = GetPrototypeIcon("AirlockGlass");
            if (icon != null)
            {
                _btnAirlockGlass.Image = icon;
                _btnAirlockGlass.Text = "";
            }
        }
    }

    // === ДИАЛОГ ВЫБОРА ТИПА КОМНАТЫ ===
    private void ShowRoomTypeDialog()
    {
        if (_roomTypeForm != null && !_roomTypeForm.IsDisposed)
        {
            _roomTypeForm.Focus();
            return;
        }

        var dialog = new RoomTypeDialog(_roomTypeManager);

        dialog.OnTypeSelected += (typeName) =>
        {
            UpdateTypeLabel();
            Render();
        };

        dialog.FormClosed += (s, e) =>
        {
            _roomTypeForm = null;
            UpdateTypeLabel();
            Render();
        };

        _roomTypeForm = dialog;
        dialog.Show(this);
    }

    private Button CreateButton(string text, EventHandler click)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
    }

    private void AddRow(TableLayoutPanel table, string labelText, Control control, int row)
    {
        table.Controls.Add(new Label { Text = labelText, AutoSize = true }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void UpdateTreeView(TreeView treeView)
    {
        treeView.Nodes.Clear();
        foreach (var category in _roomTypeManager.GetCategories().OrderBy(c => c.Key))
        {
            var node = new TreeNode(category.Key);
            foreach (var type in category.Value.OrderBy(t => t.Name))
            {
                node.Nodes.Add(new TreeNode(type.Name)
                {
                    Tag = type,
                    ForeColor = type.IsCustom ? Color.Blue : Color.Black
                });
            }
            treeView.Nodes.Add(node);
        }
    }

    // === ПАНЕЛЬ ГРИДОВ ===
    private void CreateGridPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(230, 230, 230),
            Padding = new Padding(10, 5, 10, 5),
            BorderStyle = BorderStyle.FixedSingle
        };

        var label = new Label
        {
            Text = "Грид:",
            Location = new Point(10, 10),
            AutoSize = true
        };
        panel.Controls.Add(label);

        _gridSelector = new ComboBox
        {
            Location = new Point(60, 7),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _gridSelector.SelectedIndexChanged += (s, e) =>
        {
            if (_gridSelector.SelectedItem is Grid grid)
            {
                _map.ActiveGridUid = grid.Uid;
                UpdateTileGrid();
                Render();
            }
        };
        panel.Controls.Add(_gridSelector);

        var btnAddGrid = new Button
        {
            Text = "+",
            Location = new Point(220, 7),
            Width = 30,
            Height = 25
        };
        btnAddGrid.Click += (s, e) =>
        {
            var newUid = _map.Grids.Max(g => g.Uid) + 1;
            var grid = new Grid
            {
                Uid = newUid,
                Name = $"Грид {newUid}",
                Position = new PointF(10, 10),
                Color = Color.FromArgb(
                    Random.Shared.Next(100, 200),
                    Random.Shared.Next(100, 200),
                    Random.Shared.Next(100, 200)
                )
            };
            _map.AddGrid(grid);
            UpdateGridSelector();
            UpdateTileGrid();
            Render();
        };
        panel.Controls.Add(btnAddGrid);

        var btnRemoveGrid = new Button
        {
            Text = "−",
            Location = new Point(255, 7),
            Width = 30,
            Height = 25
        };
        btnRemoveGrid.Click += (s, e) =>
        {
            if (_map.ActiveGrid != null && _map.Grids.Count > 1)
            {
                _map.RemoveGrid(_map.ActiveGrid.Uid);
                UpdateGridSelector();
                UpdateTileGrid();
                Render();
            }
        };
        panel.Controls.Add(btnRemoveGrid);

        var btnToggleOverlay = new Button
        {
            Text = "🗺️",
            Location = new Point(0, 7),
            Width = 40,
            Height = 40,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnToggleOverlay.Click += (s, e) =>
        {
            _hideRoomOverlay = !_hideRoomOverlay;
            btnToggleOverlay.BackColor = _hideRoomOverlay ? Color.LightGray : Color.LightGreen;
            btnToggleOverlay.Text = _hideRoomOverlay ? "❌" : "🗺️";
            Render();
        };
        panel.Controls.Add(btnToggleOverlay);

        var btnTogglePipe = new Button
        {
            Text = "🔧",
            Location = new Point(45, 7),
            Width = 40,
            Height = 40,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnTogglePipe.Click += (s, e) =>
        {
            _showPipeOverlay = !_showPipeOverlay;
            btnTogglePipe.BackColor = _showPipeOverlay ? Color.LightGreen : Color.LightGray;
            btnTogglePipe.Text = _showPipeOverlay ? "🔧" : "❌";
            Render();
        };
        panel.Controls.Add(btnTogglePipe);

        var btnToggleConnections = new Button
        {
            Text = "🔗",
            Location = new Point(90, 7),
            Width = 40,
            Height = 40,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnToggleConnections.Click += (s, e) =>
        {
            _showAlarmConnections = !_showAlarmConnections;
            btnToggleConnections.BackColor = _showAlarmConnections ? Color.LightGreen : Color.LightGray;
            btnToggleConnections.Text = _showAlarmConnections ? "🔗" : "❌";
            Render();
        };
        panel.Controls.Add(btnToggleConnections);

        var btnSnap = new Button
        {
            Text = "🧲",
            Location = new Point(135, 7),
            Width = 40,
            Height = 40,
            BackColor = _snapToGrid ? Color.LightGreen : Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnSnap.Click += (s, e) =>
        {
            _snapToGrid = !_snapToGrid;
            btnSnap.BackColor = _snapToGrid ? Color.LightGreen : Color.LightGray;
            btnSnap.Text = _snapToGrid ? "🧲" : "❌";
            Render();
        };
        panel.Controls.Add(btnSnap);

        Controls.Add(panel);
    }

    private void UpdateGridSelector()
    {
        _gridSelector.Items.Clear();
        foreach (var grid in _map.Grids)
        {
            _gridSelector.Items.Add(grid);
        }
        if (_map.ActiveGrid != null)
        {
            _gridSelector.SelectedItem = _map.ActiveGrid;
        }
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
        _btnDelete.BackColor = Color.White;
        _btnDeleteArea.BackColor = Color.White;
        _btnDeleteSettings.BackColor = Color.White;
        _btnAirlock.BackColor = Color.White;
        _btnAirlockGlass.BackColor = Color.White;
        _btnPipeDistra.BackColor = Color.White;
        _btnPipeWaste.BackColor = Color.White;
        _btnPipeNormal.BackColor = Color.White;
        if (_btnMove != null) _btnMove.BackColor = Color.White;

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

            default:
                _typeLabel.Text = $"Комната: {_roomTypeManager.SelectedType}, ур: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}";
                break;
        }

        Cursor = tool switch
        {
            ToolManager.Tool.CreateRoom => Cursors.Cross,
            ToolManager.Tool.Delete or ToolManager.Tool.DeleteArea or ToolManager.Tool.DeleteSettings => Cursors.Hand,
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
            var networkBuilder = new AlarmNetworkBuilder(_alarmSettings);
            var network = networkBuilder.BuildNetwork(_map.ActiveGrid);
            _renderer.SetAlarmNetwork(network);
        }
        else
        {
            _renderer.SetAlarmNetwork(null);
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

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_canvas.Width == 0 || _canvas.Height == 0) return;
        if (_map.ActiveGrid == null) return;

        if (e.Button == MouseButtons.Right)
        {
            if (_pipeBuilder.IsDrawing)
            {
                _pipeBuilder.ResetDrawing();
                Render();
                return;
            }
            _isPanning = true;
            _panStart = new PointF(e.Location.X, e.Location.Y);
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button == MouseButtons.Middle)
        {
            if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm ||
                _toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
            {
                _currentAlarmRotation += (float)(Math.PI / 2);
                if (_currentAlarmRotation >= (float)(Math.PI * 2))
                    _currentAlarmRotation -= (float)(Math.PI * 2);

                var alarmTilePos = GetTilePosition(_lastMousePosition);  // Переименовано
                string type = _toolManager.CurrentTool == ToolManager.Tool.AirAlarm ? "AirAlarm" : "FireAlarm";
                _renderer.SetAlarmPreview(alarmTilePos.x, alarmTilePos.y, _currentAlarmRotation, type);

                Render();
                return;
            }
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            var tilePos = GetTilePosition(e.Location);
            int tileX = tilePos.x;
            int tileY = tilePos.y;

            if (_toolManager.CurrentTool == ToolManager.Tool.Delete)
            {
                var grid = _map.ActiveGrid;

                // Те же флаги _deleteSettings, что и в DeleteArea: при DeleteAll фильтр
                // не мешает, иначе конкретный чекбокс должен быть включён
                bool canDeletePipes = _deleteSettings.DeleteAll || _deleteSettings.DeletePipes;
                bool canDeleteAlarms = _deleteSettings.DeleteAll || _deleteSettings.DeleteAlarms;
                bool canDeleteRooms = _deleteSettings.DeleteAll || _deleteSettings.DeleteRooms;
                bool canDeleteEntities = _deleteSettings.DeleteAll || _deleteSettings.DeleteEntities;
                bool canDeleteOther = _deleteSettings.DeleteAll || _deleteSettings.DeleteOther;
                bool canDeleteDecals = _deleteSettings.DeleteAll || _deleteSettings.DeleteDecals;

                var alarm = grid.Entities.OfType<AirAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (alarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(alarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                var fireAlarm = grid.Entities.OfType<FireAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (fireAlarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(fireAlarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                var pipe = grid.Entities.OfType<PipeEntity>().FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY);
                if (pipe != null) { if (canDeletePipes) { grid.Entities.Remove(pipe); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Двери и вручную поставленные тайлы всегда удаляемы точечно — как и в области,
                // для них нет отдельных чекбоксов в DeleteSettings
                if (_doorUpdater.TryRemoveDoor(grid, tileX, tileY)) { SaveState(); UpdateTileGrid(); Render(); return; }

                var anyEntity = grid.Entities.FirstOrDefault(e => (int)e.X == tileX && (int)e.Y == tileY);
                if (anyEntity != null)
                {
                    bool isGenericEntity = anyEntity.GetType() == typeof(MapEntity);
                    bool allowed = isGenericEntity ? canDeleteEntities : canDeleteOther;
                    if (allowed) { grid.Entities.Remove(anyEntity); SaveState(); UpdateTileGrid(); Render(); }
                    return;
                }

                var decal = grid.Decals.FirstOrDefault(d => FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY);
                if (decal != null) { if (canDeleteDecals) { grid.Decals.Remove(decal); SaveState(); UpdateTileGrid(); Render(); } return; }

                var placedTile = grid.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
                if (placedTile != null) { grid.Tiles.Remove(placedTile); SaveState(); UpdateTileGrid(); Render(); return; }

                var room = grid.Rooms.FirstOrDefault(r => tileX >= r.X && tileX < r.X + r.Width && tileY >= r.Y && tileY < r.Y + r.Height);
                if (room != null && canDeleteRooms)
                {
                    grid.Rooms.Remove(room);
                    _doorUpdater.RecalculateAllDoors(grid);
                    SaveState(); UpdateTileGrid(); Render(); return;
                }



            }



            else if (_toolManager.CurrentTool == ToolManager.Tool.DeleteArea)
            {
                _isDeletingArea = true;
                _deleteStartPoint = new Point(tileX, tileY);
                _deleteEndPoint = new Point(tileX, tileY);
                Render();
                return;
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.CreateRoom)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _currentRoom = new Room { X = tileX, Y = tileY, Width = 1, Height = 1 };
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.Door)
            {
                var targetRoom = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                string doorProto = targetRoom?.DoorProto ?? "Airlock";

                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, doorProto, out _, _snapToGrid))
                { SaveState(); UpdateTileGrid(); Render(); }
            }


            else if (_toolManager.CurrentTool == ToolManager.Tool.DoorGlass)
            {
                var targetRoom = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                string glassDoorProto = targetRoom?.GlassDoorProto ?? "AirlockGlass";

                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, glassDoorProto, out _, _snapToGrid))
                { SaveState(); UpdateTileGrid(); Render(); }
            }



            else if (_toolManager.CurrentTool == ToolManager.Tool.PipeDistra ||
                     _toolManager.CurrentTool == ToolManager.Tool.PipeWaste ||
                     _toolManager.CurrentTool == ToolManager.Tool.PipeNormal)
            {
                if (!_pipeBuilder.IsDrawing)
                {
                    _pipeBuilder.StartDrawing(tileX, tileY);
                    Render();
                }
                else
                {
                    string pipeType = _toolManager.CurrentTool switch
                    {
                        ToolManager.Tool.PipeDistra => "Distra",
                        ToolManager.Tool.PipeWaste => "Waste",
                        _ => "Normal"
                    };
                    _pipeBuilder.FinishDrawing(_map.ActiveGrid, pipeType);
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }


            else if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                if (!string.IsNullOrEmpty(_protoToPlace))
                {
                    var grid = _map.ActiveGrid;

                    var proto = _indexer.FindPrototype(_protoToPlace);
                    bool isTile = proto != null && proto.Type == "tile";
                    bool isDecal = proto != null && proto.Type == "decal";

                    if (isTile)
                    {
                        var placeTilePos = GetTilePosition(e.Location);  // Переименовано

                        var existing = grid.Tiles.FirstOrDefault(t => t.X == placeTilePos.x && t.Y == placeTilePos.y);
                        if (existing != null)
                            grid.Tiles.Remove(existing);

                        grid.Tiles.Add(new PlacedTile { X = placeTilePos.x, Y = placeTilePos.y, Proto = _protoToPlace });
                    }



                    else if (isDecal)
                    {
                        // Декали кладём только там, где есть пол — так же, как трубы
                        // (PipeBuilder.HasFloorAt) и сигнализации (AddAirAlarm/AddFireAlarm)
                        // не ставятся в пустоте. Проверяем по тайлу под курсором, а не по
                        // точной дробной координате, иначе декаль у самого края комнаты
                        // могла бы формально попасть "мимо" пола из-за округления
                        var floorCheckTile = GetTilePosition(e.Location);
                        if (!HasFloorAt(grid, floorCheckTile.x, floorCheckTile.y))
                        {
                            return;
                        }

                        // Декали — не ECS-сущности в игре, поэтому кладём их в отдельный
                        // список, а не в grid.Entities. Иначе при экспорте они попадут в
                        // entities: как обычный прототип и вызовут "Missing prototype",
                        // так как decal-id не зарегистрирован как id сущности
                        float decalX, decalY;
                        if (_snapEntityToCenter)
                        {
                            var centerTile = GetTilePosition(e.Location);
                            decalX = centerTile.x + _centerOffset.X;
                            decalY = centerTile.y + _centerOffset.Y;
                        }
                        else
                        {
                            var precise = GetPrecisePosition(e.Location);
                            decalX = precise.x;
                            decalY = precise.y;
                        }

                        grid.Decals.Add(new PlacedDecal { X = decalX, Y = decalY, Proto = _protoToPlace, Rotation = _currentEntityRotation, Color = _decalColor, Cleanable = _decalCleanable });
                    }


                    else
                    {
                        float finalX, finalY;
                        if (_snapEntityToCenter)
                        {
                            var centerTile = GetTilePosition(e.Location);
    finalX = centerTile.x + _centerOffset.X;
    finalY = centerTile.y + _centerOffset.Y;
                        }
                        else
                        {
                            var precise = GetPrecisePosition(e.Location);
                            finalX = precise.x;
                            finalY = precise.y;
                        }

                        grid.Entities.Add(new MapEntity { X = finalX, Y = finalY, Proto = _protoToPlace, Rotation = _currentEntityRotation });
                    }

                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }




            else if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm)
            {
                AddAirAlarm(_map.ActiveGrid, tileX, tileY);
                SaveState();
                UpdateTileGrid();
                Render();
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
            {
                AddFireAlarm(_map.ActiveGrid, tileX, tileY);
                SaveState();
                UpdateTileGrid();
                Render();
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.Move)
            {
                var grid = _map.ActiveGrid;
                if (grid == null) return;

                bool shiftHeld = ModifierKeys.HasFlag(Keys.Shift);
                bool ctrlHeld = ModifierKeys.HasFlag(Keys.Control);

                var hit = HitTestAt(tileX, tileY);

                if (shiftHeld && _lastClickTile.HasValue)
                {
                    int minX = Math.Min(_lastClickTile.Value.x, tileX);
                    int maxX = Math.Max(_lastClickTile.Value.x, tileX);
                    int minY = Math.Min(_lastClickTile.Value.y, tileY);
                    int maxY = Math.Max(_lastClickTile.Value.y, tileY);

                    _selectedObjects = GatherObjectsInRect(minX, minY, maxX, maxY);
                    _lastClickTile = (tileX, tileY);
                    _renderer.SetSelection(_selectedObjects);
                    _typeLabel.Text = $"Перемещение: выделено {_selectedObjects.Count}";
                    Render();
                    return;
                }

                if (ctrlHeld)
                {
                    if (hit != null)
                    {
                        if (!_selectedObjects.Contains(hit))
                            _selectedObjects.Add(hit);

                        _lastClickTile = (tileX, tileY);
                        _renderer.SetSelection(_selectedObjects);
                        _typeLabel.Text = $"Перемещение: выделено {_selectedObjects.Count}";
                        Render();
                        return;
                    }

                    // CTRL + клик по пустому месту — начинаем протягивание рамки в АДДИТИВНОМ режиме
                    _isBoxSelecting = true;
                    _boxSelectAdditive = true;
                    _boxStartScreen = e.Location;
                    _boxEndScreen = e.Location;
                    _lastClickTile = (tileX, tileY);
                    Render();
                    return;
                }

                // Без модификаторов
                if (hit != null)
                {
                    if (!_selectedObjects.Contains(hit))
                    {
                        _selectedObjects = new List<object> { hit };
                    }

                    _lastClickTile = (tileX, tileY);
                    _renderer.SetSelection(_selectedObjects);
                    _typeLabel.Text = $"Перемещение: выделено {_selectedObjects.Count}";

                    BeginMoveDrag(e.Location);
                    Render();
                    return;
                }

                // Клик по пустому месту — начинаем протягивание рамки (заменяющий режим)
                _isBoxSelecting = true;
                _boxSelectAdditive = false;
                _boxStartScreen = e.Location;
                _boxEndScreen = e.Location;
                _lastClickTile = (tileX, tileY);
                Render();
            }












        }
    }








    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        bool isPlacingPrototype = _toolManager.CurrentTool == ToolManager.Tool.PlacePrototype;
        bool ctrlHeld = ModifierKeys.HasFlag(Keys.Control);

        if (isPlacingPrototype && !ctrlHeld)
        {
            if (_snapEntityRotation)
            {
                float step = (float)(Math.PI / 2);
                _currentEntityRotation += e.Delta > 0 ? step : -step;
                _currentEntityRotation = (float)(Math.Round(_currentEntityRotation / step) * step);
            }
            else
            {
                float step = (float)(Math.PI / 36); // 5° за "щелчок" колеса
                _currentEntityRotation += e.Delta > 0 ? step : -step;
            }

            float fullCircle = (float)(Math.PI * 2);
            _currentEntityRotation %= fullCircle;
            if (_currentEntityRotation < 0)
                _currentEntityRotation += fullCircle;

            // Обновляем превью немедленно, не дожидаясь движения мыши
            if (!string.IsNullOrEmpty(_protoToPlace))
            {
                float previewX, previewY;
                if (_snapEntityToCenter)
                {
                    var centerTile = GetTilePosition(_lastMousePosition);
                    previewX = centerTile.x + _centerOffset.X;
                    previewY = centerTile.y + _centerOffset.Y;
                }
                else
                {
                    var precise = GetPrecisePosition(_lastMousePosition);
                    previewX = precise.x;
                    previewY = precise.y;
                }

                var wheelProto = _indexer.FindPrototype(_protoToPlace);
                bool wheelIsDecal = wheelProto != null && wheelProto.Type == "decal";
                _renderer.SetEntityPreview(previewX, previewY, _currentEntityRotation, _protoToPlace,
                    wheelIsDecal ? _decalColor : null);
            }


            if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                float protoDegrees = _currentEntityRotation * 180 / (float)Math.PI;
                _typeLabel.Text = $"Размещение: {_protoToPlace}  {protoDegrees:F0}° (колесо — вращение, CTRL+колесо — зум)";
            }

            Render();
            return;
        }

        // Зум: либо инструмент неактивен (CTRL не важен), либо инструмент активен и CTRL зажат
        float zoomDelta = e.Delta > 0 ? 0.1f : -0.1f;
        _scale = Math.Clamp(_scale + zoomDelta, 0.2f, 3.0f);
        Render();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        _lastMousePosition = e.Location;

        if (_isBoxSelecting)
        {
            _boxEndScreen = e.Location;
            _renderer.SetSelectionBox(_boxStartScreen, _boxEndScreen);
            Render();
            return;
        }

        if (_isMovingSelection)
        {
            var current = GetPrecisePosition(e.Location);
            float rawDx = current.x - _moveDragStartWorld.x;
            float rawDy = current.y - _moveDragStartWorld.y;

            // Если в выделении есть комната или вручную поставленный тайл — вся группа
            // двигается целыми шагами (по умолчанию 1 тайл), а не плавно по пикселю
            bool forceSnap = _selectedObjects.Any(o => o is Room || o is PlacedTile);

            float dx, dy;
            if (forceSnap)
            {
                float step = _moveSettings.Step <= 0 ? 1f : _moveSettings.Step;
                dx = (float)(Math.Round(rawDx / step) * step);
                dy = (float)(Math.Round(rawDy / step) * step);
            }
            else
            {
                dx = rawDx;
                dy = rawDy;
            }

            if (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f)
                _moveDidMove = true;

            foreach (var item in _moveSnapshot)
            {
                MoveTarget(item.Target, item.OrigX + dx, item.OrigY + dy);
            }

            UpdateTileGrid();
            Render();
            return;
        }

        if (_isPanning)
        {
            _viewOffset.X -= e.Location.X - _panStart.X;
            _viewOffset.Y -= e.Location.Y - _panStart.Y;
            _panStart = new PointF(e.Location.X, e.Location.Y);
            Render();
            return;
        }

        if (_map.ActiveGrid == null) return;

        if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm ||
            _toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
        {
            var tilePos = GetTilePosition(e.Location);
            string type = _toolManager.CurrentTool == ToolManager.Tool.AirAlarm ? "AirAlarm" : "FireAlarm";
            _renderer.SetAlarmPreview(tilePos.x, tilePos.y, _currentAlarmRotation, type);
            Render();
            return;
        }
        else
        {
            _renderer.ClearAlarmPreview();
        }

        if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype && !string.IsNullOrEmpty(_protoToPlace))
        {
            float previewX, previewY;
            if (_snapEntityToCenter)
            {
                var centerTile = GetTilePosition(e.Location);
                previewX = centerTile.x + _centerOffset.X;
                previewY = centerTile.y + _centerOffset.Y;
            }
            else
            {
                var precise = GetPrecisePosition(e.Location);
                previewX = precise.x;
                previewY = precise.y;
            }

            var moveProto = _indexer.FindPrototype(_protoToPlace);
            bool moveIsDecal = moveProto != null && moveProto.Type == "decal";
            _renderer.SetEntityPreview(previewX, previewY, _currentEntityRotation, _protoToPlace,
                moveIsDecal ? _decalColor : null);
            Render();
            return;
        }



        else
        {
            _renderer.ClearEntityPreview();
        }

        if (_isDeletingArea)
        {
            var tilePos = GetTilePosition(e.Location);
            _deleteEndPoint = new Point(tilePos.x, tilePos.y);
            Render();
            return;
        }

        if (_pipeBuilder.IsDrawing)
        {
            var tilePos = GetTilePosition(e.Location);
            _pipeBuilder.UpdateEndPoint(tilePos.x, tilePos.y);
            Render();
            return;
        }

        if (!_isDrawing || _currentRoom == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _map.ActiveGrid.Position.Y * tileSize;

        float endWorldX = (e.Location.X + _viewOffset.X - gridOffsetX) / tileSize;
        float endWorldY = (e.Location.Y + _viewOffset.Y - gridOffsetY) / tileSize;
        float startWorldX = (_startPoint.X + _viewOffset.X - gridOffsetX) / tileSize;
        float startWorldY = (_startPoint.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        int endX = (int)Math.Floor(endWorldX);
        int endY = (int)Math.Floor(endWorldY);
        int startX = (int)Math.Floor(startWorldX);
        int startY = (int)Math.Floor(startWorldY);

        _currentRoom.X = Math.Min(startX, endX);
        _currentRoom.Y = Math.Min(startY, endY);
        _currentRoom.Width = Math.Abs(endX - startX) + 1;
        _currentRoom.Height = Math.Abs(endY - startY) + 1;

        Render();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            _renderer.ClearSelectionBox();

            int dxPix = Math.Abs(_boxEndScreen.X - _boxStartScreen.X);
            int dyPix = Math.Abs(_boxEndScreen.Y - _boxStartScreen.Y);

            if (dxPix < 3 && dyPix < 3)
            {
                // Слишком маленькое перемещение — это был обычный клик по пустому месту, а не протягивание
                if (!_boxSelectAdditive)
                {
                    _selectedObjects.Clear();
                    _renderer.SetSelection(_selectedObjects);
                    _typeLabel.Text = "Перемещение: выделено 0";
                }
                Render();
                return;
            }

            var startTile = GetTilePosition(_boxStartScreen);
            var endTile = GetTilePosition(_boxEndScreen);

            int minX = Math.Min(startTile.x, endTile.x);
            int maxX = Math.Max(startTile.x, endTile.x);
            int minY = Math.Min(startTile.y, endTile.y);
            int maxY = Math.Max(startTile.y, endTile.y);

            var found = GatherObjectsInRect(minX, minY, maxX, maxY);

            if (_boxSelectAdditive)
            {
                foreach (var obj in found)
                {
                    if (!_selectedObjects.Contains(obj))
                        _selectedObjects.Add(obj);
                }
            }
            else
            {
                _selectedObjects = found;
            }

            _renderer.SetSelection(_selectedObjects);
            _typeLabel.Text = $"Перемещение: выделено {_selectedObjects.Count}";
            Render();
            return;
        }

        if (_isMovingSelection)
        {
            _isMovingSelection = false;

            if (_moveDidMove)
            {
                if (_map.ActiveGrid != null)
                    _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);

                UpdateTileGrid();
                SaveState(); // ← логирование в undo/redo
            }

            _moveSnapshot.Clear();
            _moveDidMove = false;
            Render();
            return;
        }

        if (e.Button == MouseButtons.Right && _isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
            return;
        }

        if (e.Button == MouseButtons.Left && _isDeletingArea && _map.ActiveGrid != null)
        {
            var start = _deleteStartPoint;
            var end = _deleteEndPoint ?? start;

            int minX = Math.Min(start.X, end.X);
            int maxX = Math.Max(start.X, end.X);
            int minY = Math.Min(start.Y, end.Y);
            int maxY = Math.Max(start.Y, end.Y);

            var grid = _map.ActiveGrid;
            var toRemove = new List<MapEntity>();
            var decalsToRemove = new List<PlacedDecal>();

            if (_deleteSettings.DeleteAll)
            {
                toRemove.AddRange(grid.Entities.Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY));
                decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        _doorUpdater.TryRemoveDoor(grid, x, y);

                var rooms = grid.Rooms.Where(r => !(r.X + r.Width <= minX || r.X > maxX || r.Y + r.Height <= minY || r.Y > maxY)).ToList();
                foreach (var room in rooms) grid.Rooms.Remove(room);

                if (rooms.Count > 0)
                    _doorUpdater.RecalculateAllDoors(grid);




            }
            else
            {
                if (_deleteSettings.DeletePipes)
                    toRemove.AddRange(grid.Entities.OfType<PipeEntity>().Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY));

                if (_deleteSettings.DeleteRooms)
                {
                    var roomsToRemove = grid.Rooms
                        .Where(r => !(r.X + r.Width <= minX || r.X > maxX || r.Y + r.Height <= minY || r.Y > maxY))
                        .ToList();
                    foreach (var room in roomsToRemove) grid.Rooms.Remove(room);

                    if (roomsToRemove.Count > 0)
                        _doorUpdater.RecalculateAllDoors(grid);
                }

                if (_deleteSettings.DeleteAlarms)
                {
                    toRemove.AddRange(grid.Entities.OfType<AirAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                    toRemove.AddRange(grid.Entities.OfType<FireAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                }

                if (_deleteSettings.DeleteEntities)
                {
                    var repoEntities = grid.Entities
                        .Where(e => e.GetType() == typeof(MapEntity))
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .ToList();
                    toRemove.AddRange(repoEntities);
                }

                if (_deleteSettings.DeleteOther)
                {
                    var knownTypes = new HashSet<Type>
                    {
                        typeof(PipeEntity), typeof(AirAlarmEntity), typeof(FireAlarmEntity),
                        typeof(FirelockEntity), typeof(MapEntity)
                    };
                    var otherEntities = grid.Entities
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .Where(e => !knownTypes.Contains(e.GetType()))
                        .ToList();
                    toRemove.AddRange(otherEntities);
                }

                if (_deleteSettings.DeleteDecals)
                {
                    decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));
                }
            }

            foreach (var entity in toRemove) grid.Entities.Remove(entity);
            foreach (var decal in decalsToRemove) grid.Decals.Remove(decal);
            SaveState();
            UpdateTileGrid();
            Render();
            _isDeletingArea = false;
            _deleteEndPoint = null;
            return;
        }

        if (e.Button == MouseButtons.Left && _isDrawing && _currentRoom != null && _map.ActiveGrid != null)
        {
            if (_currentRoom.Width > 1 || _currentRoom.Height > 1)
            {
                _roomTypeManager.ApplyTypeToRoom(_currentRoom);
                _map.ActiveGrid.Rooms.Add(_currentRoom);
                _doorUpdater.RecalculateDoorsInRoom(_map.ActiveGrid, _currentRoom); // снять и переставить двери на её территории
                UpdateTileGrid();
                SaveState();
            }
            _currentRoom = null;
            _isDrawing = false;
            Render();
        }
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

    // === ФАЙЛОВЫЕ ОПЕРАЦИИ ===
    private void LoadMapFromYAML()
    {
        MessageBox.Show("Загрузка карт из YAML пока не реализована");
    }

    private void ExportToYAML()
    {
        if (_map.ActiveGrid == null || _map.ActiveGrid.Rooms.Count == 0)
        {
            MessageBox.Show("Нет комнат для экспорта");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "YAML files (*.yml)|*.yml",
            DefaultExt = "yml",
            FileName = "map.yml"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var yaml = YAMLGenerator.Generate(_map.ActiveGrid, _tileBuilder, _pipeLayers, _alarmSettings);
                File.WriteAllText(dialog.FileName, yaml);
                MessageBox.Show($"Карта экспортирована в {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
        }
    }

    private void SaveProject()
    {
        if (_map.ActiveGrid == null)
        {
            MessageBox.Show("Нет активного грида для сохранения");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Project files (*.ice)|*.ice",
            DefaultExt = "ice",
            FileName = "project.ice"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var data = new ProjectData
            {
                LastSaved = DateTime.Now,
                ActiveGridName = _map.ActiveGrid.Name
            };

            // Комнаты и двери
            foreach (var room in _map.ActiveGrid.Rooms)
            {
                var roomData = new RoomData
                {
                    X = room.X,
                    Y = room.Y,
                    Width = room.Width,
                    Height = room.Height,
                    RoomType = room.RoomType,
                    WallProto = room.WallProto,
                    FloorProto = room.FloorProto,
                    DoorProto = room.DoorProto,
                    GlassDoorProto = room.GlassDoorProto,
                    FillColor = $"{room.FillColor.A},{room.FillColor.R},{room.FillColor.G},{room.FillColor.B}",
                    LineColor = $"{room.LineColor.A},{room.LineColor.R},{room.LineColor.G},{room.LineColor.B}"
                };

                foreach (var door in room.Doors)
                {
                    roomData.Doors.Add(new DoorData
                    {
                        X = door.X,
                        Y = door.Y,
                        Proto = door.Proto
                    });
                }

                data.Rooms.Add(roomData);
            }

            // Все сущности грида (трубы, сигнализации, размещённые прототипы и любые будущие типы).
            // Пожарные шлюзы (Firelock) не сохраняем — они пересоздаются автоматически
            // из дверей через DoorUpdater.UpdateAllDoors при загрузке.
            foreach (var entity in _map.ActiveGrid.Entities.Where(e => e is not FirelockEntity))
            {
                data.Entities.Add(new GenericEntityData
                {
                    Type = entity.GetType().Name,
                    Data = System.Text.Json.JsonSerializer.SerializeToElement(entity, entity.GetType())
                });
            }

            data.Tiles = _map.ActiveGrid.Tiles
                .Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto })
                .ToList();

            data.Decals = _map.ActiveGrid.Decals
                .Select(d => new PlacedDecal { X = d.X, Y = d.Y, Proto = d.Proto, Color = d.Color, Rotation = d.Rotation, Cleanable = d.Cleanable })
                .ToList();

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show($"Проект сохранён!\nКомнат: {data.Rooms.Count}\nДверей: {data.Rooms.Sum(r => r.Doors.Count)}\nСущностей: {data.Entities.Count}\nДекалей: {data.Decals.Count}");


        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }

    private void LoadProject()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Project files (*.ice)|*.ice"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var data = System.Text.Json.JsonSerializer.Deserialize<ProjectData>(json);

            if (data == null)
            {
                MessageBox.Show("Ошибка чтения файла");
                return;
            }

            if (_map.ActiveGrid != null)
            {
                _map.ActiveGrid.Rooms.Clear();
                _map.ActiveGrid.Entities.Clear();
                _map.ActiveGrid.Tiles.Clear();
                _map.ActiveGrid.Decals.Clear();
                foreach (var roomData in data.Rooms)
                {
                    var room = new Room
                    {
                        X = roomData.X,
                        Y = roomData.Y,
                        Width = roomData.Width,
                        Height = roomData.Height,
                        RoomType = roomData.RoomType,
                        WallProto = roomData.WallProto,
                        FloorProto = roomData.FloorProto,
                        DoorProto = roomData.DoorProto,
                        GlassDoorProto = roomData.GlassDoorProto ?? "AirlockGlass",
                        FillColor = ParseColor(roomData.FillColor),
                        LineColor = ParseColor(roomData.LineColor)
                    };

                    foreach (var doorData in roomData.Doors)
                    {
                        room.Doors.Add(new Door
                        {
                            X = doorData.X,
                            Y = doorData.Y,
                            Proto = doorData.Proto
                        });
                    }

                    _map.ActiveGrid.Rooms.Add(room);
                }

                int restoredCount = 0;
                foreach (var entityData in data.Entities)
                {
                    if (!EntityTypeRegistry.TryGetType(entityData.Type, out var type))
                        continue; // неизвестный/удалённый тип — пропускаем, не роняем загрузку

                    try
                    {
                        // Исправление здесь:
                        var restored = JsonSerializer.Deserialize(entityData.Data.GetRawText(), type);
                        if (restored is MapEntity mapEntity)
                        {
                            _map.ActiveGrid.Entities.Add(mapEntity);
                            restoredCount++;
                        }
                    }
                    catch
                    {
                        // повреждённая запись — пропускаем
                    }
                }


                foreach (var tileData in data.Tiles)
                {
                    _map.ActiveGrid.Tiles.Add(new PlacedTile { X = tileData.X, Y = tileData.Y, Proto = tileData.Proto });
                }

                foreach (var decalData in data.Decals)
                {
                    _map.ActiveGrid.Decals.Add(new PlacedDecal { X = decalData.X, Y = decalData.Y, Proto = decalData.Proto, Color = decalData.Color, Rotation = decalData.Rotation, Cleanable = decalData.Cleanable });
                }

                _doorUpdater.UpdateAllDoors(_map.ActiveGrid); // пересоздаёт Firelock из дверей
                UpdateTileGrid();
                SaveState();
                Render();

                int totalDoors = data.Rooms.Sum(r => r.Doors.Count);
                MessageBox.Show($"Проект загружен!\nКомнат: {data.Rooms.Count}\nДверей: {totalDoors}\nСущностей: {restoredCount}\nДекалей: {data.Decals.Count}");


            }
            else { MessageBox.Show("Уже что-то в рабочей области"); }

        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }

    private Color ParseColor(string value)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 4)
                return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }
        catch { }
        return Color.FromArgb(200, 230, 230, 230);
    }

    private void ShowPipeSettingsDialog()
    {
        if (_pipeSettingsForm != null && !_pipeSettingsForm.IsDisposed)
        {
            _pipeSettingsForm.Close();
            _pipeSettingsForm = null;
            return;
        }

        _pipeSettingsForm = new Form
        {
            Text = "Настройки слоёв труб",
            Size = new Size(400, 350),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _pipeSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 5,
            ColumnCount = 3,
            AutoSize = true
        };

        panel.Controls.Add(new Label { Text = "Слой", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true }, 0, 0);
        panel.Controls.Add(new Label { Text = "Цвет", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true }, 1, 0);
        panel.Controls.Add(new Label { Text = "", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true }, 2, 0);

        int row = 1;
        var colorButtons = new Dictionary<string, Button>();

        foreach (var layer in _pipeLayers.Keys)
        {
            var settings = _pipeLayers[layer];

            panel.Controls.Add(new Label { Text = settings.DisplayName, AutoSize = true, Font = new Font("Arial", 9) }, 0, row);

            var btnColor = new Button
            {
                BackColor = settings.Color,
                Width = 60,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Tag = layer
            };
            btnColor.Click += (s, e) =>
            {
                using var dialog = new ColorDialog();
                dialog.Color = settings.Color;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    settings.Color = dialog.Color;
                    btnColor.BackColor = dialog.Color;
                    UpdatePipeButtonColors();
                    Render();
                }
            };
            panel.Controls.Add(btnColor, 1, row);
            colorButtons[layer] = btnColor;

            var btnReset = new Button
            {
                Text = "↺",
                Width = 30,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Tag = layer
            };
            btnReset.Click += (s, e) =>
            {
                if (PipeSettings.DefaultLayers.TryGetValue(layer, out var defaultSettings))
                {
                    settings.Color = defaultSettings.Color;
                    if (colorButtons.TryGetValue(layer, out var btn))
                        btn.BackColor = defaultSettings.Color;
                    UpdatePipeButtonColors();
                    Render();
                }
            };
            panel.Controls.Add(btnReset, 2, row);

            row++;
        }

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(btnPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOk.Click += (s, e) => _pipeSettingsForm?.Close();
        btnPanel.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(btnPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.Click += (s, e) =>
        {
            foreach (var layer in _pipeLayers.Keys)
            {
                if (PipeSettings.DefaultLayers.TryGetValue(layer, out var defaultSettings))
                {
                    _pipeLayers[layer].Color = defaultSettings.Color;
                }
            }
            UpdatePipeButtonColors();
            _pipeSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnCancel);

        _pipeSettingsForm.Controls.Add(panel);
        _pipeSettingsForm.Controls.Add(btnPanel);

        _pipeSettingsForm.FormClosed += (s, e) => { _pipeSettingsForm = null; };
        _pipeSettingsForm.Show(this);
    }

    private void UpdatePipeButtonColors()
    {
        if (_btnPipeDistra != null)
        {
            _btnPipeDistra.BackColor = _toolManager.CurrentTool == ToolManager.Tool.PipeDistra ? Color.LightBlue : Color.White;
        }

        if (_btnPipeNormal != null)
        {
            _btnPipeNormal.BackColor = _toolManager.CurrentTool == ToolManager.Tool.PipeNormal ? Color.LightBlue : Color.White;
        }

        if (_btnPipeWaste != null)
        {
            _btnPipeWaste.BackColor = _toolManager.CurrentTool == ToolManager.Tool.PipeWaste ? Color.LightBlue : Color.White;
        }
    }

    private Color GetPipeLayerColor(string layer)
    {
        return _pipeLayers.GetValueOrDefault(layer)?.Color ?? Color.FromArgb(180, 150, 150, 150);
    }

    private string GetPipeHexColor(string layer)
    {
        if (_pipeLayers.TryGetValue(layer, out var settings))
            return settings.HexColor;
        return PipeSettings.DefaultLayers.TryGetValue(layer, out var def) ? def.HexColor : "#FFFFFFFF";
    }

    private bool HasWallAt(Grid grid, int x, int y)
    {
        return grid.Rooms.Any(r =>
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height &&
            (x == r.X || x == r.X + r.Width - 1 ||
             y == r.Y || y == r.Y + r.Height - 1));
    }

    private float GetAlarmRotation(Grid grid, int x, int y)
    {
        var dirs = new[] {
            (0, -1, 0f),
            (0, 1, (float)Math.PI),
            (-1, 0, (float)(Math.PI / 2)),
            (1, 0, (float)(-Math.PI / 2))
        };

        foreach (var (dx, dy, rot) in dirs)
        {
            int cx = x + dx, cy = y + dy;
            if (HasWallAt(grid, cx, cy))
                return rot;
        }
        return _currentAlarmRotation;
    }

    private void AddAirAlarm(Grid grid, int x, int y)
    {
        if (grid == null) return;
        if (grid.Entities.OfType<AirAlarmEntity>().Any(e => (int)e.X == x && (int)e.Y == y)) return;

        if (_snapToGrid)
        {
            if (!HasWallAt(grid, x, y)) return;
            grid.Entities.Add(new AirAlarmEntity { X = x, Y = y, Rotation = _currentAlarmRotation });
        }
        else
        {
            grid.Entities.Add(new AirAlarmEntity { X = x, Y = y, Rotation = _currentAlarmRotation });
        }
    }

    private void AddFireAlarm(Grid grid, int x, int y)
    {
        if (grid == null) return;
        if (grid.Entities.OfType<FireAlarmEntity>().Any(e => (int)e.X == x && (int)e.Y == y)) return;

        if (_snapToGrid)
        {
            if (!HasWallAt(grid, x, y)) return;
            grid.Entities.Add(new FireAlarmEntity { X = x, Y = y, Rotation = _currentAlarmRotation });
        }
        else
        {
            grid.Entities.Add(new FireAlarmEntity { X = x, Y = y, Rotation = _currentAlarmRotation });
        }
    }

    private bool HasFloorAt(Grid grid, int x, int y)
    {
        return grid.Rooms.Any(r =>
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height);
    }

    private void ShowAlarmSettingsDialog()
    {
        if (_alarmSettingsForm != null && !_alarmSettingsForm.IsDisposed)
        {
            _alarmSettingsForm.Close();
            _alarmSettingsForm = null;
            return;
        }

        _alarmSettingsForm = new Form
        {
            Text = "Настройки сигнализации",
            Size = new Size(450, 350),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _alarmSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 4,
            ColumnCount = 2,
            AutoSize = true
        };

        panel.Controls.Add(new Label { Text = "Тип", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true }, 0, 0);
        panel.Controls.Add(new Label { Text = "ID прототипа", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true }, 1, 0);

        int row = 1;

        var tempSettings = new Dictionary<string, AlarmSettings>();
        foreach (var kvp in _alarmSettings)
        {
            tempSettings[kvp.Value.DisplayName] = new AlarmSettings
            {
                Id = kvp.Value.Id,
                DisplayName = kvp.Value.DisplayName,
                Icon = kvp.Value.Icon,
                Color = kvp.Value.Color,
                AutoLinkDevices = true
            };
        }

        foreach (var alarm in tempSettings.Values)
        {
            panel.Controls.Add(new Label { Text = alarm.DisplayName, AutoSize = true, Font = new Font("Arial", 9) }, 0, row);

            var txtId = new TextBox
            {
                Text = alarm.Id,
                Width = 150,
                Tag = alarm.DisplayName
            };
            txtId.TextChanged += (s, e) =>
            {
                if (txtId.Tag is string displayName && tempSettings.TryGetValue(displayName, out var settings))
                {
                    settings.Id = txtId.Text;
                }
            };
            panel.Controls.Add(txtId, 1, row);
            row++;

            var chkAutoLink = new CheckBox
            {
                Text = "Автопривязка устройств",
                Checked = true,
                AutoSize = true,
                Tag = alarm.DisplayName
            };

            chkAutoLink.CheckedChanged += (s, e) =>
            {
                var chk = (CheckBox)s;
                if (chk.Tag is string displayName)
                {
                    if (tempSettings.TryGetValue(displayName, out var settings))
                    {
                        settings.AutoLinkDevices = chk.Checked;
                    }
                }
            };

            panel.Controls.Add(chkAutoLink, 0, row);
            panel.SetColumnSpan(chkAutoLink, 2);
            row++;
        }

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(btnPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOk.Click += (s, e) =>
        {
            foreach (var kvp in tempSettings)
            {
                var original = _alarmSettings.Values.FirstOrDefault(a => a.DisplayName == kvp.Key);
                if (original != null)
                {
                    original.Id = kvp.Value.Id;
                    original.AutoLinkDevices = kvp.Value.AutoLinkDevices;
                }
            }

            string message = "Состояние галочек:\n\n";
            foreach (var alarm in _alarmSettings.Values)
            {
                message += $"{alarm.DisplayName}: {(alarm.AutoLinkDevices ? "✅ ВКЛ" : "❌ ВЫКЛ")}\n";
            }
            MessageBox.Show(message, "Статус автопривязки");

            _alarmSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(btnPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.Click += (s, e) =>
        {
            foreach (var alarm in _alarmSettings.Values)
            {
                if (AlarmSettings.DefaultAlarms.TryGetValue(alarm.DisplayName, out var defaultSettings))
                {
                    alarm.Id = defaultSettings.Id;
                    alarm.AutoLinkDevices = true;
                }
            }
            _alarmSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnCancel);

        _alarmSettingsForm.Controls.Add(panel);
        _alarmSettingsForm.Controls.Add(btnPanel);

        _alarmSettingsForm.FormClosed += (s, e) => { _alarmSettingsForm = null; };
        _alarmSettingsForm.Show(this);
    }

    private void ShowDeleteSettingsDialog()
    {
        if (_deleteSettingsForm != null && !_deleteSettingsForm.IsDisposed)
        {
            _deleteSettingsForm.Close();
            _deleteSettingsForm = null;
            return;
        }

        _deleteSettingsForm = new Form
        {
            Text = "Настройки удаления",
            Size = new Size(350, 300),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _deleteSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 9,
            ColumnCount = 2,
            AutoSize = true
        };

        int row = 0;

        panel.Controls.Add(new Label
        {
            Text = "Удалять:",
            Font = new Font("Arial", 10, FontStyle.Bold),
            AutoSize = true
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        var chkAll = new CheckBox
        {
            Text = "Всё",
            Checked = _deleteSettings.DeleteAll,
            AutoSize = true,
            Tag = "all"
        };
        chkAll.CheckedChanged += (s, e) =>
        {
            _deleteSettings.DeleteAll = chkAll.Checked;
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is CheckBox chk && chk.Tag != null && chk.Tag.ToString() != "all")
                {
                    chk.Enabled = !_deleteSettings.DeleteAll;
                    if (_deleteSettings.DeleteAll) chk.Checked = true;
                }
            }
            UpdateDeleteSettingsLabel();
        };
        panel.Controls.Add(chkAll, 0, row);
        panel.SetColumnSpan(chkAll, 2);
        row++;

        var chkRooms = new CheckBox
        {
            Text = "Комнаты",
            Checked = _deleteSettings.DeleteRooms,
            AutoSize = true,
            Tag = "rooms",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkRooms.CheckedChanged += (s, e) => { _deleteSettings.DeleteRooms = chkRooms.Checked; };
        panel.Controls.Add(chkRooms, 0, row);
        panel.SetColumnSpan(chkRooms, 2);
        row++;

        var chkPipes = new CheckBox
        {
            Text = "Газовые трубы",
            Checked = _deleteSettings.DeletePipes,
            AutoSize = true,
            Tag = "pipes",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkPipes.CheckedChanged += (s, e) => { _deleteSettings.DeletePipes = chkPipes.Checked; };
        panel.Controls.Add(chkPipes, 0, row);
        panel.SetColumnSpan(chkPipes, 2);
        row++;

        var chkAlarms = new CheckBox
        {
            Text = "Сигнализации (AirAlarm, FireAlarm)",
            Checked = _deleteSettings.DeleteAlarms,
            AutoSize = true,
            Tag = "alarms",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkAlarms.CheckedChanged += (s, e) => { _deleteSettings.DeleteAlarms = chkAlarms.Checked; };
        panel.Controls.Add(chkAlarms, 0, row);
        panel.SetColumnSpan(chkAlarms, 2);
        row++;

        var chkWires = new CheckBox
        {
            Text = "Провода (скоро)",
            Checked = _deleteSettings.DeleteWires,
            AutoSize = true,
            Tag = "wires",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkWires.CheckedChanged += (s, e) => { _deleteSettings.DeleteWires = chkWires.Checked; };
        panel.Controls.Add(chkWires, 0, row);
        panel.SetColumnSpan(chkWires, 2);
        row++;

        var chkEntities = new CheckBox
        {
            Text = "Прототипы",
            Checked = _deleteSettings.DeleteEntities,
            AutoSize = true,
            Tag = "entities",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkEntities.CheckedChanged += (s, e) => { _deleteSettings.DeleteEntities = chkEntities.Checked; };
        panel.Controls.Add(chkEntities, 0, row);
        panel.SetColumnSpan(chkEntities, 2);
        row++;

        var chkOther = new CheckBox
        {
            Text = "Другое",
            Checked = _deleteSettings.DeleteOther,
            AutoSize = true,
            Tag = "other",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkOther.CheckedChanged += (s, e) => { _deleteSettings.DeleteOther = chkOther.Checked; };
        panel.Controls.Add(chkOther, 0, row);
        panel.SetColumnSpan(chkOther, 2);
        row++;

        var chkDecals = new CheckBox
        {
            Text = "Декали",
            Checked = _deleteSettings.DeleteDecals,
            AutoSize = true,
            Tag = "decals",
            Enabled = !_deleteSettings.DeleteAll
        };
        chkDecals.CheckedChanged += (s, e) => { _deleteSettings.DeleteDecals = chkDecals.Checked; };
        panel.Controls.Add(chkDecals, 0, row);
        panel.SetColumnSpan(chkDecals, 2);
        row++;

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(btnPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOk.Click += (s, e) => _deleteSettingsForm?.Close();
        btnPanel.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(btnPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.Click += (s, e) =>
        {
            _deleteSettings = new DeleteSettings();
            _deleteSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnCancel);

        _deleteSettingsForm.Controls.Add(panel);
        _deleteSettingsForm.Controls.Add(btnPanel);

        _deleteSettingsForm.FormClosed += (s, e) => { _deleteSettingsForm = null; };
        _deleteSettingsForm.Show(this);
    }

    private void UpdateDeleteSettingsLabel()
    {
        string mode = _deleteSettings.DeleteAll ? "Всё" :
                      _deleteSettings.DeletePipes ? "Трубы" : "Ничего";
        _typeLabel.Text = $"Удаление области: {mode}";
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (_renderer != null)
        {
            _renderer.ClearAlarmPreview();
            _renderer.ClearEntityPreview();

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _renderer.ClearSelectionBox();
            }

            Render();
        }
    }

    private void ArmPrototypePlacement()
    {
        if (_protoList.SelectedItem == null) return;
        string? id = _protoList.SelectedItem.ToString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

        _protoToPlace = id;
        _toolManager.ForceSetTool(ToolManager.Tool.PlacePrototype);
    }

    // ============================================================
    // НАСТРОЙКИ ЦЕНТРИРОВАНИЯ
    // ============================================================

    private void UpdateCenterSettingsButton()
    {
        if (_btnCenterSettings != null)
        {
            _btnCenterSettings.Text = $"⚙ {_centerOffset.X:F1}/{_centerOffset.Y:F1}";
        }
    }





    // Парсит цвет вида "#RRGGBBAA" (формат SS14/DecalGrid) в System.Drawing.Color.
    // При ошибке парсинга возвращает непрозрачный белый — безопасный дефолт для декали
    private static Color ParseHexColor(string hex)
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
                // Цвета палитр ("- type: palette") хранятся без альфы — считаем непрозрачным
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Color.White;
    }

    // Обратное преобразование — в формат "#RRGGBBAA", как ожидает DecalGrid при экспорте
    private static string ToHexColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }


    // Палитра хранит "#RRGGBB" (без альфы), а декали экспортируются как "#RRGGBBAA"
    private static string ToDecalColorFormat(string paletteHex)
    {
        var h = paletteHex.TrimStart('#');
        if (h.Length == 6) return $"#{h.ToUpperInvariant()}FF";
        if (h.Length == 8) return $"#{h.ToUpperInvariant()}";
        return "#FFFFFFFF";
    }

    private static Color GetContrastTextColor(Color background)
    {
        int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return brightness < 128 ? Color.White : Color.Black;
    }





    private void ShowCenterSettingsDialog()
    {
        if (_centerSettingsForm != null && !_centerSettingsForm.IsDisposed)
        {
            _centerSettingsForm.Focus();
            return;
        }

        _centerSettingsForm = new Form
        {
            Text = "Настройки прототипа",
            Size = new Size(340, 450),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _centerSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 8,
            ColumnCount = 2,
            AutoSize = false,
        };

        int row = 0;

        panel.Controls.Add(new Label
        {
            Text = "Смещение от левого верхнего угла тайла:",
            AutoSize = true,
            Font = new Font("Arial", 9, FontStyle.Bold),
            ForeColor = Color.DarkBlue
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

panel.Controls.Add(new Label { Text = "Смещение X:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudX = new NumericUpDown
        {
            Value = (decimal)_centerOffset.X,
            Minimum = -2m,
            Maximum = 2m,
            Increment = 0.01m,
            DecimalPlaces = 2,
            Width = 80
        };
        nudX.ValueChanged += (s, e) =>
        {
            _centerOffset = new PointF((float)nudX.Value, _centerOffset.Y);
            UpdateCenterSettingsButton();
        };
        panel.Controls.Add(nudX, 1, row);
        row++;

        panel.Controls.Add(new Label { Text = "Смещение Y:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudY = new NumericUpDown
        {
            Value = (decimal)_centerOffset.Y,
            Minimum = -2m,
            Maximum = 2m,
            Increment = 0.01m,
            DecimalPlaces = 2,
            Width = 80
        };
        nudY.ValueChanged += (s, e) =>
        {
            _centerOffset = new PointF(_centerOffset.X, (float)nudY.Value);
            UpdateCenterSettingsButton();
        };
        panel.Controls.Add(nudY, 1, row);
        row++;

        panel.Controls.Add(new Label
        {
            Text = "0.0 = левый верхний угол\n0.5 = центр тайла\n1.0 = правый нижний угол",
            AutoSize = true,
            Font = new Font("Arial", 8),
            ForeColor = Color.Gray
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        panel.Controls.Add(new Label
        {
            Text = "Цвет декали:",
            AutoSize = true,
            Font = new Font("Arial", 9, FontStyle.Bold),
            ForeColor = Color.DarkBlue
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        // ===== Выбор палитры (из "- type: palette" репозитория) =====
        panel.Controls.Add(new Label { Text = "Палитра:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var palettes = _indexer.GetPalettes();
        var paletteCombo = new ComboBox
        {
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8),
            Enabled = palettes.Count > 0
        };
        if (palettes.Count > 0)
        {
            foreach (var p in palettes) paletteCombo.Items.Add(p);
            paletteCombo.DisplayMember = "Name";
            paletteCombo.SelectedIndex = 0;
        }
        else
        {
            paletteCombo.Items.Add("(нет палитр — обновите репозиторий)");
            paletteCombo.SelectedIndex = 0;
        }
        panel.Controls.Add(paletteCombo, 1, row);
        row++;

        // Плашки цветов выбранной палитры — перестраиваются при смене палитры в комбобоксе
        int swatchColumns = 8;
        int swatchSize = 26;
        int swatchSpacing = 4;
        int swatchPanelWidth = swatchColumns * (swatchSize + swatchSpacing) + SystemInformation.VerticalScrollBarWidth;

        // Anchor вместо Dock: Top|Left|Bottom тянет ВЫСОТУ под доступное место в строке
        // (строка ниже получит RowStyle.Percent и будет сжиматься/расти вместе с окном),
        // но ШИРИНА остаётся фиксированной (Right не заанкорен) — поэтому 8 плашек в ряд
        // и отсутствие горизонтального скролла сохраняются при любом размере окна.
        // Height=180 здесь лишь стартовое значение, реальная высота задаётся анкором.
        var swatchPanel = new FlowLayoutPanel
        {
            Width = swatchPanelWidth,
            Height = 180,
            MinimumSize = new Size(swatchPanelWidth, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        int swatchRowIndex = row;
        panel.Controls.Add(swatchPanel, 0, row);
        panel.SetColumnSpan(swatchPanel, 2);
        row++;

        var currentDecalColor = ParseHexColor(_decalColor);
        var btnDecalColor = new Button
        {
            Text = _decalColor,
            BackColor = currentDecalColor,
            ForeColor = GetContrastTextColor(currentDecalColor),
            Width = 260,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnDecalColor.Click += (s, e) =>
        {
            using var dialog = new ColorDialog { Color = btnDecalColor.BackColor, FullOpen = true };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                btnDecalColor.BackColor = dialog.Color;
                btnDecalColor.ForeColor = GetContrastTextColor(dialog.Color);
                btnDecalColor.Text = ToHexColor(dialog.Color);
                _decalColor = btnDecalColor.Text;
            }
        };
        panel.Controls.Add(btnDecalColor, 0, row);
        panel.SetColumnSpan(btnDecalColor, 2);
        row++;

        // Стираемость декали — как в игре: тряпкой/шваброй можно стереть только декали
        // с флагом cleanable, остальные постоянные
        var chkCleanable = new CheckBox
        {
            Text = "Стираемая (cleanable)",
            Checked = _decalCleanable,
            AutoSize = true,
            Font = new Font("Segoe UI", 9)
        };
        chkCleanable.CheckedChanged += (s, e) => { _decalCleanable = chkCleanable.Checked; };
        panel.Controls.Add(chkCleanable, 0, row);
        panel.SetColumnSpan(chkCleanable, 2);
        row++;


        // Все строки, кроме блока с плашками, оставляем в естественном размере (AutoSize),
        // а строке swatchPanel отдаём 100% оставшегося места (RowStyle.Percent). Так при
        // уменьшении окна сжимается только этот блок — остальные элементы (включая кнопку
        // выбора своего цвета ниже) сохраняют свою высоту и остаются видимыми
        panel.RowCount = row;
        panel.RowStyles.Clear();
        for (int i = 0; i < row; i++)
        {
            panel.RowStyles.Add(i == swatchRowIndex
                ? new RowStyle(SizeType.Percent, 100f)
                : new RowStyle(SizeType.AutoSize));
        }

        void RebuildSwatches()
        {
            swatchPanel.Controls.Clear();
            if (paletteCombo.SelectedItem is not Palette selectedPalette) return;

            foreach (var kvp in selectedPalette.Colors)
            {
                var swatchColor = ParseHexColor(kvp.Value);
                var swatch = new Button
                {
                    Width = 26,
                    Height = 26,
                    BackColor = swatchColor,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(120, 120, 120) },
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand,
                    Tag = kvp.Value
                };
                var tooltip = new ToolTip();
                tooltip.SetToolTip(swatch, $"{kvp.Key} ({kvp.Value})");

                swatch.Click += (s, e) =>
                {
                    string decalHex = ToDecalColorFormat((string)swatch.Tag);
                    var color = ParseHexColor(decalHex);
                    btnDecalColor.BackColor = color;
                    btnDecalColor.ForeColor = GetContrastTextColor(color);
                    btnDecalColor.Text = decalHex;
                    _decalColor = decalHex;
                };

                swatchPanel.Controls.Add(swatch);
            }
        }

        paletteCombo.SelectedIndexChanged += (s, e) => RebuildSwatches();
        if (palettes.Count > 0) RebuildSwatches();

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(btnPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOk.Click += (s, e) =>
        {
            _centerSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnOk);

        // Раз все поля применяются мгновенно, кнопка "Отмена" отдельного смысла
        // отката уже не несёт — оставлена только как второй способ закрыть окно
        var btnCancel = new Button
        {
            Text = "Закрыть",
            Location = new Point(btnPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.Click += (s, e) => _centerSettingsForm?.Close();
        btnPanel.Controls.Add(btnCancel);

        _centerSettingsForm.Controls.Add(panel);
        _centerSettingsForm.Controls.Add(btnPanel);

        _centerSettingsForm.FormClosed += (s, e) => { _centerSettingsForm = null; };
        _centerSettingsForm.Show(this);
    }




    // ===== Вспомогательная логика инструмента "Перемещение" =====


    private object? HitTestAt(int tileX, int tileY)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return null;

        // 1. Сущности (сигнализации, трубы, ферлоки, generic-прототипы) — как в Delete,
        // но с Math.Floor вместо усечения (int), иначе дробные/отрицательные координаты промахиваются
        var entity = grid.Entities.FirstOrDefault(e =>
            FloorToInt(e.X) == tileX && FloorToInt(e.Y) == tileY && IsObjectIncludedForMove(e));
        if (entity != null) return entity;

        // 2. Декали — тоже точечные объекты с дробными координатами, как и сущности
        var decal = grid.Decals.FirstOrDefault(d =>
            FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY && IsObjectIncludedForMove(d));
        if (decal != null) return decal;

        // 3. Вручную размещённые тайлы
        var tile = grid.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (tile != null && IsObjectIncludedForMove(tile)) return tile;

        // 4. Комната
        var room = grid.Rooms.FirstOrDefault(r =>
            tileX >= r.X && tileX < r.X + r.Width &&
            tileY >= r.Y && tileY < r.Y + r.Height);
        if (room != null && IsObjectIncludedForMove(room)) return room;

        return null;
    }

    private List<object> GatherObjectsInRect(int minX, int minY, int maxX, int maxY)
    {
        var grid = _map.ActiveGrid;
        var result = new List<object>();
        if (grid == null) return result;

        foreach (var entity in grid.Entities)
        {
            if (!IsObjectIncludedForMove(entity)) continue;
            if (entity.X >= minX && entity.X <= maxX && entity.Y >= minY && entity.Y <= maxY)
                result.Add(entity);
        }

        foreach (var decal in grid.Decals)
        {
            if (!IsObjectIncludedForMove(decal)) continue;
            if (decal.X >= minX && decal.X <= maxX && decal.Y >= minY && decal.Y <= maxY)
                result.Add(decal);
        }

        foreach (var tile in grid.Tiles)
        {
            if (!IsObjectIncludedForMove(tile)) continue;
            if (tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
                result.Add(tile);
        }

        foreach (var room in grid.Rooms)
        {
            if (!IsObjectIncludedForMove(room)) continue;
            bool overlaps = !(room.X + room.Width <= minX || room.X > maxX ||
                               room.Y + room.Height <= minY || room.Y > maxY);
            if (overlaps)
                result.Add(room);
        }

        return result;
    }




    private static (float x, float y) GetTargetPosition(object target)
    {
        return target switch
        {
            Room room => (room.X, room.Y),
            Door door => (door.X, door.Y),
            PlacedTile tile => (tile.X, tile.Y),
            PlacedDecal decal => (decal.X, decal.Y),
            MapEntity entity => (entity.X, entity.Y),
            _ => (0f, 0f)
        };
    }

    private static void MoveTarget(object target, float newX, float newY)
    {
        switch (target)
        {
            case Room room:
                room.X = (int)Math.Round(newX);
                room.Y = (int)Math.Round(newY);
                break;
            case Door door:
                door.X = (int)Math.Round(newX);
                door.Y = (int)Math.Round(newY);
                break;
            case PlacedTile tile:
                tile.X = (int)Math.Round(newX);
                tile.Y = (int)Math.Round(newY);
                break;
            case PlacedDecal decal:
                // Декали — точечные объекты с дробными координатами, как MapEntity,
                // а не привязанные к целому тайлу (в отличие от PlacedTile)
                decal.X = newX;
                decal.Y = newY;
                break;
            case MapEntity entity:
                entity.X = newX;
                entity.Y = newY;
                break;
        }
    }
    private void BeginMoveDrag(Point mouseLocation)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return;

        _moveSnapshot.Clear();
        var alreadyAdded = new HashSet<object>();

        void AddSnapshot(object target)
        {
            if (!alreadyAdded.Add(target)) return;
            var pos = GetTargetPosition(target);
            _moveSnapshot.Add(new MoveSnapshotItem { Target = target, OrigX = pos.x, OrigY = pos.y });
        }

        foreach (var obj in _selectedObjects)
        {
            AddSnapshot(obj);

            // При перемещении комнаты вместе с ней должны сдвигаться её двери
            // и связанные с ними пожарные шлюзы (иначе они рассинхронизируются с новыми стенами)
            if (obj is Room room)
            {
                foreach (var door in room.Doors)
                {
                    AddSnapshot(door);

                    var firelock = grid.Entities.OfType<FirelockEntity>()
                        .FirstOrDefault(f => (int)f.X == door.X && (int)f.Y == door.Y);
                    if (firelock != null)
                        AddSnapshot(firelock);
                }
            }
        }

        _moveDragStartWorld = GetPrecisePosition(mouseLocation);
        _moveDidMove = false;
        _isMovingSelection = true;
    }



    private void ShowMoveSettingsDialog()
    {
        if (_moveSettingsForm != null && !_moveSettingsForm.IsDisposed)
        {
            _moveSettingsForm.Close();
            _moveSettingsForm = null;
            return;
        }

        _moveSettingsForm = new Form
        {
            Text = "Настройки перемещения",
            Size = new Size(360, 400),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _moveSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 10,
            ColumnCount = 2,
            AutoSize = true
        };

        int row = 0;

        panel.Controls.Add(new Label { Text = "Шаг перемещения:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudStep = new NumericUpDown
        {
            Value = (decimal)_moveSettings.Step,
            Minimum = 0.1m,
            Maximum = 10m,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Width = 80
        };
        panel.Controls.Add(nudStep, 1, row);
        row++;

        panel.Controls.Add(new Label
        {
            Text = "Фильтр выделяемых объектов:",
            Font = new Font("Arial", 10, FontStyle.Bold),
            AutoSize = true
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        var chkRooms = new CheckBox { Text = "Комнаты", Checked = _moveSettings.IncludeRooms, AutoSize = true };
        chkRooms.CheckedChanged += (s, e) => _moveSettings.IncludeRooms = chkRooms.Checked;
        panel.Controls.Add(chkRooms, 0, row);
        panel.SetColumnSpan(chkRooms, 2);
        row++;

        var chkTiles = new CheckBox { Text = "Отдельные тайлы", Checked = _moveSettings.IncludeTiles, AutoSize = true };
        chkTiles.CheckedChanged += (s, e) => _moveSettings.IncludeTiles = chkTiles.Checked;
        panel.Controls.Add(chkTiles, 0, row);
        panel.SetColumnSpan(chkTiles, 2);
        row++;

        var chkPipes = new CheckBox { Text = "Трубы", Checked = _moveSettings.IncludePipes, AutoSize = true };
        chkPipes.CheckedChanged += (s, e) => _moveSettings.IncludePipes = chkPipes.Checked;
        panel.Controls.Add(chkPipes, 0, row);
        panel.SetColumnSpan(chkPipes, 2);
        row++;

        var chkAlarms = new CheckBox { Text = "Сигнализации", Checked = _moveSettings.IncludeAlarms, AutoSize = true };
        chkAlarms.CheckedChanged += (s, e) => _moveSettings.IncludeAlarms = chkAlarms.Checked;
        panel.Controls.Add(chkAlarms, 0, row);
        panel.SetColumnSpan(chkAlarms, 2);
        row++;

        var chkFirelocks = new CheckBox { Text = "Пожарные шлюзы", Checked = _moveSettings.IncludeFirelocks, AutoSize = true };
        chkFirelocks.CheckedChanged += (s, e) => _moveSettings.IncludeFirelocks = chkFirelocks.Checked;
        panel.Controls.Add(chkFirelocks, 0, row);
        panel.SetColumnSpan(chkFirelocks, 2);
        row++;

        var chkEntities = new CheckBox { Text = "Сущности", Checked = _moveSettings.IncludeEntities, AutoSize = true };
        chkEntities.CheckedChanged += (s, e) => _moveSettings.IncludeEntities = chkEntities.Checked;
        panel.Controls.Add(chkEntities, 0, row);
        panel.SetColumnSpan(chkEntities, 2);
        row++;

        var chkOther = new CheckBox { Text = "Другое", Checked = _moveSettings.IncludeOther, AutoSize = true };
        chkOther.CheckedChanged += (s, e) => _moveSettings.IncludeOther = chkOther.Checked;
        panel.Controls.Add(chkOther, 0, row);
        panel.SetColumnSpan(chkOther, 2);
        row++;

        var chkDecals = new CheckBox { Text = "Декали", Checked = _moveSettings.IncludeDecals, AutoSize = true };
        chkDecals.CheckedChanged += (s, e) => _moveSettings.IncludeDecals = chkDecals.Checked;
        panel.Controls.Add(chkDecals, 0, row);
        panel.SetColumnSpan(chkDecals, 2);
        row++;

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(btnPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOk.Click += (s, e) =>
        {
            _moveSettings.Step = (float)nudStep.Value;
            _moveSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(btnPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.Click += (s, e) => _moveSettingsForm?.Close();
        btnPanel.Controls.Add(btnCancel);

        _moveSettingsForm.Controls.Add(panel);
        _moveSettingsForm.Controls.Add(btnPanel);

        _moveSettingsForm.FormClosed += (s, e) => { _moveSettingsForm = null; };
        _moveSettingsForm.Show(this);
    }
}