// Forms/MainForm.Dialogs.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

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
                if (ArgbColorPickerDialog.Pick(this, settings.Color, out var picked))
                {
                    settings.Color = picked;
                    btnColor.BackColor = picked;
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
            Text = "Прототипы",
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
            Text = "Декали",
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






    // Парсит цвет вида "#RRGGBBAA" (формат SS14/DecalGrid) в System.Drawing.Color.
    // При ошибке парсинга возвращает непрозрачный белый — безопасный дефолт для декали
    private static Color ParseHexColor(string hex)
    {
        try
        {
            var h = hex.TrimStart('#');
            if (h.Length == 8)
            {
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                int a = Convert.ToInt32(h.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            if (h.Length == 6)
            {
                // Цвета палитр ("- type: palette") хранятся без альфы — считаем непрозрачным
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Color.White;
    }


    // Обратное преобразование — в формат "#RRGGBBAA", как ожидает DecalGrid при экспорте
    private static string ToHexColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }



    // Палитра хранит "#RRGGBB" (без альфы), а декали экспортируются как "#RRGGBBAA"
    private static string ToDecalColorFormat(string paletteHex)
    {
        var h = paletteHex.TrimStart('#');
        if (h.Length == 6) return $"#{h.ToUpperInvariant()}FF";
        if (h.Length == 8) return $"#{h.ToUpperInvariant()}";
        return "#FFFFFFFF";
    }


    private static Color GetContrastTextColor(Color background)
    {
        int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return brightness < 128 ? Color.White : Color.Black;
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
            Text = "Настройки прототипа",
            Size = new Size(340, 450),
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
            RowCount = 8,
            ColumnCount = 2,
            AutoSize = false,
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
            Increment = 0.01m,
            DecimalPlaces = 2,
            Width = 80
        };
        nudX.ValueChanged += (s, e) =>
        {
            _centerOffset = new PointF((float)nudX.Value, _centerOffset.Y);
            UpdateCenterSettingsButton();
        };
        panel.Controls.Add(nudX, 1, row);
        row++;

        panel.Controls.Add(new Label { Text = "Смещение Y:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudY = new NumericUpDown
        {
            Value = (decimal)_centerOffset.Y,
            Minimum = -2m,
            Maximum = 2m,
            Increment = 0.01m,
            DecimalPlaces = 2,
            Width = 80
        };
        nudY.ValueChanged += (s, e) =>
        {
            _centerOffset = new PointF(_centerOffset.X, (float)nudY.Value);
            UpdateCenterSettingsButton();
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

        panel.Controls.Add(new Label
        {
            Text = "Цвет декали:",
            AutoSize = true,
            Font = new Font("Arial", 9, FontStyle.Bold),
            ForeColor = Color.DarkBlue
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        // ===== Выбор палитры (из "- type: palette" репозитория) =====
        panel.Controls.Add(new Label { Text = "Палитра:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var palettes = _indexer.GetPalettes();
        var paletteCombo = new ComboBox
        {
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8),
            Enabled = palettes.Count > 0
        };
        if (palettes.Count > 0)
        {
            foreach (var p in palettes) paletteCombo.Items.Add(p);
            paletteCombo.DisplayMember = "Name";
            paletteCombo.SelectedIndex = 0;
        }
        else
        {
            paletteCombo.Items.Add("(нет палитр — обновите репозиторий)");
            paletteCombo.SelectedIndex = 0;
        }
        panel.Controls.Add(paletteCombo, 1, row);
        row++;

        // Плашки цветов выбранной палитры — перестраиваются при смене палитры в комбобоксе
        int swatchColumns = 8;
        int swatchSize = 26;
        int swatchSpacing = 4;
        int swatchPanelWidth = swatchColumns * (swatchSize + swatchSpacing) + SystemInformation.VerticalScrollBarWidth;

        // Anchor вместо Dock: Top|Left|Bottom тянет ВЫСОТУ под доступное место в строке
        // (строка ниже получит RowStyle.Percent и будет сжиматься/расти вместе с окном),
        // но ШИРИНА остаётся фиксированной (Right не заанкорен) — поэтому 8 плашек в ряд
        // и отсутствие горизонтального скролла сохраняются при любом размере окна.
        // Height=180 здесь лишь стартовое значение, реальная высота задаётся анкором.
        var swatchPanel = new FlowLayoutPanel
        {
            Width = swatchPanelWidth,
            Height = 180,
            MinimumSize = new Size(swatchPanelWidth, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        int swatchRowIndex = row;
        panel.Controls.Add(swatchPanel, 0, row);
        panel.SetColumnSpan(swatchPanel, 2);
        row++;

        var currentDecalColor = ParseHexColor(_decalColor);
        var btnDecalColor = new Button
        {
            Text = _decalColor,
            BackColor = currentDecalColor,
            ForeColor = GetContrastTextColor(currentDecalColor),
            Width = 260,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnDecalColor.Click += (s, e) =>
        {
            if (ArgbColorPickerDialog.Pick(this, btnDecalColor.BackColor, out var picked))
            {
                btnDecalColor.BackColor = picked;
                btnDecalColor.ForeColor = GetContrastTextColor(picked);
                btnDecalColor.Text = ToHexColor(picked);
                _decalColor = btnDecalColor.Text;
            }
        };
        panel.Controls.Add(btnDecalColor, 0, row);
        panel.SetColumnSpan(btnDecalColor, 2);
        row++;

        // Стираемость декали — как в игре: тряпкой/шваброй можно стереть только декали
        // с флагом cleanable, остальные постоянные
        var chkCleanable = new CheckBox
        {
            Text = "Стираемая (cleanable)",
            Checked = _decalCleanable,
            AutoSize = true,
            Font = new Font("Segoe UI", 9)
        };
        chkCleanable.CheckedChanged += (s, e) => { _decalCleanable = chkCleanable.Checked; };
        panel.Controls.Add(chkCleanable, 0, row);
        panel.SetColumnSpan(chkCleanable, 2);
        row++;


        // Все строки, кроме блока с плашками, оставляем в естественном размере (AutoSize),
        // а строке swatchPanel отдаём 100% оставшегося места (RowStyle.Percent). Так при
        // уменьшении окна сжимается только этот блок — остальные элементы (включая кнопку
        // выбора своего цвета ниже) сохраняют свою высоту и остаются видимыми
        panel.RowCount = row;
        panel.RowStyles.Clear();
        for (int i = 0; i < row; i++)
        {
            panel.RowStyles.Add(i == swatchRowIndex
                ? new RowStyle(SizeType.Percent, 100f)
                : new RowStyle(SizeType.AutoSize));
        }

        void RebuildSwatches()
        {
            swatchPanel.Controls.Clear();
            if (paletteCombo.SelectedItem is not Palette selectedPalette) return;

            foreach (var kvp in selectedPalette.Colors)
            {
                var swatchColor = ParseHexColor(kvp.Value);
                var swatch = new Button
                {
                    Width = 26,
                    Height = 26,
                    BackColor = swatchColor,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(120, 120, 120) },
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand,
                    Tag = kvp.Value
                };
                var tooltip = new ToolTip();
                tooltip.SetToolTip(swatch, $"{kvp.Key} ({kvp.Value})");

                swatch.Click += (s, e) =>
                {
                    string decalHex = ToDecalColorFormat((string)swatch.Tag);
                    var color = ParseHexColor(decalHex);
                    btnDecalColor.BackColor = color;
                    btnDecalColor.ForeColor = GetContrastTextColor(color);
                    btnDecalColor.Text = decalHex;
                    _decalColor = decalHex;
                };

                swatchPanel.Controls.Add(swatch);
            }
        }

        paletteCombo.SelectedIndexChanged += (s, e) => RebuildSwatches();
        if (palettes.Count > 0) RebuildSwatches();

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
            _centerSettingsForm?.Close();
        };
        btnPanel.Controls.Add(btnOk);

        // Раз все поля применяются мгновенно, кнопка "Отмена" отдельного смысла
        // отката уже не несёт — оставлена только как второй способ закрыть окно
        var btnCancel = new Button
        {
            Text = "Закрыть",
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




    private void ShowMoveSettingsDialog()
    {
        if (_moveSettingsForm != null && !_moveSettingsForm.IsDisposed)
        {
            _moveSettingsForm.Close();
            _moveSettingsForm = null;
            return;
        }

        _moveSettingsForm = new Form
        {
            Text = "Настройки перемещения",
            Size = new Size(360, 400),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _moveSettingsForm.Owner = this;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 10,
            ColumnCount = 2,
            AutoSize = true
        };

        int row = 0;

        panel.Controls.Add(new Label { Text = "Шаг перемещения:", AutoSize = true, Font = new Font("Arial", 9) }, 0, row);
        var nudStep = new NumericUpDown
        {
            Value = (decimal)_moveSettings.Step,
            Minimum = 0.1m,
            Maximum = 10m,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Width = 80
        };
        panel.Controls.Add(nudStep, 1, row);
        row++;

        panel.Controls.Add(new Label
        {
            Text = "Фильтр выделяемых объектов:",
            Font = new Font("Arial", 10, FontStyle.Bold),
            AutoSize = true
        }, 0, row);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 2);
        row++;

        var chkRooms = new CheckBox { Text = "Комнаты", Checked = _moveSettings.IncludeRooms, AutoSize = true };
        chkRooms.CheckedChanged += (s, e) => _moveSettings.IncludeRooms = chkRooms.Checked;
        panel.Controls.Add(chkRooms, 0, row);
        panel.SetColumnSpan(chkRooms, 2);
        row++;

        var chkTiles = new CheckBox { Text = "Отдельные тайлы", Checked = _moveSettings.IncludeTiles, AutoSize = true };
        chkTiles.CheckedChanged += (s, e) => _moveSettings.IncludeTiles = chkTiles.Checked;
        panel.Controls.Add(chkTiles, 0, row);
        panel.SetColumnSpan(chkTiles, 2);
        row++;

        var chkPipes = new CheckBox { Text = "Трубы", Checked = _moveSettings.IncludePipes, AutoSize = true };
        chkPipes.CheckedChanged += (s, e) => _moveSettings.IncludePipes = chkPipes.Checked;
        panel.Controls.Add(chkPipes, 0, row);
        panel.SetColumnSpan(chkPipes, 2);
        row++;

        var chkAlarms = new CheckBox { Text = "Сигнализации", Checked = _moveSettings.IncludeAlarms, AutoSize = true };
        chkAlarms.CheckedChanged += (s, e) => _moveSettings.IncludeAlarms = chkAlarms.Checked;
        panel.Controls.Add(chkAlarms, 0, row);
        panel.SetColumnSpan(chkAlarms, 2);
        row++;

        var chkFirelocks = new CheckBox { Text = "Пожарные шлюзы", Checked = _moveSettings.IncludeFirelocks, AutoSize = true };
        chkFirelocks.CheckedChanged += (s, e) => _moveSettings.IncludeFirelocks = chkFirelocks.Checked;
        panel.Controls.Add(chkFirelocks, 0, row);
        panel.SetColumnSpan(chkFirelocks, 2);
        row++;

        var chkEntities = new CheckBox { Text = "Сущности", Checked = _moveSettings.IncludeEntities, AutoSize = true };
        chkEntities.CheckedChanged += (s, e) => _moveSettings.IncludeEntities = chkEntities.Checked;
        panel.Controls.Add(chkEntities, 0, row);
        panel.SetColumnSpan(chkEntities, 2);
        row++;

        var chkOther = new CheckBox { Text = "Другое", Checked = _moveSettings.IncludeOther, AutoSize = true };
        chkOther.CheckedChanged += (s, e) => _moveSettings.IncludeOther = chkOther.Checked;
        panel.Controls.Add(chkOther, 0, row);
        panel.SetColumnSpan(chkOther, 2);
        row++;

        var chkDecals = new CheckBox { Text = "Декали", Checked = _moveSettings.IncludeDecals, AutoSize = true };
        chkDecals.CheckedChanged += (s, e) => _moveSettings.IncludeDecals = chkDecals.Checked;
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
        btnOk.Click += (s, e) =>
        {
            _moveSettings.Step = (float)nudStep.Value;
            _moveSettingsForm?.Close();
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
        btnCancel.Click += (s, e) => _moveSettingsForm?.Close();
        btnPanel.Controls.Add(btnCancel);

        _moveSettingsForm.Controls.Add(panel);
        _moveSettingsForm.Controls.Add(btnPanel);

        _moveSettingsForm.FormClosed += (s, e) => { _moveSettingsForm = null; };
        _moveSettingsForm.Show(this);
    }
}
