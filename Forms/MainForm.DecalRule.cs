// Forms/MainForm.DecalRule.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    private Form? _decalRuleForm = null;

    private void ShowDecalRuleDialog(Room room)
    {
        if (_decalRuleForm != null && !_decalRuleForm.IsDisposed)
        {
            _decalRuleForm.Close();
            _decalRuleForm = null;
        }

        if (_scannedDecalPacks.Count == 0)
            _scannedDecalPacks = DecalPackScanner.ScanFromIndexer(_indexer);

        _decalRuleForm = new Form
        {
            Text = $"Decal Rule — комната {room.RoomType}",
            Size = new Size(560, 620),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _decalRuleForm.Owner = this;

        // Применяет любое изменённое (закоммиченное) свойство слоя/режима: пересобирает
        // декали комнаты, обновляет тайлгрид, кладёт снапшот в undo и перерисовывает.
        // Не вызывается на каждый символ ввода — только на Enter/Leave/выбор/клик.
        void ApplyLiveChanges()
        {
            RecalculateDecalPatterns();
            UpdateTileGrid();
            SaveState();
            Render();
        }

        // ===== ВЕРХНЯЯ ПАНЕЛЬ (режим) =====
        var modePanel = new Panel { Location = new Point(0, 0), Size = new Size(552, 34) };
        modePanel.Controls.Add(new Label { Text = "Режим:", Location = new Point(8, 9), AutoSize = true });
        var modeCombo = new ComboBox { Location = new Point(68, 5), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        modeCombo.Items.AddRange(new object[] { "Авто (по периметру)", "Ручной (области — в разработке)" });

        // Подписка ДО установки SelectedIndex — иначе самая первая установка индекса
        // (-1 -> 0/1) не вызывает обработчик, т.к. подписки ещё не существует, и узор
        // не появляется, пока пользователь не переключит режим вручную туда-обратно
        modeCombo.SelectedIndexChanged += (s, e) =>
        {
            room.DecalMode = modeCombo.SelectedIndex == 1 ? DecalPatternMode.Manual : DecalPatternMode.Auto;
            ApplyLiveChanges();
        };
        modeCombo.SelectedIndex = room.DecalMode == DecalPatternMode.Manual ? 1 : 0; // по умолчанию — Авто; сразу применит режим и пересчитает узор
        modePanel.Controls.Add(modeCombo);
        _decalRuleForm.Controls.Add(modePanel);

        // ===== ОСНОВНАЯ ОБЛАСТЬ: левая панель (слои, 150px) + правая (редактор, всё остальное) =====
        int mainTop = 34;
        int mainHeight = 620 - mainTop - 50; // оставляем место под нижнюю панель с кнопками
        int listPanelWidth = 150;

        var leftPanel = new Panel
        {
            Location = new Point(0, mainTop),
            Size = new Size(listPanelWidth, mainHeight),
            BorderStyle = BorderStyle.FixedSingle
        };

        var layerButtonsPanel = new Panel { Location = new Point(0, 0), Size = new Size(listPanelWidth, 30) };
        var btnAddLayer = new Button { Text = "➕", Width = 23, Height = 26, Location = new Point(0, 2) };
        var btnRemoveLayer = new Button { Text = "🗑", Width = 23, Height = 26, Location = new Point(24, 2) };
        var btnUpLayer = new Button { Text = "↑", Width = 23, Height = 26, Location = new Point(48, 2) };
        var btnDownLayer = new Button { Text = "↓", Width = 23, Height = 26, Location = new Point(72, 2) };
        layerButtonsPanel.Controls.AddRange(new Control[] { btnAddLayer, btnRemoveLayer, btnUpLayer, btnDownLayer });
        leftPanel.Controls.Add(layerButtonsPanel);

        var layerList = new ListBox
        {
            Location = new Point(0, 30),
            Size = new Size(listPanelWidth, mainHeight - 30)
        };

        // Подавляет реентерабельный SelectedIndexChanged во время программного
        // перестроения списка (Clear+Add) — раньше точечное Items[index]=... само
        // стреляло SelectedIndexChanged, ShowLayerEditor пересобирал редактор прямо
        // во время Leave/KeyDown у ещё активного текстового поля, из-за чего список
        // терял синхронизацию со слоями и появлялась лишняя пустая строка
        bool suppressSelectionEvent = false;

        void RefreshLayerList(int selectIndex = -1)
        {
            suppressSelectionEvent = true;
            layerList.BeginUpdate();
            layerList.Items.Clear();
            foreach (var l in room.AutoDecalRule.Layers) layerList.Items.Add(l.Name);
            layerList.EndUpdate();
            if (selectIndex >= 0 && selectIndex < layerList.Items.Count) layerList.SelectedIndex = selectIndex;
            suppressSelectionEvent = false;
        }
        RefreshLayerList();
        leftPanel.Controls.Add(layerList);

        _decalRuleForm.Controls.Add(leftPanel);

        var editorPanel = new Panel
        {
            Location = new Point(listPanelWidth + 5, mainTop),
            Size = new Size(552 - listPanelWidth - 5, mainHeight),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
        _decalRuleForm.Controls.Add(editorPanel);

        void ShowLayerEditor(int index)
        {
            editorPanel.Controls.Clear();
            if (index < 0 || index >= room.AutoDecalRule.Layers.Count) return;
            var layer = room.AutoDecalRule.Layers[index];

            int y = 5;
            editorPanel.Controls.Add(new Label { Text = "Название:", Location = new Point(5, y + 3), AutoSize = true });
            var txtName = new TextBox { Text = layer.Name, Location = new Point(100, y), Width = 180 };

            // Обновление имени только по Enter или по потере фокуса — не на каждый символ
            void CommitLayerName()
            {
                if (layer.Name == txtName.Text) return; // ничего не менялось — не дёргаем пересборку списка
                layer.Name = txtName.Text;
                RefreshLayerList(index);
                ApplyLiveChanges();
            }
            txtName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    CommitLayerName();
                }
            };
            txtName.Leave += (s, e) => CommitLayerName();

            editorPanel.Controls.Add(txtName);
            y += 28;

            editorPanel.Controls.Add(new Label { Text = "Пак (преднаполнение):", Location = new Point(5, y + 3), AutoSize = true });
            var packCombo = new ComboBox { Location = new Point(160, y), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };

            void RefreshPackCombo()
            {
                packCombo.Items.Clear();
                packCombo.Items.Add("(не выбран)");
                foreach (var pack in DecalPack.Examples.Values) packCombo.Items.Add(pack);
                foreach (var pack in _scannedDecalPacks) packCombo.Items.Add(pack);
                packCombo.SelectedIndex = 0;
            }
            RefreshPackCombo();
            editorPanel.Controls.Add(packCombo);

            var btnScanRepo = new Button
            {
                Text = "🔄",
                Location = new Point(325, y),
                Width = 30,
                Height = packCombo.Height,
                FlatStyle = FlatStyle.Flat
            };
            var scanTip = new ToolTip();
            scanTip.SetToolTip(btnScanRepo, "Собрать паки из прототипов текущего репозитория (по суффиксам NE/NW/SE/SW/N/S/E/W)");
            btnScanRepo.Click += (s, e) =>
            {
                // forceRescan: true — пользователь явно нажал кнопку, значит хочет
                // актуальные данные, даже если для этого репозитория уже есть кэш
                _scannedDecalPacks = DecalPackScanner.ScanFromIndexer(_indexer, forceRescan: true);
                RefreshPackCombo();
                MessageBox.Show($"Собрано паков: {_scannedDecalPacks.Count}", "Сборка паков");
            };
            editorPanel.Controls.Add(btnScanRepo);
            y += 30;

            var colorColor = ParseHexColor(layer.Color);
            var btnColor = new Button
            {
                Text = layer.Color, BackColor = colorColor, ForeColor = GetContrastTextColor(colorColor),
                Location = new Point(5, y), Width = 200, Height = 26, FlatStyle = FlatStyle.Flat
            };
            btnColor.Click += (s, e) =>
            {
                using var dlg = new ColorDialog { Color = btnColor.BackColor, FullOpen = true };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    layer.Color = ToHexColor(dlg.Color);
                    btnColor.BackColor = dlg.Color;
                    btnColor.ForeColor = GetContrastTextColor(dlg.Color);
                    btnColor.Text = layer.Color;
                    ApplyLiveChanges();
                }
            };
            editorPanel.Controls.Add(btnColor);
            y += 34;

            var chkEnabled = new CheckBox { Text = "Слой включён", Checked = layer.Enabled, Location = new Point(5, y), AutoSize = true };
            chkEnabled.CheckedChanged += (s, e) =>
            {
                layer.Enabled = chkEnabled.Checked;
                ApplyLiveChanges();
            };
            editorPanel.Controls.Add(chkEnabled);
            y += 30;

            editorPanel.Controls.Add(new Label
            {
                Text = "Позиции (id прототипа декали, пусто = не рисовать):",
                Location = new Point(5, y), AutoSize = true, Font = new Font("Arial", 8, FontStyle.Bold)
            });
            y += 22;

            var positionTextBoxes = new Dictionary<DecalPosition, TextBox>();
            foreach (DecalPosition pos in Enum.GetValues<DecalPosition>())
            {
                editorPanel.Controls.Add(new Label { Text = pos.ToString(), Location = new Point(5, y + 3), Width = 120 });
                var txt = new TextBox
                {
                    Text = layer.Positions.TryGetValue(pos, out var v) ? v : "",
                    Location = new Point(130, y), Width = 220
                };
                var capturedPos = pos;

                void CommitPosition()
                {
                    if (string.IsNullOrWhiteSpace(txt.Text)) layer.Positions.Remove(capturedPos);
                    else layer.Positions[capturedPos] = txt.Text.Trim();
                    ApplyLiveChanges();
                }
                txt.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        CommitPosition();
                    }
                };
                txt.Leave += (s, e) => CommitPosition();

                positionTextBoxes[pos] = txt;
                editorPanel.Controls.Add(txt);
                y += 26;
            }

            packCombo.SelectedIndexChanged += (s, e) =>
            {
                if (packCombo.SelectedItem is DecalPack pack)
                {
                    layer.SourcePackId = pack.Id;
                    foreach (var kvp in pack.Positions)
                    {
                        layer.Positions[kvp.Key] = kvp.Value;
                        if (positionTextBoxes.TryGetValue(kvp.Key, out var tb)) tb.Text = kvp.Value;
                    }
                    ApplyLiveChanges();
                }
            };
        }

        layerList.SelectedIndexChanged += (s, e) =>
        {
            if (suppressSelectionEvent) return;
            ShowLayerEditor(layerList.SelectedIndex);
        };

        btnAddLayer.Click += (s, e) =>
        {
            room.AutoDecalRule.Layers.Add(new DecalLayer { Name = $"Слой {room.AutoDecalRule.Layers.Count + 1}" });
            RefreshLayerList(room.AutoDecalRule.Layers.Count - 1);
            ShowLayerEditor(layerList.SelectedIndex);
            ApplyLiveChanges();
        };
        btnRemoveLayer.Click += (s, e) =>
        {
            int idx = layerList.SelectedIndex;
            if (idx < 0) return;
            room.AutoDecalRule.Layers.RemoveAt(idx);
            RefreshLayerList(Math.Min(idx, room.AutoDecalRule.Layers.Count - 1));
            ShowLayerEditor(layerList.SelectedIndex);
            ApplyLiveChanges();
        };
        btnUpLayer.Click += (s, e) =>
        {
            int idx = layerList.SelectedIndex;
            if (idx <= 0) return;
            (room.AutoDecalRule.Layers[idx - 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx - 1]);
            RefreshLayerList(idx - 1);
            ApplyLiveChanges();
        };
        btnDownLayer.Click += (s, e) =>
        {
            int idx = layerList.SelectedIndex;
            if (idx < 0 || idx >= room.AutoDecalRule.Layers.Count - 1) return;
            (room.AutoDecalRule.Layers[idx + 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx + 1]);
            RefreshLayerList(idx + 1);
            ApplyLiveChanges();
        };

        if (room.AutoDecalRule.Layers.Count > 0) layerList.SelectedIndex = 0;

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOk = new Button { Text = "Готово", Width = 100, Height = 30, Location = new Point(360, 8) };
        btnOk.Click += (s, e) => _decalRuleForm?.Close(); // изменения уже применены live, кнопка просто закрывает
        var btnClose = new Button { Text = "Закрыть", Width = 100, Height = 30, Location = new Point(465, 8) };
        btnClose.Click += (s, e) => _decalRuleForm?.Close();
        btnPanel.Controls.Add(btnOk);
        btnPanel.Controls.Add(btnClose);
        _decalRuleForm.Controls.Add(btnPanel);

        _decalRuleForm.FormClosed += (s, e) => { _decalRuleForm = null; };
        _decalRuleForm.Show(this);
    }
}