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
    private string _currentDoorType = "Airlock";
    private Button? _selectedDoorButton = null;
    private bool _hideRoomOverlay = false;

    public MainForm()
    {
        Text = "MapperIce";
        Size = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _renderer = new Renderer(Width, Height, _indexer);
        _doorUpdater = new DoorUpdater(_roomTypeManager);

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

    // === Undo/Redo ===

    private void SaveState()
    {
        if (_map.ActiveGrid == null) return;
        var state = _map.ActiveGrid.Rooms.Select(r => r.Clone()).ToList();
        _undo.AddState(state);
    }

    private void RestoreState(List<Room> state)
    {
        if (_map.ActiveGrid == null) return;
        _map.ActiveGrid.Rooms.Clear();
        foreach (var room in state)
            _map.ActiveGrid.Rooms.Add(room);
        Render();
    }

    // === Обработка клавиш ===

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            if (_undo.CanUndo)
            {
                var state = _undo.Undo();
                RestoreState(state);
            }
            return true;
        }

        if (keyData == (Keys.Control | Keys.Y))
        {
            if (_undo.CanRedo)
            {
                var state = _undo.Redo();
                RestoreState(state);
            }
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // === Панель репозиториев ===

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

        var btnPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 2, 0, 2) };

        _btnAddRepo = new Button { Text = "➕", Location = new Point(5, 5), Width = 30, Height = 25 };
        _btnAddRepo.Click += (s, e) => AddRepository();
        btnPanel.Controls.Add(_btnAddRepo);

        _btnRemoveRepo = new Button { Text = "🗑", Location = new Point(40, 5), Width = 30, Height = 25, Enabled = false };
        _btnRemoveRepo.Click += (s, e) => RemoveRepository();
        btnPanel.Controls.Add(_btnRemoveRepo);

        _btnIndexRepo = new Button { Text = "⚙", Location = new Point(75, 5), Width = 30, Height = 25, Enabled = false };
        _btnIndexRepo.Click += (s, e) => IndexSelectedRepository();
        btnPanel.Controls.Add(_btnIndexRepo);

        var btnRefresh = new Button
        {
            Name = "btnRefresh",
            Text = "🔄 Обновить",
            Location = new Point(110, 5),
            Width = 80,
            Height = 25,
            Enabled = false,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.Click += (s, e) => IndexSelectedRepository();
        btnPanel.Controls.Add(btnRefresh);

        panel.Controls.Add(btnPanel);

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
            _repoSelector.SelectedIndex = 0;
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

        if (hasRepo && repo!.IsIndexed)
        {
            _indexer.IndexRepository(repo);
        }
        else if (hasRepo)
        {
            _protoList.Items.Clear();
            _protoList.Items.Add("⚠️ Репозиторий не проиндексирован");
            _protoList.Items.Add("Нажмите 'Обновить' для загрузки");
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
            _indexer.IndexRepository(repo);
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

    // === Панель инструментов ===

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

        // Только левая граница
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

        // Первая строка: Создать + Настройки
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
        _btnCreateRoom.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.CreateRoom);
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

        // Вторая строка: Две кнопки дверей (Airlock и AirlockGlass)
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
            _currentDoorType = "Airlock";
            _selectedDoorButton = _btnAirlock;
            UpdateDoorButtons();
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
            _toolManager.SetTool(ToolManager.Tool.Door);
            _currentDoorType = "AirlockGlass";
            _selectedDoorButton = _btnAirlockGlass;
            UpdateDoorButtons();
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

        // Третья строка: Удалить
        _btnDelete = new Button
        {
            Text = "🗑 Удалить",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnDelete.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.Delete);
        _toolPanel.Controls.Add(_btnDelete);
        y += 40 + 2;

        _typeLabel = new Label
        {
            Text = $"Тип: {_roomTypeManager.SelectedType}  Приоритет: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DarkGray,
            Font = new Font("Arial", 8)
        };
        _toolPanel.Controls.Add(_typeLabel);
        y += 25 + 2;

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
                _btnAirlock.ImageAlign = ContentAlignment.MiddleCenter;
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
                _btnAirlockGlass.ImageAlign = ContentAlignment.MiddleCenter;
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

    private void UpdateDoorButtons()
    {
        if (_btnAirlock != null)
            _btnAirlock.BackColor = Color.White;

        if (_btnAirlockGlass != null)
            _btnAirlockGlass.BackColor = Color.White;

        if (_selectedDoorButton != null && _toolManager.CurrentTool == ToolManager.Tool.Door)
        {
            _selectedDoorButton.BackColor = Color.LightBlue;
        }
    }

    // === Диалог выбора типа комнаты ===

    private void ShowRoomTypeDialog()
    {
        if (_roomTypeForm != null && !_roomTypeForm.IsDisposed)
        {
            _roomTypeForm.Close();
            _roomTypeForm = null;
            return;
        }

        _roomTypeForm = new Form
        {
            Text = "Выберите тип комнаты",
            Size = new Size(550, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            ShowInTaskbar = false
        };
        _roomTypeForm.Owner = this;

        var treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            Indent = 20
        };
        UpdateTreeView(treeView);

        var btnPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10),
            ColumnCount = 7,
            RowCount = 1
        };
        for (int i = 0; i < 7; i++)
            btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / 7));

        var btnOk = new Button
        {
            Text = "OK",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnOk.Click += (s, e) =>
        {
            if (treeView.SelectedNode?.Tag is RoomType selected)
            {
                _roomTypeManager.SelectType(selected.Name);
                UpdateTypeLabel();
            }
            _roomTypeForm?.Close();
        };
        btnPanel.Controls.Add(btnOk, 0, 0);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnCancel.Click += (s, e) => _roomTypeForm?.Close();
        btnPanel.Controls.Add(btnCancel, 1, 0);

        var btnAdd = new Button
        {
            Text = "➕ Создать",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnAdd.Click += (s, e) =>
        {
            var editForm = new Form
            {
                Text = "Создать тип комнаты",
                Size = new Size(350, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false
            };
            editForm.Owner = _roomTypeForm;

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 10,
                ColumnCount = 2
            };

            var txtName = new TextBox { Dock = DockStyle.Fill };
            var txtCategory = new TextBox { Dock = DockStyle.Fill, Text = "Custom" };
            var txtWall = new TextBox { Dock = DockStyle.Fill, Text = "WallSolid" };
            var txtFloor = new TextBox { Dock = DockStyle.Fill, Text = "Plating" };
            var txtDoor = new TextBox { Dock = DockStyle.Fill, Text = "Airlock" };
            var txtGlassDoor = new TextBox { Dock = DockStyle.Fill, Text = "AirlockGlass" };
            var txtFill = new TextBox { Dock = DockStyle.Fill, Text = "200,230,230,230" };
            var txtLine = new TextBox { Dock = DockStyle.Fill, Text = "255,180,180,180" };
            var txtPriority = new TextBox { Dock = DockStyle.Fill, Text = "0" };

            AddRow(table, "Название:", txtName, 0);
            AddRow(table, "Категория:", txtCategory, 1);
            AddRow(table, "Стена (proto):", txtWall, 2);
            AddRow(table, "Пол (proto):", txtFloor, 3);
            AddRow(table, "Дверь (proto):", txtDoor, 4);
            AddRow(table, "Стеклянная дверь (proto):", txtGlassDoor, 5);
            AddRow(table, "Цвет (A,R,G,B):", txtFill, 6);
            AddRow(table, "Цвет линии (A,R,G,B):", txtLine, 7);
            AddRow(table, "Приоритет (число):", txtPriority, 8);

            var btnSave = new Button { Text = "Сохранить", Dock = DockStyle.Fill };
            var btnCancelEdit = new Button { Text = "Отмена", Dock = DockStyle.Fill };
            var btnPanelEdit = new Panel { Dock = DockStyle.Fill };

            btnSave.Click += (s2, e2) =>
            {
                try
                {
                    var fill = txtFill.Text.Split(',').Select(int.Parse).ToArray();
                    var line = txtLine.Text.Split(',').Select(int.Parse).ToArray();
                    var priority = int.Parse(txtPriority.Text);

                    _roomTypeManager.CreateCustomType(
                        txtName.Text,
                        txtCategory.Text,
                        txtWall.Text,
                        txtFloor.Text,
                        txtDoor.Text,
                        txtGlassDoor.Text,
                        Color.FromArgb(fill[0], fill[1], fill[2], fill[3]),
                        Color.FromArgb(line[0], line[1], line[2], line[3]),
                        priority
                    );
                    UpdateTreeView(treeView);
                    editForm.Close();
                }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
            };
            btnCancelEdit.Click += (s2, e2) => editForm.Close();

            btnPanelEdit.Controls.Add(btnSave);
            btnPanelEdit.Controls.Add(btnCancelEdit);
            table.Controls.Add(btnPanelEdit, 0, 9);
            table.SetColumnSpan(btnPanelEdit, 2);

            editForm.Controls.Add(table);
            editForm.Show(_roomTypeForm);
        };
        btnPanel.Controls.Add(btnAdd, 2, 0);

        var btnEdit = new Button
        {
            Text = "✏️ Правка",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnEdit.Click += (s, e) =>
        {
            if (treeView.SelectedNode?.Tag is CustomRoomType custom)
            {
                var editForm = new Form
                {
                    Text = $"Редактировать {custom.Name}",
                    Size = new Size(350, 480),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ShowInTaskbar = false
                };
                editForm.Owner = _roomTypeForm;

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(10),
                    RowCount = 10,
                    ColumnCount = 2
                };

                var txtName = new TextBox { Dock = DockStyle.Fill, Text = custom.Name };
                var txtCategory = new TextBox { Dock = DockStyle.Fill, Text = custom.Category };
                var txtWall = new TextBox { Dock = DockStyle.Fill, Text = custom.WallProto };
                var txtFloor = new TextBox { Dock = DockStyle.Fill, Text = custom.FloorProto };
                var txtDoor = new TextBox { Dock = DockStyle.Fill, Text = custom.DoorProto };
                var txtGlassDoor = new TextBox { Dock = DockStyle.Fill, Text = custom.GlassDoorProto };
                var txtFill = new TextBox { Dock = DockStyle.Fill, Text = $"{custom.FillColor.A},{custom.FillColor.R},{custom.FillColor.G},{custom.FillColor.B}" };
                var txtLine = new TextBox { Dock = DockStyle.Fill, Text = $"{custom.LineColor.A},{custom.LineColor.R},{custom.LineColor.G},{custom.LineColor.B}" };
                var txtPriority = new TextBox { Dock = DockStyle.Fill, Text = custom.Priority.ToString() };

                AddRow(table, "Название:", txtName, 0);
                AddRow(table, "Категория:", txtCategory, 1);
                AddRow(table, "Стена (proto):", txtWall, 2);
                AddRow(table, "Пол (proto):", txtFloor, 3);
                AddRow(table, "Дверь (proto):", txtDoor, 4);
                AddRow(table, "Стеклянная дверь (proto):", txtGlassDoor, 5);
                AddRow(table, "Цвет (A,R,G,B):", txtFill, 6);
                AddRow(table, "Цвет линии (A,R,G,B):", txtLine, 7);
                AddRow(table, "Приоритет (число):", txtPriority, 8);

                var btnSave = new Button { Text = "Сохранить", Dock = DockStyle.Fill };
                var btnCancelEdit = new Button { Text = "Отмена", Dock = DockStyle.Fill };
                var btnPanelEdit = new Panel { Dock = DockStyle.Fill };

                btnSave.Click += (s2, e2) =>
                {
                    try
                    {
                        var fill = txtFill.Text.Split(',').Select(int.Parse).ToArray();
                        var line = txtLine.Text.Split(',').Select(int.Parse).ToArray();
                        var priority = int.Parse(txtPriority.Text);

                        _roomTypeManager.EditCustomType(
                            custom.Name,
                            txtName.Text,
                            txtCategory.Text,
                            txtWall.Text,
                            txtFloor.Text,
                            txtDoor.Text,
                            txtGlassDoor.Text,
                            Color.FromArgb(fill[0], fill[1], fill[2], fill[3]),
                            Color.FromArgb(line[0], line[1], line[2], line[3]),
                            priority
                        );
                        UpdateTreeView(treeView);
                        editForm.Close();
                    }
                    catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
                };
                btnCancelEdit.Click += (s2, e2) => editForm.Close();

                btnPanelEdit.Controls.Add(btnSave);
                btnPanelEdit.Controls.Add(btnCancelEdit);
                table.Controls.Add(btnPanelEdit, 0, 9);
                table.SetColumnSpan(btnPanelEdit, 2);

                editForm.Controls.Add(table);
                editForm.Show(_roomTypeForm);
            }
            else
            {
                MessageBox.Show("Выберите кастомный тип для редактирования");
            }
        };
        btnPanel.Controls.Add(btnEdit, 3, 0);

        var btnDelete = new Button
        {
            Text = "🗑 Удалить",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnDelete.Click += (s, e) =>
        {
            if (treeView.SelectedNode?.Tag is CustomRoomType custom)
            {
                if (MessageBox.Show($"Удалить тип '{custom.Name}'?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _roomTypeManager.DeleteCustomType(custom.Name);
                    UpdateTreeView(treeView);
                }
            }
            else MessageBox.Show("Выберите кастомный тип");
        };
        btnPanel.Controls.Add(btnDelete, 4, 0);

        var btnExport = new Button
        {
            Text = "📤 Экспорт",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnExport.Click += (s, e) =>
        {
            if (treeView.SelectedNode == null) { MessageBox.Show("Выберите тип или категорию"); return; }
            using var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (treeView.SelectedNode.Tag is RoomType selected)
            {
                dialog.FileName = $"{selected.Name}.json";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _roomTypeManager.ExportType(selected.Name, dialog.FileName);
                    MessageBox.Show($"Тип '{selected.Name}' экспортирован!");
                }
            }
            else
            {
                dialog.FileName = $"{treeView.SelectedNode.Text}.json";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _roomTypeManager.ExportCategory(treeView.SelectedNode.Text, dialog.FileName);
                    MessageBox.Show($"Категория '{treeView.SelectedNode.Text}' экспортирована!");
                }
            }
        };
        btnPanel.Controls.Add(btnExport, 5, 0);

        var btnImport = new Button
        {
            Text = "📥 Импорт",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
        btnImport.Click += (s, e) =>
        {
            using var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                if (json.Contains("\"Type\":\"Category\"") || json.StartsWith("["))
                    _roomTypeManager.ImportCategory(dialog.FileName);
                else
                    _roomTypeManager.ImportType(dialog.FileName);
                UpdateTreeView(treeView);
                MessageBox.Show("Импорт завершён!");
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
        };
        btnPanel.Controls.Add(btnImport, 6, 0);

        _roomTypeForm.Controls.Add(treeView);
        _roomTypeForm.Controls.Add(btnPanel);

        _roomTypeForm.FormClosed += (s, e) => { _roomTypeForm = null; };
        _roomTypeForm.Show(this);
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

    // === Панель гридов ===

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
                Render();
            }
        };
        panel.Controls.Add(btnRemoveGrid);

        // Кнопка переключения оверлея комнат
        var btnToggleOverlay = new Button
        {
            Text = "🗺️",
            Location = new Point(0, 7),
            Width = 40,
            Height = 40,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 14),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnToggleOverlay.Click += (s, e) =>
        {
            _hideRoomOverlay = !_hideRoomOverlay;
            btnToggleOverlay.BackColor = _hideRoomOverlay ? Color.LightGray : Color.LightGreen;
            btnToggleOverlay.Text = _hideRoomOverlay ? "📍" : "🗺️";
            Render();
        };
        panel.Controls.Add(btnToggleOverlay);

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

    // === Холст ===

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
        Controls.Add(_canvas);
    }

    // === Меню ===

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

    // === Отрисовка ===

    private void OnToolChanged(ToolManager.Tool tool)
    {
        _btnCreateRoom.BackColor = tool == ToolManager.Tool.CreateRoom ? Color.LightBlue : Color.White;
        _btnDelete.BackColor = tool == ToolManager.Tool.Delete ? Color.LightBlue : Color.White;

        if (tool == ToolManager.Tool.Door)
        {
            UpdateDoorButtons();
        }
        else
        {
            _selectedDoorButton = null;
            if (_btnAirlock != null)
                _btnAirlock.BackColor = Color.White;
            if (_btnAirlockGlass != null)
                _btnAirlockGlass.BackColor = Color.White;
        }

        Cursor = tool == ToolManager.Tool.CreateRoom ? Cursors.Cross :
                 tool == ToolManager.Tool.Delete ? Cursors.Hand :
                 tool == ToolManager.Tool.Door ? Cursors.Help :
                         Cursors.Default;

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
        _canvas.Invalidate();
    }

    private void UpdateBuffer()
    {
        _renderer.Resize(_canvas.Width, _canvas.Height);
    }

    // === Обработка мыши ===

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_canvas.Width == 0 || _canvas.Height == 0) return;
        if (_map.ActiveGrid == null) return;

        if (e.Button == MouseButtons.Right)
        {
            _isPanning = true;
            _panStart = new PointF(e.Location.X, e.Location.Y);
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            int tileSize = (int)(Constants.TILE_SIZE * _scale);
            float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
            float gridOffsetY = _map.ActiveGrid.Position.Y * tileSize;
            float worldX = (e.Location.X + _viewOffset.X - gridOffsetX) / tileSize;
            float worldY = (e.Location.Y + _viewOffset.Y - gridOffsetY) / tileSize;
            int tileX = (int)Math.Floor(worldX);
            int tileY = (int)Math.Floor(worldY);

            // === СОЗДАТЬ КОМНАТУ ===
            if (_toolManager.CurrentTool == ToolManager.Tool.CreateRoom)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _currentRoom = new Room { X = tileX, Y = tileY, Width = 1, Height = 1 };
            }

            // === УДАЛИТЬ ===
            else if (_toolManager.CurrentTool == ToolManager.Tool.Delete)
            {
                var grid = _map.ActiveGrid;

                // Проверяем дверь
                if (_doorUpdater.TryRemoveDoor(grid, tileX, tileY))
                {
                    SaveState();
                    Render();
                    return;
                }

                // Проверяем комнату
                var roomToDelete = grid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                if (roomToDelete != null)
                {
                    grid.Rooms.Remove(roomToDelete);
                    SaveState();
                    Render();
                }
            }

            // === ДВЕРЬ ===
            else if (_toolManager.CurrentTool == ToolManager.Tool.Door)
            {
                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, _currentDoorType, out var newDoor))
                {
                    SaveState();
                    Render();
                }
            }
        }
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

        if (!_isDrawing || _currentRoom == null || _map.ActiveGrid == null) return;

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
            Cursor = _toolManager.CurrentTool == ToolManager.Tool.CreateRoom ? Cursors.Cross : Cursors.Default;
            return;
        }

        if (e.Button == MouseButtons.Left && _isDrawing && _currentRoom != null && _map.ActiveGrid != null)
        {
            if (_currentRoom.Width > 1 || _currentRoom.Height > 1)
            {
                _roomTypeManager.ApplyTypeToRoom(_currentRoom);
                _map.ActiveGrid.Rooms.Add(_currentRoom);

                // Обновляем все двери в гриде (а не только на границах комнаты)
                _doorUpdater.UpdateAllDoors(_map.ActiveGrid);

                SaveState();
            }

            _currentRoom = null;
            _isDrawing = false;
            Render();
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        float delta = e.Delta > 0 ? 0.1f : -0.1f;
        _scale = Math.Clamp(_scale + delta, 0.2f, 3.0f);
        Render();
    }

    private void UpdateTypeLabel()
    {
        if (_typeLabel != null)
            _typeLabel.Text = $"Тип: {_roomTypeManager.SelectedType}  Приоритет: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}";
    }

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
                var generator = new YAMLGenerator();
                var yaml = generator.Generate(_map.ActiveGrid.Rooms);
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
}