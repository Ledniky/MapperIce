// Forms/MainForm.LinkedEntitiesPanel.cs
//
// Панель «Привязанные сущности» — отдельная панель справа, которая открывается
// поверх панели инструментов (или подменяет её) по клику на кнопку
// «Привязанные сущности» в панели инструментов. В верхней части панели есть
// кнопка «Назад», закрывающая её. Сюда перенесён выпадающий список дверей,
// который раньше находился в панели инструментов (см. _doorToolCombo).

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    private Panel _linkedEntitiesPanel = null!;
    private Button _btnLinkedEntities = null!;

    // === ПАНЕЛЬ ПРИВЯЗАННЫХ СУЩНОСТЕЙ ===
    private void CreateLinkedEntitiesPanel()
    {
        _linkedEntitiesPanel = new Panel
        {
            Dock = DockStyle.None,
            Width = 200,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.None,
            Padding = new Padding(0),
            Visible = false
        };
        DragDropCursorHelper.SuppressForbiddenCursor(_linkedEntitiesPanel);

        // Панель подключаем к форме сразу, хотя показывается она позже (Visible = false):
        // тема применяется к уже подключённым контролам, а окно запоминается как
        // оформленное (см. MainForm.ThemeOpenForms) — добавленная по клику панель
        // иначе оставалась бы светлой при тёмной теме.
        Controls.Add(_linkedEntitiesPanel);

        var leftLine = new Panel
        {
            Dock = DockStyle.Left,
            Width = 1,
            BackColor = AppTheme.PanelEdge
        };
        _linkedEntitiesPanel.Controls.Add(leftLine);

        int leftMargin = 2;
        int rightMargin = 2;
        int y = leftMargin;
        int contentWidth = 200 - leftMargin - rightMargin;

        // Кнопка «Назад» — закрывает панель и возвращает к панели инструментов
        var backButton = new Button
        {
            Text = "← Назад",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 35,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Arial", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        backButton.Click += (s, e) => HideLinkedEntitiesPanel();
        _linkedEntitiesPanel.Controls.Add(backButton);
        y += 35 + 2;

        var title = new Label
        {
            Text = "Привязанные сущности",
            Font = new Font("Arial", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(220, 220, 220)
        };
        _linkedEntitiesPanel.Controls.Add(title);
        y += 25 + 2;

        // // === ЗАГОЛОВОК СЕКЦИИ ===
        // var doorsLabel = new Label
        // {
        //     Text = "Двери/проходы:",
        //     Location = new Point(leftMargin + 2, y),
        //     Width = contentWidth - 4,
        //     Height = 20,
        //     TextAlign = ContentAlignment.MiddleLeft,
        //     Font = new Font("Arial", 8, FontStyle.Bold),
        //     ForeColor = Color.DarkGray
        // };
        // _linkedEntitiesPanel.Controls.Add(doorsLabel);
        // y += 20 + 2;

        // === ВЫПАДАЮЩИЙ СПИСОК ДВЕРЕЙ (перенесён из панели инструментов) ===
        _doorToolCombo = BuildDoorWindowCombo(drawDoorIcons: true);
        _doorToolCombo.Location = new Point(leftMargin + 2, y);
        foreach (var label in _doorToolMap.Keys)
            _doorToolCombo.Items.Add(label);
        if (_doorToolCombo.Items.Count > 0)
            _doorToolCombo.SelectedIndex = 0;

        _doorToolCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_doorToolCombo.SelectedItem is string key && _doorToolMap.TryGetValue(key, out var tool))
                _toolManager.SetTool(tool);
        };
        _linkedEntitiesPanel.Controls.Add(_doorToolCombo);
        y += 40 + 2;

        // // === ЗАГОЛОВОК СЕКЦИИ ОКОН ===
        // var windowsLabel = new Label
        // {
        //     Text = "Окна:",
        //     Location = new Point(leftMargin + 2, y),
        //     Width = contentWidth - 4,
        //     Height = 20,
        //     TextAlign = ContentAlignment.MiddleLeft,
        //     Font = new Font("Arial", 8, FontStyle.Bold),
        //     ForeColor = Color.DarkGray
        // };
        // _linkedEntitiesPanel.Controls.Add(windowsLabel);
        // y += 20 + 2;

        // === ВЫПАДАЮЩИЙ СПИСОК ОКОН (наполняется в UpdateWindowCombo) ===
        _windowToolCombo = BuildDoorWindowCombo(drawDoorIcons: false);
        _windowToolCombo.Location = new Point(leftMargin + 2, y);
        _windowToolCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_windowToolCombo.SelectedItem is string key &&
                _windowProtoIdCache.TryGetValue(key, out var foundId))
            {
                System.Diagnostics.Debug.WriteLine($"[WindowSelect] key='{key}' id='{foundId}'");
                _selectedWindowProto = foundId;
                _toolManager.SetTool(ToolManager.Tool.ReplaceWallWithWindow);
            }
        };
        _linkedEntitiesPanel.Controls.Add(_windowToolCombo);
        y += 40 + 2;

        // === ЗАГОЛОВОК СЕКЦИИ ПРИВЯЗОК ===
        var linksLabel = new Label
        {
            Text = "Привязки:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _linkedEntitiesPanel.Controls.Add(linksLabel);
        y += 20 + 2;

        // === СИГНАЛИЗАЦИЯ (перенесена из панели инструментов в «Привязки») ===
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

        _linkedEntitiesPanel.Controls.Add(alarmPanel);
        y += 34 + 2;

        // === КНОПКА УСТАНОВКИ ГОЛОПАДА ===
        _btnPlaceHolopad = new Button
        {
            Text = "  Голопад",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _holopadIcon
        };
        _btnPlaceHolopad.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceHolopad);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceHolopad);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ ШКАФЧИКА ===
        _btnPlaceLocker = new Button
        {
            Text = "  Шкафчик",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _lockerIcon
        };
        _btnPlaceLocker.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceLocker);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceLocker);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ ХРАНИЛИЩА СКАФАНДРА ===
        _btnPlaceSuitStorage = new Button
        {
            Text = "  Скафандр",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _suitStorageIcon
        };
        _btnPlaceSuitStorage.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceSuitStorage);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceSuitStorage);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ КРОВАТИ ===
        _btnPlaceBed = new Button
        {
            Text = "  Кровать",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _bedIcon
        };
        _btnPlaceBed.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceBed);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceBed);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ КАМЕРЫ ===
        _btnPlaceCamera = new Button
        {
            Text = "  Камера",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _cameraIcon
        };
        _btnPlaceCamera.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceCamera);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceCamera);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ КОМОДА (только у комнат глав отделов) ===
        _btnPlaceDresser = new Button
        {
            Text = "  Комод",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _dresserIcon
        };
        _btnPlaceDresser.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceDresser);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceDresser);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ ИНТЕРКОМА (вешается в тайл-стену рядом с комнатой) ===
        _btnPlaceIntercom = new Button
        {
            Text = "  Интерком",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _intercomIcon
        };
        _btnPlaceIntercom.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceIntercom);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceIntercom);
        y += 40 + 2;

        // === КНОПКА УСТАНОВКИ ФАКСА (адрес берётся из свойств комнаты) ===
        _btnPlaceFax = new Button
        {
            Text = "  Факс",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            Image = _faxIcon
        };
        _btnPlaceFax.Click += (s, e) => _toolManager.SetTool(ToolManager.Tool.PlaceFax);
        _linkedEntitiesPanel.Controls.Add(_btnPlaceFax);
        y += 40 + 2;
    }

    private ComboBox BuildDoorWindowCombo(bool drawDoorIcons)
    {
        var combo = new ComboBox
        {
            Width = 200 - 2 - 2 - 4,
            Height = 40,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 34,
            DropDownHeight = 160,
            Font = new Font("Arial", 10)
        };
        combo.DrawItem += (s, e) =>
        {
            AppTheme.DrawItemBackground(e.Graphics, e.Bounds,
                (e.State & DrawItemState.Selected) == DrawItemState.Selected);
            if (e.Index >= 0)
            {
                string label = combo.Items[e.Index].ToString() ?? "";
                const int iconSize = 28;
                const int padding = 4;
                int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                int textX = e.Bounds.Left + padding;

                Image? icon = null;
                string fallback = "";
                if (drawDoorIcons)
                {
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
                }
                else if (label.StartsWith("🪟 "))
                {
                    fallback = "🪟";
                    _windowIconCache.TryGetValue(label, out icon);
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

                TextRenderer.DrawText(e.Graphics, label, combo.Font,
                    new Rectangle(textX, e.Bounds.Top, e.Bounds.Width - (textX - e.Bounds.Left), e.Bounds.Height),
                    combo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        };
        return combo;
    }

    private void ShowLinkedEntitiesPanel()
    {
        // Панель привязанных сущностей открывается ПОВЕРХ панели инструментов
        // (на том же месте справа), а не слева от неё.
        bool alreadyOpen = _linkedEntitiesPanel.Visible;
        HideLinkedEntitiesPanel();

        if (!alreadyOpen)
        {
            if (!Controls.Contains(_linkedEntitiesPanel))
                Controls.Add(_linkedEntitiesPanel);

            // Позиционируем ровно поверх панели инструментов: правая часть формы,
            // та же ширина (200px). _toolPanel.Docked слева, поэтому его левая граница
            // = ширина формы - ширина панели.
            int width = _toolPanel.Width > 0 ? _toolPanel.Width : 200;
            int height = _toolPanel.Height;
            int x = ClientSize.Width - width;
            int y = _toolPanel.Top;

            _linkedEntitiesPanel.Bounds = new Rectangle(x, y, width, height);
            _linkedEntitiesPanel.BringToFront();
            _linkedEntitiesPanel.Visible = true;
        }
    }

    private void HideLinkedEntitiesPanel()
    {
        if (_linkedEntitiesPanel != null)
            _linkedEntitiesPanel.Visible = false;
    }
}