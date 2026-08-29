// Forms/MainForm.GridPanel.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    // === ПАНЕЛЬ СЛОЁВ (вкладки как в браузере) ===
    private Panel _tabStrip = null!;
    private Button _btnAddGrid = null!;
    private ContextMenuStrip _tabContextMenu = null!;
    private Grid? _contextMenuGrid;
    private ToolTip _tabToolTip = null!;
    private const int TAB_START_X = 42; // X позиция первой вкладки (после кнопки +)

    private void CreateGridPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(230, 230, 230),
            Padding = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Initialize ToolTip
        _tabToolTip = new ToolTip { InitialDelay = 100 };

        // === CENTER BLOCK: Tab Strip (full width, full height) ===
        _tabStrip = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(215, 215, 215),
            Margin = new Padding(0)
        };

        // "+" button (add new layer)
        _btnAddGrid = new Button
        {
            Text = "+",
            Location = new Point(8, 12),
            Width = 28,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 80, 80),
            BackColor = Color.Transparent
        };
        _btnAddGrid.Click += (s, e) => AddGridTab(_map.Grids.Count);
        _tabStrip.Controls.Add(_btnAddGrid);

        _tabToolTip.SetToolTip(_btnAddGrid, "Добавить новый слой");

        panel.Controls.Add(_tabStrip);

        // === RIGHT BLOCK: Toggle Buttons (Flags) ===
        var flagsPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            Height = 55,
            BackColor = Color.FromArgb(220, 220, 220),
            Margin = new Padding(4, 0, 4, 0)
        };
        CreateToggleButtonsInPanel(flagsPanel);
        panel.Controls.Add(flagsPanel);

        Controls.Add(panel);

        // --- Context Menu ---
        _tabContextMenu = new ContextMenuStrip();

        var renameItem = new ToolStripMenuItem("Переименовать");
        renameItem.Click += (s, e) => RenameContextGrid();
        _tabContextMenu.Items.Add(renameItem);

        var separator = new ToolStripSeparator();
        _tabContextMenu.Items.Add(separator);

        var addAfterItem = new ToolStripMenuItem("Добавить слой после");
        addAfterItem.Click += (s, e) => AddGridAfterContext();
        _tabContextMenu.Items.Add(addAfterItem);

        var deleteItem = new ToolStripMenuItem("Удалить слой");
        deleteItem.Click += (s, e) => DeleteContextGrid();
        _tabContextMenu.Items.Add(deleteItem);
    }

    private void CreateToggleButtonsInPanel(Panel parentPanel)
    {
        int xPos = 10;
        int yPos = 8;

        // 🗺️ Room overlay
        var btnRoom = CreateToggleBtn("🗺️", "❌", "Оверлей комнат", xPos, yPos, v =>
        {
            _hideRoomOverlay = v;
            Render();
        });
        parentPanel.Controls.Add(btnRoom);
        xPos += 46;

        // 🔧 Pipe overlay
        var btnPipe = CreateToggleBtn("🔧", "❌", "Оверлей труб", xPos, yPos, v =>
        {
            _showPipeOverlay = v;
            Render();
        });
        parentPanel.Controls.Add(btnPipe);
        xPos += 46;

        // 🔗 Alarm connections
        var btnAlarm = CreateToggleBtn("🔗", "❌", "Сети сигнализаций", xPos, yPos, v =>
        {
            _showAlarmConnections = v;
            Render();
        });
        parentPanel.Controls.Add(btnAlarm);
        xPos += 46;

        // 🧲 Snap to grid
        var btnSnap = CreateToggleBtn("🧲", "❌", "Привязка к сетке", xPos, yPos, v =>
        {
            _snapToGrid = v;
            Render();
        });
        parentPanel.Controls.Add(btnSnap);
    }

    private Button CreateToggleBtn(string onIcon, string offIcon, string tooltip, int x, int y, Action<bool> onToggle)
    {
        var btn = new Button
        {
            Text = onIcon,
            Location = new Point(x, y),
            Width = 40,
            Height = 40,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 14),
            Anchor = AnchorStyles.Top
        };

        bool currentState = true;
        btn.Text = onIcon;
        btn.BackColor = Color.LightGreen;

        btn.Click += (s, e) =>
        {
            currentState = !currentState;
            btn.BackColor = currentState ? Color.LightGreen : Color.LightGray;
            btn.Text = currentState ? onIcon : offIcon;
            onToggle(currentState);
        };

        // Set tooltip via shared ToolTip component
        _tabToolTip.SetToolTip(btn, tooltip);

        return btn;
    }

    // --- Tab Management ---

    private void RefreshTabStrip()
    {
        // Remove all tab controls
        var controlsToRemove = _tabStrip.Controls
            .Cast<Control>()
            .Where(c => c != _btnAddGrid)
            .ToList();

        foreach (var ctrl in controlsToRemove)
        {
            _tabStrip.Controls.Remove(ctrl);
            ctrl.Dispose();
        }

        foreach (var grid in _map.Grids)
        {
            CreateTabForGrid(grid);
        }
    }

    private void CreateTabForGrid(Grid grid)
    {
        var tabPanel = new Panel
        {
            Tag = grid,
            Size = new Size(120, 55),
            BackColor = grid.Uid == _map.ActiveGridUid ? Color.FromArgb(255, 255, 255) : Color.FromArgb(225, 225, 225),
            Margin = new Padding(0, 0, 2, 0),
            BorderStyle = BorderStyle.None
        };

        // Tab name label
        var label = new Label
        {
            Text = grid.Name,
            Location = new Point(4, 4),
            Size = new Size(80, 20),
            Font = new Font("Segoe UI", 9),
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = grid.Uid == _map.ActiveGridUid ? Color.Black : Color.FromArgb(60, 60, 60)
        };
        tabPanel.Click += (s, e) =>
        {
            if (_map.ActiveGridUid != grid.Uid)
            {
                _map.ActiveGridUid = grid.Uid;
                UpdateTileGrid();
                Render();
            }
            RefreshTabStrip();
        };
        tabPanel.Cursor = Cursors.Hand;
        tabPanel.DoubleClick += (s, e) =>
        {
            ShowRenameDialog(grid);
        };
        tabPanel.Controls.Add(label);

        // Close button (×)
        var closeBtn = new Button
        {
            Text = "×",
            Location = new Point(88, 2),
            Size = new Size(28, 24),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = grid.Uid == _map.ActiveGridUid ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160),
            Margin = Padding.Empty
        };
        closeBtn.MouseEnter += (s, e) => { closeBtn.BackColor = Color.FromArgb(220, 80, 80); closeBtn.ForeColor = Color.White; };
        closeBtn.MouseLeave += (s, e) =>
        {
            closeBtn.BackColor = Color.Transparent;
            closeBtn.ForeColor = grid.Uid == _map.ActiveGridUid ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160);
        };
        closeBtn.Click += (s, e) => RemoveGridTab(grid.Uid);
        tabPanel.Controls.Add(closeBtn);

        // Right-click context menu
        tabPanel.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                _contextMenuGrid = grid;
                if (_map.Grids.Count > 1)
                {
                    _tabContextMenu.Items.Find("deleteItem", true)[0].Enabled = true;
                    _tabContextMenu.Items.Find("addAfterItem", true)[0].Enabled = true;
                }
                else
                {
                    _tabContextMenu.Items.Find("deleteItem", true)[0].Enabled = false;
                    _tabContextMenu.Items.Find("addAfterItem", true)[0].Enabled = false;
                }
                _tabContextMenu.Show(tabPanel, e.Location);
            }
        };

        // Hover effect
        tabPanel.MouseEnter += (s, e) =>
        {
            if (grid.Uid != _map.ActiveGridUid)
            {
                tabPanel.BackColor = Color.FromArgb(240, 240, 240);
            }
        };
        tabPanel.MouseLeave += (s, e) =>
        {
            if (grid.Uid != _map.ActiveGridUid)
            {
                tabPanel.BackColor = Color.FromArgb(225, 225, 225);
            }
        };

        // Position tab next to the "+" button using fixed offset
        int nextX = TAB_START_X + (_tabStrip.Controls.Count - 1) * (120 + 2); // 2 = margin
        tabPanel.Left = nextX;
        tabPanel.Top = 0;

        _tabStrip.Controls.Add(tabPanel);
    }

    private void SetTabActive(Control tabControl, bool active)
    {
        var tabPanel = tabControl as Panel;
        if (tabPanel == null || tabPanel.Tag is not Grid grid) return;

        tabPanel.BackColor = active ? Color.FromArgb(255, 255, 255) : Color.FromArgb(225, 225, 225);

        var label = tabPanel.Controls.OfType<Label>().FirstOrDefault();
        if (label != null)
        {
            label.ForeColor = active ? Color.Black : Color.FromArgb(60, 60, 60);
        }

        var closeBtn = tabPanel.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "×");
        if (closeBtn != null)
        {
            closeBtn.ForeColor = active ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160);
        }
    }

    private void AddGridTab(int index)
    {
        var newUid = _map.Grids.Any() ? _map.Grids.Max(g => g.Uid) + 1 : 1;
        var grid = new Grid
        {
            Uid = newUid,
            Name = $"Слой {newUid}",
            Position = new PointF(10, 10),
            Color = Color.FromArgb(
                Random.Shared.Next(100, 200),
                Random.Shared.Next(100, 200),
                Random.Shared.Next(100, 200)
            )
        };

        _map.Grids.Add(grid);
        _map.ActiveGridUid = grid.Uid;

        RefreshTabStrip();
        UpdateTileGrid();
        Render();
    }

    private void RemoveGridTab(int uid)
    {
        if (_map.Grids.Count <= 1) return;

        _map.RemoveGrid(uid);
        RefreshTabStrip();
        UpdateTileGrid();
        Render();
    }

    // --- Context Menu Actions ---

    private void ShowRenameDialog(Grid grid)
    {
        var dlg = new Form
        {
            Text = "Переименовать слой",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new Size(320, 110)
        };

        var label = new Label
        {
            Text = "Новое имя:",
            Location = new Point(12, 15),
            Size = new Size(80, 20),
            Font = new Font("Segoe UI", 9)
        };
        dlg.Controls.Add(label);

        var textBox = new TextBox
        {
            Location = new Point(100, 12),
            Width = 190,
            Text = grid.Name,
            Font = new Font("Segoe UI", 9)
        };
        dlg.Controls.Add(textBox);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(80, 50),
            Width = 75,
            DialogResult = DialogResult.OK
        };
        dlg.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(170, 50),
            Width = 75,
            DialogResult = DialogResult.Cancel
        };
        dlg.Controls.Add(btnCancel);

        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        textBox.SelectAll();
        textBox.Focus();

        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            grid.Name = textBox.Text.Trim();
            RefreshTabStrip();
        }
        dlg.Dispose();
    }

    private void RenameContextGrid()
    {
        if (_contextMenuGrid != null)
        {
            ShowRenameDialog(_contextMenuGrid);
        }
    }

    private void AddGridAfterContext()
    {
        if (_contextMenuGrid == null) return;

        var newUid = _map.Grids.Any() ? _map.Grids.Max(g => g.Uid) + 1 : 1;
        var grid = new Grid
        {
            Uid = newUid,
            Name = $"Слой {newUid}",
            Position = new PointF(10, 10),
            Color = Color.FromArgb(
                Random.Shared.Next(100, 200),
                Random.Shared.Next(100, 200),
                Random.Shared.Next(100, 200)
            )
        };

        var idx = _map.Grids.IndexOf(_contextMenuGrid);
        _map.Grids.Insert(idx + 1, grid);
        _map.ActiveGridUid = grid.Uid;

        RefreshTabStrip();
        UpdateTileGrid();
        Render();
    }

    private void DeleteContextGrid()
    {
        if (_contextMenuGrid == null || _map.Grids.Count <= 1) return;

        var uid = _contextMenuGrid.Uid;
        _contextMenuGrid = null;
        RemoveGridTab(uid);
    }

    // --- Initialization ---

    private void InitGridTabs()
    {
        RefreshTabStrip();
    }
}
