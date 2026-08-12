using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

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
}
