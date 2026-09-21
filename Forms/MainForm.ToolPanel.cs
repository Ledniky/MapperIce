// Forms/MainForm.ToolPanel.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

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
        DragDropCursorHelper.SuppressForbiddenCursor(_toolPanel);

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

        // === ЗАГОЛОВОК СЕКЦИИ ===
        var roomsLabel = new Label
        {
            Text = "Комнаты:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(roomsLabel);
        y += 20 + 2;

        // === ВЫБОР ТИПА КОМНАТЫ (ComboBox с ToolTip) ===
        _roomTypeCombo = new ComboBox
        {
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Arial", 9),
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        _roomTypeCombo.DataSource = _roomTypeManager.GetAllTypeNames().OrderBy(n => n).ToList();
        _roomTypeCombo.SelectedItem = _roomTypeManager.SelectedType;
        _roomTypeCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_roomTypeCombo.SelectedItem != null)
            {
                _roomTypeManager.SelectType(_roomTypeCombo.SelectedItem.ToString()!);
                if (_currentRoom != null)
                {
                    _roomTypeManager.ApplyTypeToRoom(_currentRoom);
                    UpdateTileGrid();
                    Render();
                }
            }
        };
        _roomTypeCombo.MouseHover += (s, e) =>
        {
            var roomType = _roomTypeManager.GetRoomType(_roomTypeCombo.SelectedItem?.ToString());
            if (roomType != null && !string.IsNullOrEmpty(roomType.Description))
            {
                _roomTypeTooltip.SetToolTip(_roomTypeCombo, roomType.Description);
            }
            else
            {
                _roomTypeTooltip.SetToolTip(_roomTypeCombo, "");
            }
        };
        _roomTypeCombo.MouseLeave += (s, e) =>
        {
            _roomTypeTooltip.SetToolTip(_roomTypeCombo, "");
        };
        _toolPanel.Controls.Add(_roomTypeCombo);
        y += 25 + 4;

        // Подписываемся на изменение типа извне (например, из диалога RoomTypeDialog)
        _roomTypeManager.OnTypeChanged += () =>
        {
            if (_roomTypeCombo.InvokeRequired)
            {
                _roomTypeCombo.Invoke(new Action(() =>
                {
                    _roomTypeCombo.SelectedItem = _roomTypeManager.SelectedType;
                }));
            }
            else
            {
                _roomTypeCombo.SelectedItem = _roomTypeManager.SelectedType;
            }
        };

        // === КОМНАТЫ ===
        // Строка 1: большая кнопка "Создать комнату" + маленькая шестерёнка настроек
        var roomRow1Panel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        _btnCreateRoom = new Button
        {
            Text = "➕ Добавить",
            Location = new Point(0, 0),
            Width = contentWidth - 50,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Arial", 12)
        };
        _btnCreateRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.CreateRoom);
        };
        roomRow1Panel.Controls.Add(_btnCreateRoom);

        _btnRoomSettings = new Button
        {
            Text = "⚙",
            Location = new Point(contentWidth - 44, 0),
            Width = 40,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnRoomSettings.Click += (s, e) => ShowRoomTypeDialog();
        roomRow1Panel.Controls.Add(_btnRoomSettings);

        roomRow1Panel.Resize += (s, e) =>
        {
            _btnCreateRoom.Width = roomRow1Panel.Width - 50;
            _btnRoomSettings.Location = new Point(roomRow1Panel.Width - 44, 0);
        };
        _toolPanel.Controls.Add(roomRow1Panel);
        y += 34 + 2;

        // Строка 2: вычесть, восстановить и добавить всё (по 33% каждая)
        var roomRow2Panel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        int btnWidth = roomRow2Panel.Width / 3;

        _btnSubtractRoom = new Button
        {
            Text = "✂️",
            Location = new Point(0, 0),
            Width = btnWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Arial", 14)
        };
        _btnSubtractRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.SubtractRoom);
        };
        roomRow2Panel.Controls.Add(_btnSubtractRoom);

        _btnRestoreRoom = new Button
        {
            Text = "🔨",
            Location = new Point(btnWidth, 0),
            Width = btnWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Arial", 14)
        };
        _btnRestoreRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.RestoreRoom);
        };
        roomRow2Panel.Controls.Add(_btnRestoreRoom);

        _btnAddAll = new Button
        {
            Text = "➕",
            Location = new Point(btnWidth * 2, 0),
            Width = btnWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Arial", 14)
        };
        _btnAddAll.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.ExpandRoom);
        };
        roomRow2Panel.Controls.Add(_btnAddAll);

        roomRow2Panel.Resize += (s, e) =>
        {
            int bw = roomRow2Panel.Width / 3;
            _btnSubtractRoom.Width = bw - 1;
            _btnRestoreRoom.Location = new Point(bw, 0);
            _btnRestoreRoom.Width = bw - 1;
            _btnAddAll.Location = new Point(bw * 2, 0);
            _btnAddAll.Width = bw - 1;
        };

        _toolPanel.Controls.Add(roomRow2Panel);
        y += 34 + 2;

        _doorToolCombo = new ComboBox
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 34,
            DropDownHeight = 160,
            Font = new Font("Arial", 10)
        };
        foreach (var label in _doorToolMap.Keys)
            _doorToolCombo.Items.Add(label);
        if (_doorToolCombo.Items.Count > 0)
            _doorToolCombo.SelectedIndex = 0;

        // Рисуем реальный спрайт прототипа (если есть и уже загружен через
        // GetCachedProtoIcon), иначе — emoji-заглушку из _doorToolIcons.Fallback.
        // Тот же приём, что и в ProtoList_DrawItem: иконка слева, текст справа от неё.
        _doorToolCombo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                string label = _doorToolCombo.Items[e.Index].ToString() ?? "";
                const int iconSize = 28;
                const int padding = 4;
                int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                int textX = e.Bounds.Left + padding;

                Image? icon = null;
                string fallback = "";
                if (_doorToolIcons.TryGetValue(label, out var info))
                {
                    fallback = info.Fallback;
                    icon = info.ProtoId switch
                    {
                        "Airlock" => _doorIconAirlock,
                        "AirlockGlass" => _doorIconAirlockGlass,
                        _ => null
                    };
                }

                if (icon != null)
                {
                    int drawY = e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2;
                    e.Graphics.DrawImage(icon, e.Bounds.Left + padding, drawY, icon.Width, icon.Height);
                    textX = e.Bounds.Left + padding + iconSize + padding;
                }
                else if (!string.IsNullOrEmpty(fallback))
                {
                    TextRenderer.DrawText(e.Graphics, fallback, new Font("Segoe UI", 14),
                        new Rectangle(e.Bounds.Left + padding, iconY, iconSize, iconSize),
                        e.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    textX = e.Bounds.Left + padding + iconSize + padding;
                }

                TextRenderer.DrawText(e.Graphics, label, _doorToolCombo.Font,
                    new Rectangle(textX, e.Bounds.Top, e.Bounds.Width - (textX - e.Bounds.Left), e.Bounds.Height),
                    _doorToolCombo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        };

        _doorToolCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_doorToolCombo.SelectedItem is string key)
            {
                // Проверяем, является ли пункт прототипом окна (начинается с 🪟)
                if (key.StartsWith("🪟 "))
                {
                    // Извлекаем ID прототипа — убираем префикс "🪟 "
                    string protoName = key.Substring(3);
                    // Ищем полный ID в индексированных прототипах
                    var allIds = _indexer.GetPrototypeIds();
                    string? foundId = allIds.FirstOrDefault(id =>
                        id.Replace("Window", "").TrimStart('-', '_') == protoName ||
                        id.EndsWith(protoName, StringComparison.OrdinalIgnoreCase));
                    if (foundId != null)
                    {
                        _selectedWindowProto = foundId;
                        _toolManager.SetTool(ToolManager.Tool.ReplaceWallWithWindow);
                        return;
                    }
                }

                if (_doorToolMap.TryGetValue(key, out var tool))
                {
                    _toolManager.SetTool(tool);
                }
            }
        };
        _toolPanel.Controls.Add(_doorToolCombo);
        y += 40 + 2;

        // ЭЛЕКТРОСЕТЬ
        var wireLabel = new Label
        {
            Text = "Электросеть:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(wireLabel);
        y += 20 + 2;

        // Строка 1: выбор типа кабеля + кнопка настроек цветов
        var wireRow1Panel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        _wireTypeCombo = new ComboBox
        {
            Location = new Point(0, 0),
            Width = wireRow1Panel.Width - 42,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28,
            Font = new Font("Arial", 11)
        };
        foreach (var wt in _wireTypeManager.GetTypes())
            _wireTypeCombo.Items.Add(wt.DisplayName);
        if (_wireTypeCombo.Items.Count > 0)
            _wireTypeCombo.SelectedIndex = 0;
        _wireTypeCombo.SelectedIndexChanged += (s, e) =>
        {
            int idx = _wireTypeCombo.SelectedIndex;
            var types = _wireTypeManager.GetTypes();
            if (idx < 0 || idx >= types.Count) return;

            _currentWireLayer = types[idx].Name;
            _toolManager.SetTool(_currentWireLayer switch
            {
                "HV" => ToolManager.Tool.WireHV,
                "MV" => ToolManager.Tool.WireMV,
                _ => ToolManager.Tool.WireLV
            });
        };
        _wireTypeCombo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                string text = _wireTypeCombo.Items[e.Index].ToString() ?? "";
                // Карта иконок по индексу: 0=HV, 1=MV, 2=LV
                Image? icon = e.Index switch
                {
                    0 => _wireIconHV,
                    1 => _wireIconMV,
                    2 => _wireIconLV,
                    _ => null
                };
                int iconSize = 24;
                int iconX = e.Bounds.Left + 6;
                int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                if (icon != null)
                {
                    e.Graphics.DrawImage(icon, iconX, iconY, iconSize, iconSize);
                    TextRenderer.DrawText(e.Graphics, text, _wireTypeCombo.Font,
                        new Rectangle(iconX + iconSize + 4, e.Bounds.Top,
                            e.Bounds.Width - iconX - iconSize - 4, e.Bounds.Height),
                        _wireTypeCombo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, text, _wireTypeCombo.Font, e.Bounds, _wireTypeCombo.ForeColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
            e.DrawFocusRectangle();
        };
        wireRow1Panel.Controls.Add(_wireTypeCombo);

        _btnWireSettings = new Button
        {
            Text = "⚙",
            Location = new Point(wireRow1Panel.Width - 40, 0),
            Width = 40,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 14)
        };
        _btnWireSettings.Click += (s, e) => ShowWireSettingsDialog();
        wireRow1Panel.Controls.Add(_btnWireSettings);

        wireRow1Panel.Resize += (s, e) =>
        {
            _wireTypeCombo.Width = wireRow1Panel.Width - 42;
            _btnWireSettings.Location = new Point(wireRow1Panel.Width - 40, 0);
        };

        _toolPanel.Controls.Add(wireRow1Panel);
        y += 34 + 2;

        // Строка 2: выпадающий список прототипов электротехнических переходников (ЛКП/APC и подстанции) —
        // выбор пункта сразу активирует инструмент точечной установки переходника
        var wireRow2Panel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        _powerJunctionCombo = new ComboBox
        {
            Location = new Point(0, 0),
            Width = wireRow2Panel.Width,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28,
            Font = new Font("Arial", 10)
        };
        var powerJunctionTip = new ToolTip();
        powerJunctionTip.SetToolTip(_powerJunctionCombo, "Электротехнический переходник (ЛКП/Подстанция)");
        _powerJunctionCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_powerJunctionCombo.SelectedItem is ProtoDisplayItem item)
            {
                _selectedPowerJunctionProto = item.Id;
                _selectedPowerJunctionType = item.Type;
                _toolManager.SetTool(item.Type == "APC" ? ToolManager.Tool.PlaceApc : ToolManager.Tool.PlaceSubstation);
            }
        };
        _powerJunctionCombo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                var item = _powerJunctionCombo.Items[e.Index] as ProtoDisplayItem;
                string text = item?.DisplayName ?? _powerJunctionCombo.Items[e.Index].ToString() ?? "";
                const int iconSize = 24;
                const int padding = 3;
                int textX = e.Bounds.Left + padding;

                if (item != null)
                {
                    _wireJunctionIconCache.TryGetValue(item.Id, out var icon);
                    if (icon != null)
                    {
                        int drawX = e.Bounds.Left + padding + (iconSize - icon.Width) / 2;
                        int drawY = e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2;
                        e.Graphics.DrawImage(icon, drawX, drawY, icon.Width, icon.Height);
                    }
                    else
                    {
                        using var placeholderBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
                        int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                        e.Graphics.FillRectangle(placeholderBrush, e.Bounds.Left + padding, iconY, iconSize, iconSize);
                    }
                    textX = e.Bounds.Left + padding + iconSize + padding;
                }

                TextRenderer.DrawText(e.Graphics, text, _powerJunctionCombo.Font,
                    new Rectangle(textX, e.Bounds.Top, e.Bounds.Right - textX, e.Bounds.Height),
                    _powerJunctionCombo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        };
        wireRow2Panel.Controls.Add(_powerJunctionCombo);

        _toolPanel.Controls.Add(wireRow2Panel);
        y += 34 + 2;

        // === УТИЛИЗАЦИЯ ===
        var utilLabel = new Label
        {
            Text = "Утилизация:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(utilLabel);
        y += 20 + 2;

        var utilPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        const int utilBtnWidth = 48;
        const int utilComboGap = 4;

        _btnPipeUtil = new Button
        {
            Location = new Point(0, 0),
            Width = utilBtnWidth,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Util",
            Text = "U",
            Padding = new Padding(0)
        };
        _btnPipeUtil.Click += (s, e) =>
        {
            _currentPipeLayer = "Util";
            _toolManager.SetTool(ToolManager.Tool.PipeUtil);
        };
        utilPanel.Controls.Add(_btnPipeUtil);

        // Выпадающий список остальных инструментов утилизации — раньше это были
        // две отдельные кнопки (⚙ настройка стрелок / 🔧🔧 перекраска), теперь
        // они объединены в один ComboBox, чтобы освободить место в строке
        _utilToolCombo = new ComboBox
        {
            Location = new Point(utilBtnWidth + utilComboGap, 0),
            Width = utilPanel.Width - utilBtnWidth - utilComboGap,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28, // реальная высота поля = ItemHeight + рамка; подберите под 34px при необходимости
            Font = new Font("Arial", 12)
        };
        foreach (var label in _utilToolMap.Keys)
            _utilToolCombo.Items.Add(label);
        if (_utilToolCombo.Items.Count > 0)
            _utilToolCombo.SelectedIndex = 0;

        // При OwnerDrawFixed текст сам по себе не рисуется — приходится отрисовывать его вручную,
        // зато именно благодаря OwnerDraw высота поля перестаёт зависеть от размера шрифта
        _utilToolCombo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                string text = _utilToolCombo.Items[e.Index].ToString() ?? "";
                TextRenderer.DrawText(e.Graphics, text, _utilToolCombo.Font, e.Bounds, _utilToolCombo.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        };

        _utilToolCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_utilToolCombo.SelectedItem is string key && _utilToolMap.TryGetValue(key, out var tool))
            {
                _toolManager.SetTool(tool);
                // Для инструментов сброс не нужен — SetActiveTool сам подсветит пункт
            }
        };
        utilPanel.Controls.Add(_utilToolCombo);

        utilPanel.Resize += (s, e) =>
        {
            _utilToolCombo.Width = utilPanel.Width - utilBtnWidth - utilComboGap;
        };

        _toolPanel.Controls.Add(utilPanel);
        y += 34 + 2;

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
            Height = 34,
            BackColor = Color.Transparent
        };

        int buttonWidth = pipePanel.Width / 4;

        _btnPipeDistra = new Button
        {
            Location = new Point(0, 0),
            Width = buttonWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Distra",
            Text = "L2",
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Normal",
            Text = "L1",
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Waste",
            Text = "L3",
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 12)
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
        y += 34 + 2;

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
            Height = 34,
            BackColor = Color.Transparent
        };

        int alarmButtonWidth = alarmPanel.Width / 3;

        _btnAirAlarm = new Button
        {
            Location = new Point(0, 0),
            Width = alarmButtonWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔊",
            Font = new Font("Arial", 14)
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔥",
            Font = new Font("Arial", 14)
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 12)
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
        y += 34 + 2;




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
            Height = 34,
            BackColor = Color.Transparent
        };

        _btnMove = new Button
        {
            Text = "✥ Переместить",
            Location = new Point(0, 0),
            Width = movePanel.Width - 42,
            Height = 34,
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 12)
        };
        _btnMoveSettings.Click += (s, e) => ShowMoveSettingsDialog();
        movePanel.Controls.Add(_btnMoveSettings);

        movePanel.Resize += (s, e) =>
        {
            _btnMove.Width = movePanel.Width - 42;
            _btnMoveSettings.Location = new Point(movePanel.Width - 40, 0);
        };

        _toolPanel.Controls.Add(movePanel);
        y += 34 + 2;


        // === ЛУПА (отладка) ===
        var magnifierLabel = new Label
        {
            Text = "Отладка:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(magnifierLabel);
        y += 20 + 2;

        var magnifierRowPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            BackColor = Color.Transparent
        };

        _btnMagnifier = new Button
        {
            Text = "🔍 Лупа",
            Location = new Point(0, 0),
            Width = magnifierRowPanel.Width / 2 - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnMagnifier.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Magnifier);
        };
        magnifierRowPanel.Controls.Add(_btnMagnifier);

        _btnProjectSettings = new Button
        {
            Text = "Проект",
            Location = new Point(magnifierRowPanel.Width / 2 + 1, 0),
            Width = magnifierRowPanel.Width / 2 - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnProjectSettings.Click += (s, e) => ShowProjectSettingsDialog();
        magnifierRowPanel.Controls.Add(_btnProjectSettings);

        magnifierRowPanel.Resize += (s, e) =>
        {
            int halfW = magnifierRowPanel.Width / 2;
            _btnMagnifier.Width = halfW - 1;
            _btnProjectSettings.Location = new Point(halfW + 1, 0);
            _btnProjectSettings.Width = halfW - 1;
        };

        _toolPanel.Controls.Add(magnifierRowPanel);
        y += 34 + 2;

        var decalRuleLabel = new Label
        {
            Text = "Decal Rule:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(decalRuleLabel);
        y += 20 + 2;

        _btnDecalRule = new Button
        {
            Text = "🧱 Узор по периметру",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnDecalRule.Click += (s, e) => { _toolManager.SetTool(ToolManager.Tool.DecalRule); };
        _toolPanel.Controls.Add(_btnDecalRule);
        y += 34 + 2;

        // Отдельная кнопка — не инструмент канвы, а обычный диалог, работающий не с
        // конкретными установленными комнатами, а с абстрактными RoomType-классами
        _btnDecalInheritance = new Button
        {
            Text = "🌳 Наследование декалей",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnDecalInheritance.Click += (s, e) =>
                {
                    // Симметрично с диалогом конкретной комнаты (_decalRuleForm): одно окно,
                    // повторный клик по кнопке при уже открытом окне закрывает его, а не
                    // плодит новые копии. Плюс сбрасываем активный инструмент канвы — иначе
                    // "Узор по периметру" оставался включённым в фоне, и случайный клик по
                    // карте после закрытия окна наследования неожиданно открывал диалог
                    // конкретной комнаты
                    if (_decalInheritanceForm != null && !_decalInheritanceForm.IsDisposed)
                    {
                        _decalInheritanceForm.Close();
                        return;
                    }

                    _toolManager.ResetTool();

                    _decalInheritanceForm = new DecalInheritanceDialog(_decalInheritanceManager, _decalPackManager, _indexer);
                    _btnDecalInheritance.BackColor = Color.LightBlue;
                    _decalInheritanceForm.FormClosed += (fs, fe) =>
                    {
                        _decalInheritanceForm = null;
                        _btnDecalInheritance.BackColor = Color.White;
                    };
                    _decalInheritanceForm.Show(this);
                };
        _toolPanel.Controls.Add(_btnDecalInheritance);
        y += 34 + 2;







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
            Height = 34,
            BackColor = Color.Transparent
        };

        int deleteButtonWidth = deletePanel.Width / 3;

        _btnDelete = new Button
        {
            Text = "🗑",
            Location = new Point(0, 0),
            Width = deleteButtonWidth - 1,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 14),
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 14),
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
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 12),
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
        y += 34 + 2;

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


    /// <summary>
    /// Наполняет выпадающий список электротехнических переходников ID-шниками из проиндексированного
    /// репозитория. Вызывается через _indexer.OnIndexingComplete — при первом запуске
    /// список пуст, пока не выбран и не проиндексирован хотя бы один репозиторий.
    /// </summary>
    private void UpdateWireJunctionCombos()
    {
        if (_powerJunctionCombo == null) return;

        var allIds = _indexer.GetPrototypeIds();

        string? prevProto = _selectedPowerJunctionProto;
        string? prevType = _selectedPowerJunctionType;
        _powerJunctionCombo.Items.Clear();
        
        foreach (var id in allIds.Where(i =>
            i.StartsWith("APC", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("Frame", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("Electronics", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("Switch", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("MachineCircuit", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("board", StringComparison.OrdinalIgnoreCase)))
        {
            var displayName = id.Replace("APC", "").TrimStart('-', '_');
            _powerJunctionCombo.Items.Add(new ProtoDisplayItem(id, displayName, "APC"));
        }
        
        foreach (var id in allIds.Where(i =>
            i.StartsWith("Substation", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("MachineCircuit", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("board", StringComparison.OrdinalIgnoreCase)&&
            !i.Contains("Frame", StringComparison.OrdinalIgnoreCase)))
        {
            var displayName = id.Replace("Substation", "").TrimStart('-', '_');
            _powerJunctionCombo.Items.Add(new ProtoDisplayItem(id, displayName, "Substation"));
        }
        
        // Восстановление выбранного элемента
        if (prevProto != null && prevType != null)
        {
            var selectedItem = _powerJunctionCombo.Items.Cast<ProtoDisplayItem>()
                .FirstOrDefault(p => p.Id == prevProto && p.Type == prevType);
            if (selectedItem != null)
                _powerJunctionCombo.SelectedItem = selectedItem;
        }
        else if (_powerJunctionCombo.Items.Count > 0)
        {
            _powerJunctionCombo.SelectedIndex = 0;
        }

        LoadWireJunctionIcons();
    }

    /// <summary>
    /// Синхронно (как LoadDoorComboIcons) подгружает иконки для всех пунктов
    /// _powerJunctionCombo — GetPrototypeIcon уже поддерживает
    /// многослойные прототипы (берёт первый видимый слой, см. репозиторную
    /// панель). Без этого иконки были бы видны только после наведения курсора,
    /// т.к. закрытый ComboBox сам себя не перерисовывает по завершении
    /// фоновой загрузки (в отличие от постоянно видимого _protoList).
    /// </summary>
    private void LoadWireJunctionIcons()
    {
        foreach (var kvp in _wireJunctionIconCache)
            kvp.Value?.Dispose();
        _wireJunctionIconCache.Clear();

        foreach (var item in _powerJunctionCombo.Items.Cast<ProtoDisplayItem>())
        {
            if (!_wireJunctionIconCache.ContainsKey(item.Id))
                _wireJunctionIconCache[item.Id] = GetPrototypeIcon(item.Id);
        }

        _powerJunctionCombo.Invalidate();
    }

    /// <summary>
    /// Синхронно подгружает иконки для пунктов _wireTypeCombo:
    /// HV → CableHV (hvcable_15), MV → CableMV (mvcable_15),
    /// LV → CableApcExtension (lvcable_15).
    /// Состояние _15 — спрайт с 4 соседями (полный сегмент).
    /// </summary>
    private void LoadWireTypeIcons()
    {
        _wireIconHV?.Dispose();
        _wireIconMV?.Dispose();
        _wireIconLV?.Dispose();
        _wireIconHV = GetPrototypeIcon("CableHV", "hvcable_15");
        _wireIconMV = GetPrototypeIcon("CableMV", "mvcable_15");
        _wireIconLV = GetPrototypeIcon("CableApcExtension", "lvcable_15");
        if (_wireTypeCombo != null && !_wireTypeCombo.IsDisposed) _wireTypeCombo.Invalidate();
    }

    /// <summary>
    /// Наполняет выпадающий список окон прототипами, содержащими "Window",
    /// но НЕ содержащими "Directional" и "Diagonal".
    /// </summary>
    private void UpdateWindowCombo()
    {
        if (_doorToolCombo == null) return;

        var allIds = _indexer.GetPrototypeIds();

        string? prevProto = _selectedWindowProto;
        _doorToolCombo.Items.Clear();

        // Восстанавливаем старый список + добавляем новый пункт
        foreach (var label in _doorToolMap.Keys)
            _doorToolCombo.Items.Add(label);

        foreach (var id in allIds.Where(i =>
            i.Contains("Window", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("Directional", StringComparison.OrdinalIgnoreCase) &&
            !i.Contains("Diagonal", StringComparison.OrdinalIgnoreCase)))
        {
            var displayName = id.Replace("Window", "").TrimStart('-', '_');
            if (string.IsNullOrEmpty(displayName)) displayName = "Window";
            _doorToolCombo.Items.Add($"🪟 {displayName}");
        }

        if (_doorToolCombo.Items.Count > 0)
            _doorToolCombo.SelectedIndex = 0;

        // Восстанавливаем выбранное окно
        if (prevProto != null)
        {
            for (int i = 0; i < _doorToolCombo.Items.Count; i++)
            {
                var item = _doorToolCombo.Items[i].ToString();
                if (item != null && item.EndsWith(prevProto, StringComparison.OrdinalIgnoreCase))
                {
                    _doorToolCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }


    /// <summary>
    /// Обёртка для отображения прототипа в ComboBox: хранит оригинальный ID,
    /// отформатированное имя для отображения и тип (APC или Substation).
    /// </summary>
    private class ProtoDisplayItem
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Type { get; } // "APC" или "Substation"
        public ProtoDisplayItem(string id, string displayName, string type)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
        }
        public override string ToString() => DisplayName;
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
}
