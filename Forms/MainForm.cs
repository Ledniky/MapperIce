using MapperIce.Models;
using MapperIce.Services;

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
            Width = 65,
            Height = 22,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8),
            Enabled = false
        };
        _filterCombo.Items.AddRange(new object[] { "Все", "Тайлы", "Структура", "Спавнер" });
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
            Location = new Point(5, 35),
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
            Location = new Point(70, 35),
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
            Location = new Point(135, 35),
            Width = 60,
            Height = 25,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnCenterSettings.Click += (s, e) => ShowCenterSettingsDialog();
        btnPanel.Controls.Add(_btnCenterSettings);

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
                    case "all": break;
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
                }

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

        if (_btnAirAlarm != null) _btnAirAlarm.BackColor = Color.White;
        if (_btnFireAlarm != null) _btnFireAlarm.BackColor = Color.White;
        if (tool != ToolManager.Tool.PlacePrototype) _protoToPlace = null;

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
                _typeLabel.Text = $"Размещение: {_protoToPlace}  (клик — поставить)";
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

                var alarm = grid.Entities.OfType<AirAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (alarm != null) { grid.Entities.Remove(alarm); SaveState(); UpdateTileGrid(); Render(); return; }

                var fireAlarm = grid.Entities.OfType<FireAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (fireAlarm != null) { grid.Entities.Remove(fireAlarm); SaveState(); UpdateTileGrid(); Render(); return; }

                var pipe = grid.Entities.OfType<PipeEntity>().FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY);
                if (pipe != null) { grid.Entities.Remove(pipe); SaveState(); UpdateTileGrid(); Render(); return; }

                if (_doorUpdater.TryRemoveDoor(grid, tileX, tileY)) { SaveState(); UpdateTileGrid(); Render(); return; }

                var anyEntity = grid.Entities.FirstOrDefault(e => (int)e.X == tileX && (int)e.Y == tileY);
                if (anyEntity != null) { grid.Entities.Remove(anyEntity); SaveState(); UpdateTileGrid(); Render(); return; }

                var room = grid.Rooms.FirstOrDefault(r => tileX >= r.X && tileX < r.X + r.Width && tileY >= r.Y && tileY < r.Y + r.Height);
                if (room != null) { grid.Rooms.Remove(room); SaveState(); UpdateTileGrid(); Render(); return; }
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
                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, "Airlock", out _, _snapToGrid))
                { SaveState(); UpdateTileGrid(); Render(); }
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.DoorGlass)
            {
                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, "AirlockGlass", out _, _snapToGrid))
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

                    grid.Entities.Add(new MapEntity { X = finalX, Y = finalY, Proto = _protoToPlace });
                    SaveState();
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
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        float zoomDelta = e.Delta > 0 ? 0.1f : -0.1f;
        _scale = Math.Clamp(_scale + zoomDelta, 0.2f, 3.0f);
        Render();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
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

            if (_deleteSettings.DeleteAll)
            {
                toRemove.AddRange(grid.Entities.Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY));

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        _doorUpdater.TryRemoveDoor(grid, x, y);

                var rooms = grid.Rooms.Where(r => !(r.X + r.Width <= minX || r.X > maxX || r.Y + r.Height <= minY || r.Y > maxY)).ToList();
                foreach (var room in rooms) grid.Rooms.Remove(room);
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
            }

            foreach (var entity in toRemove) grid.Entities.Remove(entity);

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
                _doorUpdater.UpdateAllDoors(_map.ActiveGrid);
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

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show($"Проект сохранён!\nКомнат: {data.Rooms.Count}\nДверей: {data.Rooms.Sum(r => r.Doors.Count)}");
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

                _doorUpdater.UpdateAllDoors(_map.ActiveGrid);
                UpdateTileGrid();
                SaveState();
                Render();

                int totalDoors = data.Rooms.Sum(r => r.Doors.Count);
                MessageBox.Show($"Проект загружен!\nКомнат: {data.Rooms.Count}\nДверей: {totalDoors}");
            }
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
            Text = "Сущности",
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
            Text = "Декали (скоро)",
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

    private void ShowCenterSettingsDialog()
    {
        if (_centerSettingsForm != null && !_centerSettingsForm.IsDisposed)
        {
            _centerSettingsForm.Focus();
            return;
        }

        _centerSettingsForm = new Form
        {
            Text = "Настройки смещения центрирования",
            Size = new Size(320, 220),
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
            RowCount = 4,
            ColumnCount = 2,
            AutoSize = true
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
            Increment = 0.1m,
            DecimalPlaces = 1,
            Width = 80
        };
        panel.Controls.Add(nudX, 1, row);
        row++;

        panel.Controls.Add(new Label { Text = "Смещение Y:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudY = new NumericUpDown
        {
            Value = (decimal)_centerOffset.Y,
            Minimum = -2m,
            Maximum = 2m,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Width = 80
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
            _centerOffset = new PointF((float)nudX.Value, (float)nudY.Value);
            UpdateCenterSettingsButton();
            _centerSettingsForm?.Close();
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
        btnCancel.Click += (s, e) => _centerSettingsForm?.Close();
        btnPanel.Controls.Add(btnCancel);

        _centerSettingsForm.Controls.Add(panel);
        _centerSettingsForm.Controls.Add(btnPanel);

        _centerSettingsForm.FormClosed += (s, e) => { _centerSettingsForm = null; };
        _centerSettingsForm.Show(this);
    }
}