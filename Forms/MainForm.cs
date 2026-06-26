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

    private Point _startPoint;
    private bool _isDrawing = false;

    private PointF _viewOffset = new PointF(0, 0);
    private PointF _panStart;
    private bool _isPanning = false;
    private float _scale = 1.0f;

    private PictureBox _canvas = null!;
    private Button _btnCreateRoom = null!;
    private Button _btnDelete = null!;
    private ComboBox _gridSelector = null!;

    public MainForm()
    {
        Text = "MapperIce";
        Size = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _renderer = new Renderer(Width, Height);

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

        // Сохраняем начальное состояние (пусто)
        SaveState();

        UpdateBuffer();
    }

    // === Undo/Redo ===

    private void SaveState()
    {
        if (_map.ActiveGrid == null) return;
        var state = _map.ActiveGrid.Rooms.Select(r => r.Clone()).ToList();
        _undo.SaveState(state);
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

        _btnCreateRoom = new Button
        {
            Text = "🟦 Создать комнату",
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(5, 2, 5, 2)
        };
        _btnCreateRoom.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.CreateRoom);
        panel.Controls.Add(_btnCreateRoom);

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
                    SaveState();
                    _map.ActiveGrid.Rooms.Remove(room);
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
                SaveState();
                _map.ActiveGrid.Rooms.Add(_currentRoom);
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