// Forms/MainForm.Magnifier.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    // ===== Инструмент "Лупа" — окошко с описанием объектов под курсором =====

    private void CreateMagnifierPanel()
    {
        _magnifierPanel = new Panel
        {
            Width = 260,
            Height = 190,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(255, 250, 250, 235),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.FromArgb(255, 225, 225, 190)
        };

        _magnifierHeaderLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🔍 Лупа",
            Font = new Font("Arial", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        headerPanel.Controls.Add(_magnifierHeaderLabel);

        _btnMagnifierUnpin = new Button
        {
            Dock = DockStyle.Right,
            Width = 82,
            Text = "🔓 Открепить",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Arial", 7),
            Visible = false
        };
        _btnMagnifierUnpin.Click += (s, e) =>
        {
            _magnifierPinned = false;
            _magnifierPinnedTile = null;
            UpdateMagnifierPanel();
        };
        headerPanel.Controls.Add(_btnMagnifierUnpin);

        _magnifierPanel.Controls.Add(headerPanel);

        _magnifierTextBox = new TextBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(255, 255, 255, 245),
            Font = new Font("Consolas", 8.5f)
        };
        _magnifierPanel.Controls.Add(_magnifierTextBox);

        _magnifierTextBox.Top = headerPanel.Height;
        _magnifierTextBox.Width = _magnifierPanel.Width;

        _magnifierPanel.Resize += (s, e) =>
        {
            if (_magnifierTextBox != null)
            {
                _magnifierTextBox.Width = _magnifierPanel.Width;
                _magnifierTextBox.Height = _magnifierPanel.Height - headerPanel.Height;
            }
        };

        headerPanel.BringToFront();

        _canvas.Controls.Add(_magnifierPanel);
        _magnifierPanel.BringToFront();

        // На момент вызова этого метода (конструктор MainForm) реальный докинг ещё
        // не выполнен — форма ещё не показана, WindowState=Maximized ещё не применён,
        // поэтому _canvas.Width тут может быть равен начальному Size(1024,768) формы,
        // а не финальному размеру экрана. Пересчёт всё равно происходит (на случай,
        // если размеры уже корректны), но окончательно верное положение гарантируется
        // повторным вызовом из OnResize (см. MainForm.cs) и из OnToolChanged при
        // каждом включении инструмента — оба момента наступают уже после реального
        // докинга.
        RepositionMagnifierPanel();
    }

    /// <summary>
    /// Пересчитывает экранное положение окошка "Лупа" в правом нижнем углу канвы.
    /// Отступ справа равен ширине панели инструментов (200px), отступ снизу — 10px.
    /// </summary>
    private void RepositionMagnifierPanel()
    {
        if (_magnifierPanel == null || _canvas == null) return;
        if (_canvas.Width <= 0 || _canvas.Height <= 0) return;

        const int rightMargin = 200; // ширина панели инструментов
        _magnifierPanel.Location = new Point(
            Math.Max(0, _canvas.Width - _magnifierPanel.Width - rightMargin),
            Math.Max(0, _canvas.Height - _magnifierPanel.Height - 10));

        if (_magnifierTextBox != null)
        {
            _magnifierTextBox.Top = 22;
            _magnifierTextBox.Width = _magnifierPanel.Width;
        }
    }

    /// <summary>
    /// Пересчитывает содержимое окошка "Лупа". В обычном режиме — по клетке под
    /// текущим положением курсора (_lastMousePosition), обновляется на каждое
    /// движение мыши. После ЛКМ по тайлу (см. OnMouseDown в MainForm.MouseInput.cs)
    /// содержимое "фиксируется" (_magnifierPinned=true) на зафиксированной клетке
    /// (_magnifierPinnedTile) и больше не следует за курсором, пока пользователь
    /// не нажмёт "Открепить" или не переключит инструмент (см. OnToolChanged).
    /// </summary>
    private void UpdateMagnifierPanel()
    {
        if (_magnifierPanel == null || _magnifierTextBox == null || _magnifierHeaderLabel == null)
            return;

        var grid = _map.ActiveGrid;
        if (grid == null)
        {
            _magnifierHeaderLabel.Text = "🔍 Лупа — нет активного грида";
            _magnifierTextBox.Text = "";
            if (_btnMagnifierUnpin != null) _btnMagnifierUnpin.Visible = false;
            return;
        }

        int tileX, tileY;
        if (_magnifierPinned && _magnifierPinnedTile.HasValue)
        {
            tileX = _magnifierPinnedTile.Value.x;
            tileY = _magnifierPinnedTile.Value.y;
        }
        else
        {
            var tilePos = GetTilePosition(_lastMousePosition);
            tileX = tilePos.x;
            tileY = tilePos.y;
        }

        if (_btnMagnifierUnpin != null)
            _btnMagnifierUnpin.Visible = _magnifierPinned;

        string pinMark = _magnifierPinned ? " [закреплено]" : "";
        _magnifierHeaderLabel.Text = $"🔍 Тайл ({tileX}, {tileY}){pinMark}";

        var lines = new List<string>();

        // Пол
        var floorTile = _tileGrid.GetTilesByContent(TileContent.Floor)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (floorTile != null)
            lines.Add($"Пол: {floorTile.ProtoId ?? "Plating"}");

        // Стена
        var wallTile = _tileGrid.GetTilesByContent(TileContent.Wall)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (wallTile != null)
            lines.Add($"Стена: {wallTile.ProtoId ?? "WallSolid"}");

        // Дверь (+ пол под ней, если есть)
        var doorTile = _tileGrid.GetTilesByContent(TileContent.Door)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (doorTile != null)
        {
            string doorLine = $"Дверь: {doorTile.ProtoId ?? "Airlock"}";
            if (doorTile.HasFloorUnder && !string.IsNullOrEmpty(doorTile.FloorProtoUnder))
                doorLine += $" (пол под: {doorTile.FloorProtoUnder})";
            lines.Add(doorLine);
        }

        // Вручную размещённые тайлы (инструмент "Разместить прототип" → tile)
        foreach (var pt in grid.Tiles.Where(t => t.X == tileX && t.Y == tileY))
            lines.Add($"Ручной тайл: {pt.Proto}");

        // Комнаты, реально владеющие этой клеткой (учитывает вырезы через Room.Contains)
        foreach (var room in grid.Rooms.Where(r => r.Contains(tileX, tileY)))
            lines.Add($"Комната: {room.RoomType} [{room.Width}×{room.Height} @ ({room.X},{room.Y})]");

        // Сущности — попадание проверяем через floor(X)/floor(Y), как и весь
        // остальной код (HitTestAt/Delete), чтобы декали/сущности с дробными
        // координатами у края клетки тоже засчитывались как "в этом тайле"
        foreach (var entity in grid.Entities)
        {
            if (FloorToInt(entity.X) != tileX || FloorToInt(entity.Y) != tileY) continue;

            switch (entity)
            {
                case PipeEntity pipe:
                    {
                        string pipeLine = $"Труба [{pipe.PipeType}]";
                        if (pipe.CustomColor.HasValue)
                        {
                            var c = pipe.CustomColor.Value;
                            pipeLine += $" цвет:#{c.R:X2}{c.G:X2}{c.B:X2}";
                        }
                        if (pipe.HasFilterMarker && !string.IsNullOrEmpty(pipe.FilterLabel))
                            pipeLine += $" метка:\"{pipe.FilterLabel}\"";
                        pipeLine += pipe.EndpointType switch
                        {
                            EndpointType.MailingUnit => " [MailingUnit]",
                            EndpointType.DisposalUnit => " [DisposalUnit]",
                            _ => ""
                        };
                        lines.Add(pipeLine);
                        break;
                    }
                case FirelockEntity firelock:
                    lines.Add($"Огнешлюз: {firelock.Proto}{(firelock.IsGlass ? " (стекло)" : "")}");
                    break;
                case AirAlarmEntity air:
                    lines.Add($"Сигнализация воздуха (поворот {air.Rotation * 180 / Math.PI:F0}°)");
                    break;
                case FireAlarmEntity fire:
                    lines.Add($"Сигнализация пожара (поворот {fire.Rotation * 180 / Math.PI:F0}°)");
                    break;
                default:
                    lines.Add($"Сущность: {entity.Proto}");
                    break;
            }
        }

        // Декали
        foreach (var decal in grid.Decals)
        {
            if (FloorToInt(decal.X) != tileX || FloorToInt(decal.Y) != tileY) continue;
            lines.Add($"Декаль: {decal.Proto} [{decal.Color}, поворот {decal.Rotation * 180 / Math.PI:F0}°]");
        }

        _magnifierTextBox.Text = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "(пусто)";
    }
}