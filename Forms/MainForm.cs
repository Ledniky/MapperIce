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

    private Point _startPoint;
    private bool _isDrawing = false;

    private PointF _viewOffset = new PointF(0, 0);
    private PointF _panStart;
    private bool _isPanning = false;
    private float _scale = 1.0f;

    private PictureBox _canvas = null!;
    private Button _btnCreateRoom = null!;
    private Button _btnDelete = null!;
    private Button _btnRoomSettings = null!;
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

    public MainForm()
    {
        Text = "MapperIce";
        Size = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _renderer = new Renderer(Width, Height);

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
        _indexer.OnIndexingComplete += () => { UpdatePrototypeList(); Render(); };

        UpdateRepoSelector();
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
            BorderStyle = BorderStyle.FixedSingle
        };

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
        _searchBox.TextChanged += (s, e) => UpdatePrototypeList(_searchBox.Text);
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
            MessageBox.Show($"Проиндексировано {count} прототипов");
        }
    }

    private void UpdatePrototypeList(string filter = "")
    {
        _protoList.Items.Clear();

        var allIds = string.IsNullOrEmpty(filter) || filter == "Поиск прототипов..."
            ? _indexer.GetPrototypeIds()
            : _indexer.SearchPrototypes(filter);

        var filteredIds = allIds;

        switch (_currentFilter)
        {
            case "all":
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
        }

        foreach (var id in filteredIds.Take(1000))
            _protoList.Items.Add(id);

        if (_protoList.Items.Count == 0)
            _protoList.Items.Add("(нет прототипов)");
    }

    private void OnPrototypeDoubleClick(object? sender, EventArgs e)
    {
        if (_protoList.SelectedItem == null) return;
        string? id = _protoList.SelectedItem.ToString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

        var path = _indexer.GetFullTexturePath(id);
        if (path != null && File.Exists(path))
        {
            using var img = Image.FromFile(path);
            var previewForm = new Form
            {
                Text = $"Спрайт: {id}",
                Size = new Size(300, 300),
                StartPosition = FormStartPosition.CenterParent
            };
            var pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = (Image)img.Clone()
            };
            previewForm.Controls.Add(pb);
            previewForm.ShowDialog();
        }
        else
        {
            MessageBox.Show($"Спрайт для '{id}' не найден");
        }
    }

    // === Панель инструментов ===

    private void CreateToolPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0, 5, 0, 5)
        };

        var title = new Label
        {
            Text = "Инструменты",
            Font = new Font("Arial", 14, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 35,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(220, 220, 220),
            Margin = new Padding(0, 0, 0, 5)
        };
        panel.Controls.Add(title);

        var rowPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5, 2, 5, 2)
        };

        _btnCreateRoom = new Button
        {
            Text = "🟦 Создать",
            Location = new Point(5, 2),
            Width = 155,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnCreateRoom.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.CreateRoom);
        rowPanel.Controls.Add(_btnCreateRoom);

        _btnRoomSettings = new Button
        {
            Text = "⚙",
            Location = new Point(165, 2),
            Width = 25,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnRoomSettings.Click += (s, e) => ShowRoomTypeDialog();
        rowPanel.Controls.Add(_btnRoomSettings);

        panel.Controls.Add(rowPanel);

        _btnDelete = new Button
        {
            Text = "🗑 Удалить",
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(5, 2, 5, 2)
        };
        _btnDelete.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.Delete);
        panel.Controls.Add(_btnDelete);

        var hint = new Label
        {
            Text = "Повторное нажатие\nсбрасывает инструмент",
            Dock = DockStyle.Bottom,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Arial", 9),
            BackColor = Color.FromArgb(230, 230, 230)
        };
        panel.Controls.Add(hint);

        Controls.Add(panel);
    }

    // === Диалог выбора типа комнаты (неблокирующий) ===

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
            Size = new Size(450, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            TopMost = true,
            ShowInTaskbar = false
        };

        var treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            Indent = 20
        };
        UpdateTreeView(treeView);

        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10)
        };

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(10, 10),
            Width = 60,
            Height = 30
        };
        btnOk.Click += (s, e) =>
        {
            if (treeView.SelectedNode?.Tag is RoomType selected)
                _roomTypeManager.SelectType(selected.Name);
            _roomTypeForm?.Close();
        };
        btnPanel.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(75, 10),
            Width = 60,
            Height = 30
        };
        btnCancel.Click += (s, e) => _roomTypeForm?.Close();
        btnPanel.Controls.Add(btnCancel);

        var btnAdd = new Button
        {
            Text = "➕ Создать",
            Location = new Point(145, 10),
            Width = 70,
            Height = 30
        };
        btnAdd.Click += (s, e) =>
        {
            using var editForm = new Form
            {
        Text = "Создать тип комнаты",
        Size = new Size(300, 350),
        StartPosition = FormStartPosition.CenterParent,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        TopMost = true  // ← ДОБАВЬ ЭТУ СТРОКУ
    };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 7,
                ColumnCount = 2
            };

            table.Controls.Add(new Label { Text = "Название:", AutoSize = true }, 0, 0);
            var txtName = new TextBox { Dock = DockStyle.Fill };
            table.Controls.Add(txtName, 1, 0);

            table.Controls.Add(new Label { Text = "Категория:", AutoSize = true }, 0, 1);
            var txtCategory = new TextBox { Dock = DockStyle.Fill, Text = "Custom" };
            table.Controls.Add(txtCategory, 1, 1);

            table.Controls.Add(new Label { Text = "Стена (proto):", AutoSize = true }, 0, 2);
            var txtWall = new TextBox { Dock = DockStyle.Fill, Text = "WallSolid" };
            table.Controls.Add(txtWall, 1, 2);

            table.Controls.Add(new Label { Text = "Пол (proto):", AutoSize = true }, 0, 3);
            var txtFloor = new TextBox { Dock = DockStyle.Fill, Text = "Plating" };
            table.Controls.Add(txtFloor, 1, 3);

            table.Controls.Add(new Label { Text = "Цвет (A,R,G,B):", AutoSize = true }, 0, 4);
            var txtFill = new TextBox { Dock = DockStyle.Fill, Text = "128,230,230,230" };
            table.Controls.Add(txtFill, 1, 4);

            table.Controls.Add(new Label { Text = "Цвет линии (A,R,G,B):", AutoSize = true }, 0, 5);
            var txtLine = new TextBox { Dock = DockStyle.Fill, Text = "255,200,200,200" };
            table.Controls.Add(txtLine, 1, 5);

            var btnSave = new Button { Text = "Сохранить", Dock = DockStyle.Fill };
            var btnCancelEdit = new Button { Text = "Отмена", Dock = DockStyle.Fill };
            var btnPanelEdit = new Panel { Dock = DockStyle.Fill };
            btnSave.Click += (s2, e2) =>
            {
                try
                {
                    var fillParts = txtFill.Text.Split(',').Select(int.Parse).ToArray();
                    var lineParts = txtLine.Text.Split(',').Select(int.Parse).ToArray();
                    _roomTypeManager.CreateCustomType(
                        txtName.Text,
                        txtCategory.Text,
                        txtWall.Text,
                        txtFloor.Text,
                        Color.FromArgb(fillParts[0], fillParts[1], fillParts[2], fillParts[3]),
                        Color.FromArgb(lineParts[0], lineParts[1], lineParts[2], lineParts[3])
                    );
                    UpdateTreeView(treeView);
                    editForm.DialogResult = DialogResult.OK;
                    editForm.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            };
            btnCancelEdit.Click += (s2, e2) => editForm.Close();

            btnPanelEdit.Controls.Add(btnSave);
            btnPanelEdit.Controls.Add(btnCancelEdit);
            table.Controls.Add(btnPanelEdit, 0, 6);
            table.SetColumnSpan(btnPanelEdit, 2);

            editForm.Controls.Add(table);
            editForm.ShowDialog();
        };
        btnPanel.Controls.Add(btnAdd);

        var btnEdit = new Button
        {
            Text = "✏️ Правка",
            Location = new Point(220, 10),
            Width = 65,
            Height = 30
        };
        btnEdit.Click += (s, e) =>
        {
            if (treeView.SelectedNode?.Tag is CustomRoomType custom)
            {
                MessageBox.Show("Редактирование пока не реализовано");
            }
            else
            {
                MessageBox.Show("Выберите кастомный тип для редактирования");
            }
        };
        btnPanel.Controls.Add(btnEdit);

        var btnDelete = new Button
        {
            Text = "🗑 Удалить",
            Location = new Point(290, 10),
            Width = 65,
            Height = 30
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
            else
            {
                MessageBox.Show("Выберите кастомный тип для удаления");
            }
        };
        btnPanel.Controls.Add(btnDelete);

        _roomTypeForm.Controls.Add(treeView);
        _roomTypeForm.Controls.Add(btnPanel);

        _roomTypeForm.FormClosed += (s, e) => { _roomTypeForm = null; };
        _roomTypeForm.Show(this);
    }

    private void UpdateTreeView(TreeView treeView)
    {
        treeView.Nodes.Clear();
        var categories = _roomTypeManager.GetCategories();
        foreach (var category in categories.OrderBy(c => c.Key))
        {
            var node = new TreeNode(category.Key);
            foreach (var type in category.Value.OrderBy(t => t.Name))
            {
                var childNode = new TreeNode(type.Name)
                {
                    Tag = type,
                    ForeColor = type.IsCustom ? Color.Blue : Color.Black
                };
                node.Nodes.Add(childNode);
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
            Height = 40,
            BackColor = Color.FromArgb(230, 230, 230),
            Padding = new Padding(10, 5, 10, 5)
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
        fileMenu.DropDownItems.Add("Сохранить проект", null, (s, e) => { });
        fileMenu.DropDownItems.Add("Экспорт в YAML", null, (s, e) => { });
        fileMenu.DropDownItems.Add("Загрузить проект", null, (s, e) => { });
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

        Cursor = tool == ToolManager.Tool.CreateRoom ? Cursors.Cross :
                 tool == ToolManager.Tool.Delete ? Cursors.Hand :
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

            if (_toolManager.CurrentTool == ToolManager.Tool.CreateRoom)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _currentRoom = new Room { X = tileX, Y = tileY, Width = 1, Height = 1 };
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.Delete)
            {
                var room = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                if (room != null)
                {
                    _map.ActiveGrid.Rooms.Remove(room);
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
}