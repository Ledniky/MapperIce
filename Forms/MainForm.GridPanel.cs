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
        const int tabWidth = 120;
        const int tabHeight = 55;
        const int btnSize = 22;
        const int bottomY = 30;

        var tabPanel = new Panel
        {
            Tag = grid,
            Size = new Size(tabWidth, tabHeight),
            BackColor = grid.Uid == _map.ActiveGridUid ? Color.FromArgb(255, 255, 255) : Color.FromArgb(225, 225, 225),
            Margin = new Padding(0, 0, 2, 0),
            BorderStyle = BorderStyle.None
        };

        Color defaultBtnColor = grid.Uid == _map.ActiveGridUid ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160);

        // Tab name label (top)
        var label = new Label
        {
            Text = grid.Name,
            Location = new Point(4, 4),
            Size = new Size(tabWidth - 8, 20),
            Font = new Font("Segoe UI", 9),
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = grid.Uid == _map.ActiveGridUid ? Color.Black : Color.FromArgb(60, 60, 60),
            TextAlign = ContentAlignment.MiddleCenter
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

        // --- Bottom row: ◀ ▶ ⚙ × (all right-aligned) ---
        int totalBtns = 4;
        int groupWidth = btnSize * totalBtns + 3 * 2; // 2 = gap between buttons
        int startX = tabWidth - groupWidth - 2;

        // Left arrow
        var btnLeft = new Button
        {
            Text = "◀",
            Location = new Point(startX, bottomY),
            Size = new Size(btnSize, btnSize),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = defaultBtnColor,
            Margin = Padding.Empty
        };
        btnLeft.MouseEnter += (s, e) => { btnLeft.BackColor = Color.FromArgb(200, 200, 200); };
        btnLeft.MouseLeave += (s, e) => { btnLeft.BackColor = Color.Transparent; };
        btnLeft.Click += (s, e) => MoveGridLeft(grid);
        tabPanel.Controls.Add(btnLeft);

        // Right arrow
        var btnRight = new Button
        {
            Text = "▶",
            Location = new Point(startX + btnSize + 2, bottomY),
            Size = new Size(btnSize, btnSize),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = defaultBtnColor,
            Margin = Padding.Empty
        };
        btnRight.MouseEnter += (s, e) => { btnRight.BackColor = Color.FromArgb(200, 200, 200); };
        btnRight.MouseLeave += (s, e) => { btnRight.BackColor = Color.Transparent; };
        btnRight.Click += (s, e) => MoveGridRight(grid);
        tabPanel.Controls.Add(btnRight);

        // Settings gear
        var btnSettings = new Button
        {
            Text = "⚙",
            Location = new Point(startX + (btnSize + 2) * 2, bottomY),
            Size = new Size(btnSize, btnSize),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent,
            ForeColor = defaultBtnColor,
            Margin = Padding.Empty
        };
        btnSettings.MouseEnter += (s, e) => { btnSettings.BackColor = Color.FromArgb(200, 200, 200); };
        btnSettings.MouseLeave += (s, e) => { btnSettings.BackColor = Color.Transparent; };
        btnSettings.Click += (s, e) => ShowLayerSettingsDialog(grid);
        tabPanel.Controls.Add(btnSettings);

        // Close button (×)
        var closeBtn = new Button
        {
            Text = "×",
            Location = new Point(startX + (btnSize + 2) * 3, bottomY),
            Size = new Size(btnSize, btnSize),
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
        int nextX = TAB_START_X + (_tabStrip.Controls.Count - 1) * (tabWidth + 2); // 2 = margin
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

        var grid = _map.Grids.First(g => g.Uid == uid);
        if (!ShowConfirmDialog($"Удалить слой «{grid.Name}»?")) return;

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

    private void MoveGridLeft(Grid grid)
    {
        var idx = _map.Grids.IndexOf(grid);
        if (idx <= 0) return;

        (_map.Grids[idx - 1], _map.Grids[idx]) = (_map.Grids[idx], _map.Grids[idx - 1]);
        RefreshTabStrip();
        Render();
    }

    private void MoveGridRight(Grid grid)
    {
        var idx = _map.Grids.IndexOf(grid);
        if (idx < 0 || idx >= _map.Grids.Count - 1) return;

        (_map.Grids[idx], _map.Grids[idx + 1]) = (_map.Grids[idx + 1], _map.Grids[idx]);
        RefreshTabStrip();
        Render();
    }

    private void ShowLayerSettingsDialog(Grid grid)
    {
        // TODO: реализовать настройки слоя
        var dlg = new Form
        {
            Text = "Настройки слоя",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new Size(280, 100),
            TopMost = true
        };

        var label = new Label
        {
            Text = $"Настройки слоя: {grid.Name}",
            Location = new Point(5, 5),
            Size = new Size(250, 20),
            Font = new Font("Segoe UI", 10)
        };
        dlg.Controls.Add(label);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(100, 25),
            Width = 75,
            DialogResult = DialogResult.OK
        };
        dlg.Controls.Add(btnOk);
        dlg.AcceptButton = btnOk;

        dlg.Show(this);
    }

    private bool ShowConfirmDialog(string message)
    {
        var dlg = new Form
        {
            Text = "Подтверждение",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new Size(280, 110),
            TopMost = true
        };

        var label = new Label
        {
            Text = message,
            Location = new Point(15, 12),
            Size = new Size(250, 30),
            Font = new Font("Segoe UI", 10)
        };
        dlg.Controls.Add(label);

        var btnYes = new Button
        {
            Text = "Да",
            Location = new Point(70, 50),
            Width = 65,
            DialogResult = DialogResult.Yes,
            Font = new Font("Segoe UI", 9)
        };
        dlg.Controls.Add(btnYes);

        var btnNo = new Button
        {
            Text = "Нет",
            Location = new Point(145, 50),
            Width = 65,
            DialogResult = DialogResult.No,
            Font = new Font("Segoe UI", 9)
        };
        dlg.Controls.Add(btnNo);

        dlg.AcceptButton = btnYes;
        dlg.CancelButton = btnNo;

        return dlg.ShowDialog(this) == DialogResult.Yes;
    }

    private void ShowInfoDialog(string title, string message)
    {
        var dlg = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new Size(190, 220),
            TopMost = true
        };

        var label = new Label
        {
            Text = message,
            Location = new Point(20, 20),
            Size = new Size(340, 120),
            Font = new Font("Arial", 10),
            AutoSize = false
        };
        dlg.Controls.Add(label);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(100, 150),
            Width = 75,
            DialogResult = DialogResult.OK
        };
        dlg.Controls.Add(btnOk);
        dlg.AcceptButton = btnOk;

        dlg.Show(this);
    }

    // --- Initialization ---

    private void InitGridTabs()
    {
        RefreshTabStrip();
    }
}
