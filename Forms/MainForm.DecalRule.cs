// Forms/MainForm.DecalRule.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    private Form? _decalRuleForm = null;
    private const float DecalCornerHitPx = 10f;

    private void ShowDecalRuleDialog(Room room)
    {
        if (_decalRuleForm != null && !_decalRuleForm.IsDisposed)
        {
            _decalRuleForm.Close();
            _decalRuleForm = null;
        }

        int formWidth = 560;
        int formHeight = 520;

        _decalRuleForm = new Form
        {
            Text = $"Decal Rule — комната {_roomTypeManager.GetRoomType(room.RoomType).DisplayName}",
            Size = new Size(formWidth, formHeight),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };
        _decalRuleForm.Owner = this;

        void ApplyLiveChanges()
        {
            RecalculateDecalPatterns();
            UpdateTileGrid();
            SaveState();
            Render();
        }

        var topButtons = new Panel { Location = new Point(0, 0), Size = new Size(formWidth - 8, 30) };
        var btnAddLayer = new Button { Text = "➕ Добавить слой", Location = new Point(5, 2), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat };
        topButtons.Controls.Add(btnAddLayer);

        var btnInherit = new Button { Text = "⬇ Унаследовать", Location = new Point(160, 2), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat };
        var inheritTip = new ToolTip();
        inheritTip.SetToolTip(btnInherit, "Добавить слои из унаследованного правила (по типу комнаты и её предкам в дереве Наследования декалей)");
        topButtons.Controls.Add(btnInherit);

        _decalRuleForm.Controls.Add(topButtons);

        var listPanel = new Panel
        {
            Location = new Point(0, 30),
            Size = new Size(formWidth - 8, formHeight - 30 - 50),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
        _decalRuleForm.Controls.Add(listPanel);

        // ===== Список ручных областей слоя — без цифр, редактирование через канвас =====
        Form ShowManualAreasDialog(DecalLayer layer)
        {
            var areasForm = new Form
            {
                Text = $"Ручные области — {layer.Name}",
                Size = new Size(360, 420),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false,
                MaximizeBox = false,
                MinimizeBox = false,
                Owner = _decalRuleForm
            };

            var areaListPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(360, 330),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            areasForm.Controls.Add(areaListPanel);

            void RebuildAreas()
            {
                areaListPanel.Controls.Clear();
                int rowY = 5;

                for (int i = 0; i < layer.ManualAreas.Count; i++)
                {
                    var area = layer.ManualAreas[i];
                    int idx = i;

                    var row = new Panel { Location = new Point(5, rowY), Size = new Size(330, 60) };

                    var lblInfo = new Label
                    {
                        Text = $"X:{area.X} Y:{area.Y} Ш:{area.Width} В:{area.Height}",
                        Location = new Point(0, 7),
                        Width = 170,
                        Font = new Font("Segoe UI", 8)
                    };
                    row.Controls.Add(lblInfo);

                    // Режим рисовки выбранной области
                    var lblMode = new Label
                    {
                        Text = "Режим:",
                        Location = new Point(0, 35),
                        Width = 46,
                        Font = new Font("Segoe UI", 8)
                    };
                    row.Controls.Add(lblMode);

                    var cmbMode = new ComboBox
                    {
                        Location = new Point(46, 31),
                        Width = 100,
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Font = new Font("Segoe UI", 8)
                    };
                    cmbMode.Items.AddRange(new object[] { "Область-стена", "Вокруг стен" });
                    cmbMode.SelectedIndex = area.Mode == ManualAreaMode.WallsOnly ? 1 : 0;
                    cmbMode.SelectedIndexChanged += (s, e) =>
                    {
                        area.Mode = cmbMode.SelectedIndex == 1 ? ManualAreaMode.WallsOnly : ManualAreaMode.WallRegion;
                        ApplyLiveChanges();
                    };
                    var modeTip = new ToolTip();
                    modeTip.SetToolTip(cmbMode,
                        "Область-стена — вся область считается сплошной стеной, обрамление рисуется по форме прямоугольника.\n" +
                        "Вокруг стен — обычный узор вдоль настоящих стен комнаты, но только внутри области.");
                    row.Controls.Add(cmbMode);

                    var btnEditOnMap = new Button
                    {
                        Text = "✏ На карте",
                        Location = new Point(175, 2),
                        Width = 90,
                        Height = 24,
                        FlatStyle = FlatStyle.Flat
                    };
                    btnEditOnMap.Click += (s, e) =>
                    {
                        BeginEditDecalArea(room, area, () =>
                        {
                            // Живой пересчёт декалей по мере перетаскивания — без SaveState
                            // на каждом кадре (иначе undo засоряется и рендер тормозит).
                            RecalculateDecalPatterns();
                            Render();
                            lblInfo.Text = $"X:{area.X} Y:{area.Y} Ш:{area.Width} В:{area.Height}";
                        });
                    };
                    row.Controls.Add(btnEditOnMap);



                    var btnSubtract = new Button
                    {
                        Text = "➖ Вычесть",
                        Location = new Point(175, 30),
                        Width = 90,
                        Height = 24,
                        FlatStyle = FlatStyle.Flat
                    };
                    btnSubtract.Click += (s, e) =>
                    {
                        btnSubtract.Text = "выделите…";
                        // Переиспользуем протяжку рамки от "Добавить область":
                        // результат падает в _decalAreaPending, дальше кладём его в вычеты.
                        _editingDecalAreaCommitAction = () =>
                        {
                            var pending = _decalAreaPending;
                            if (pending == null) return;
                            area.Subtractions.Add(new DecalAreaRect
                            {
                                X = pending.X,
                                Y = pending.Y,
                                Width = pending.Width,
                                Height = pending.Height
                            });
                            RebuildAreas();
                            ApplyLiveChanges();
                            // Возвращаемся в режим редактирования этой области,
                            // чтобы вырезы были видны на карте
                            BeginEditDecalArea(room, area, () =>
                            {
                                RecalculateDecalPatterns();
                                Render();
                            });
                        };
                        _editingDecalAreaCancelAction = () => btnSubtract.Text = "➖ Вычесть";
                        BeginDrawDecalArea();
                        // BeginDrawDecalArea гасит рамку области — возвращаем её, чтобы
                        // было видно, откуда вычитаем (только отрисовка, режим редактирования не включается)
                        _renderer.SetDecalAreaEditRect(area.X, area.Y, area.Width, area.Height);
                        _renderer.SetDecalAreaEditSubtractions(area.Subtractions);
                        Render();
                    };
                    row.Controls.Add(btnSubtract);

                    var btnUndoSubtract = new Button
                    {
                        Text = "↶",
                        Location = new Point(270, 30),
                        Width = 26,
                        Height = 24,
                        FlatStyle = FlatStyle.Flat,
                        Enabled = area.Subtractions.Count > 0
                    };
                    new ToolTip().SetToolTip(btnUndoSubtract, "Убрать последний вычет");
                    btnUndoSubtract.Click += (s, e) =>
                    {
                        if (area.Subtractions.Count == 0) return;
                        area.Subtractions.RemoveAt(area.Subtractions.Count - 1);
                        RebuildAreas();
                        ApplyLiveChanges();
                    };
                    row.Controls.Add(btnUndoSubtract);

                    
                    var btnDelArea = new Button { Text = "🗑", Location = new Point(270, 2), Width = 26, Height = 24, FlatStyle = FlatStyle.Flat };
                    btnDelArea.Click += (s, e) =>
                    {
                        if (_editingDecalArea == area) EndEditDecalArea();
                        layer.ManualAreas.RemoveAt(idx);
                        RebuildAreas();
                        ApplyLiveChanges();
                    };
                    row.Controls.Add(btnDelArea);

                    areaListPanel.Controls.Add(row);
                    rowY += 64;
                }
            }

            var btnAddArea = new Button { Text = "➕ Добавить область", Location = new Point(5, 335), Width = 160, Height = 26, FlatStyle = FlatStyle.Flat };
            var addAreaTip = new ToolTip();
            addAreaTip.SetToolTip(btnAddArea, "Включить режим — затем растяните область мышью на карте, как рамку выделения.");

            // Меняет подпись кнопки в зависимости от активного режима рисования области
            void SetDecalAddButtonActive(bool active)
            {
                btnAddArea.Text = active ? "выделите область…" : "➕ Добавить область";
                btnAddArea.BackColor = active ? Color.FromArgb(200, 235, 255) : Color.Empty;
            }

            btnAddArea.Click += (s, e) =>
            {
                // Включаем режим: подпись кнопки меняется на "выделите область", сама
                // область рисуется вручную протяжкой мыши. Данные кладём в _decalAreaPending.
                SetDecalAddButtonActive(true);
                _editingDecalAreaCommitAction = () =>
                {
                    var pending = _decalAreaPending;
                    if (pending == null) return;
                    // Ручные области рисуются только в режиме Manual — включаем его,
                    // иначе RecalculateAll пойдёт по ветке Auto и области не дадут декалей.
                    layer.Mode = DecalPatternMode.Manual;
                    layer.ManualAreas.Add(pending);
                    RebuildAreas();
                    ApplyLiveChanges();
                    SetDecalAddButtonActive(false);
                    // Сразу переходим в режим редактирования новой области, чтобы её
                    // можно было двигать/тянуть углы с живым обновлением декалей.
                    BeginEditDecalArea(room, pending, () =>
                    {
                        RecalculateDecalPatterns();
                        Render();
                    });
                };
                _editingDecalAreaCancelAction = () => SetDecalAddButtonActive(false);
                _decalAreaPendingLayer = layer;
                BeginDrawDecalArea();
            };
            areasForm.Controls.Add(btnAddArea);

            var btnCloseAreas = new Button { Text = "Закрыть", Location = new Point(255, 335), Width = 90, Height = 26 };
            btnCloseAreas.Click += (s, e) => areasForm.Close();
            areasForm.Controls.Add(btnCloseAreas);

            areasForm.FormClosed += (s, e) => { EndEditDecalArea(); };

            RebuildAreas();
            areasForm.Show(_decalRuleForm); // немодально — канвас остаётся доступным для перетаскивания углов
            return areasForm;
        }

        void RebuildList()
        {
            listPanel.Controls.Clear();
            int rowY = 5;

            for (int i = 0; i < room.AutoDecalRule.Layers.Count; i++)
            {
                var layer = room.AutoDecalRule.Layers[i];
                int idx = i;

                var row = new Panel { Location = new Point(5, rowY), Size = new Size(listPanel.Width - 30, 62), BorderStyle = BorderStyle.FixedSingle };

                var chkEnabled = new CheckBox { Checked = layer.Enabled, Location = new Point(2, 5), Width = 20 };
                chkEnabled.CheckedChanged += (s, e) => { layer.Enabled = chkEnabled.Checked; ApplyLiveChanges(); };
                row.Controls.Add(chkEnabled);

                var txtName = new TextBox { Text = layer.Name, Location = new Point(24, 3), Width = 85 };
                void CommitName()
                {
                    if (layer.Name == txtName.Text || string.IsNullOrWhiteSpace(txtName.Text)) return;
                    layer.Name = txtName.Text;
                    ApplyLiveChanges();
                }
                txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitName(); } };
                txtName.Leave += (s, e) => CommitName();
                row.Controls.Add(txtName);

                var currentPack = !string.IsNullOrEmpty(layer.SourcePackId) ? _decalPackManager.GetById(layer.SourcePackId) : null;
                var btnPickPack = new Button
                {
                    Text = currentPack?.Name ?? "(не выбран)",
                    Location = new Point(112, 2),
                    Width = 110,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                btnPickPack.Click += (s, e) =>
                {
                    var dialog = new DecalPackDialog(_decalPackManager, _indexer)
                    {
                        RescanCallback = () =>
                            {
                                var scanned = DecalPackScanner.ScanFromIndexer(_indexer, forceRescan: true);
                                var (added, updated) = _decalPackManager.MergeScanned(scanned);
                                MessageBox.Show($"Добавлено новых: {added}, обновлено: {updated}", "Обновление паков");
                            }
                    };
                    dialog.OnPackSelected += (pack) =>
                    {
                        layer.SourcePackId = pack.Id;
                        btnPickPack.Text = pack.Name;
                        ApplyLiveChanges();
                    };
                    dialog.Show(this);
                };
                row.Controls.Add(btnPickPack);

                var btnClonePack = new Button
                {
                    Text = "📋",
                    Location = new Point(224, 2),
                    Width = 22,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    Enabled = !string.IsNullOrEmpty(layer.SourcePackId)
                };
                var cloneTip = new ToolTip();
                cloneTip.SetToolTip(btnClonePack, "Сохранить как личную копию только для этой комнаты");
                btnClonePack.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(layer.SourcePackId)) return;
                    var source = _decalPackManager.GetById(layer.SourcePackId);
                    if (source == null)
                    {
                        MessageBox.Show("Пак не найден.");
                        return;
                    }
                    var clone = _decalPackManager.CloneForOwnUse(source, $"{source.Name} ({_roomTypeManager.GetRoomType(room.RoomType).DisplayName} room)");
                    layer.SourcePackId = clone.Id;
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnClonePack);

                Color EffectiveColor()
                {
                    var pack = !string.IsNullOrEmpty(layer.SourcePackId) ? _decalPackManager.GetById(layer.SourcePackId) : null;
                    return ParseHexColor(layer.Color ?? pack?.Color ?? "#FFFFFFFF");
                }

                var btnColor = new Button
                {
                    Location = new Point(250, 2),
                    Width = 32,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = EffectiveColor(),
                    Text = layer.Color == null ? "· авто" : "",
                    Font = new Font("Segoe UI", 6),
                    ForeColor = GetContrastTextColor(EffectiveColor())
                };
                var colorTip = new ToolTip();
                colorTip.SetToolTip(btnColor, "Цвет декали только для ЭТОЙ комнаты (RGBA-выбор)");
                btnColor.Click += (s, e) =>
                {
                    if (ArgbColorPickerDialog.Pick(this, EffectiveColor(), out var picked))
                    {
                        layer.Color = ToHexColor(picked);
                        ApplyLiveChanges();
                        RebuildList();
                    }
                };
                row.Controls.Add(btnColor);

                // Выбор цвета из палитры репозитория ("- type: palette") прямо тут
                var palettes = _indexer.GetPalettes();
                var btnPalette = new Button
                {
                    Text = "🎨",
                    Location = new Point(284, 2),
                    Width = 22,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    Enabled = palettes.Count > 0
                };
                var paletteTip = new ToolTip();
                paletteTip.SetToolTip(btnPalette, palettes.Count > 0
                    ? "Выбрать цвет из палитры репозитория (только для этой комнаты)"
                    : "Недоступно — в репозитории не найдено палитр");
                btnPalette.Click += (s, e) =>
                {
                    ShowLayerPaletteColorPicker(color =>
                    {
                        layer.Color = ToHexColor(color);
                        ApplyLiveChanges();
                        RebuildList();
                    });
                };
                row.Controls.Add(btnPalette);

                var btnResetColor = new Button
                {
                    Text = "⟲",
                    Location = new Point(308, 2),
                    Width = 20,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    Enabled = layer.Color != null
                };
                var resetTip = new ToolTip();
                resetTip.SetToolTip(btnResetColor, "Сбросить — использовать цвет типа комнаты / пака");
                btnResetColor.Click += (s, e) =>
                {
                    layer.Color = null;
                    ApplyLiveChanges();
                    RebuildList();
                };
                row.Controls.Add(btnResetColor);

                var btnUp = new Button { Text = "↑", Location = new Point(332, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnUp.Click += (s, e) =>
                {
                    if (idx <= 0) return;
                    (room.AutoDecalRule.Layers[idx - 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx - 1]);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnUp);

                var btnDown = new Button { Text = "↓", Location = new Point(358, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDown.Click += (s, e) =>
                {
                    if (idx >= room.AutoDecalRule.Layers.Count - 1) return;
                    (room.AutoDecalRule.Layers[idx + 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx + 1]);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnDown);

                var btnDel = new Button { Text = "🗑", Location = new Point(384, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDel.Click += (s, e) =>
                {
                    room.AutoDecalRule.Layers.RemoveAt(idx);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnDel);

                // ===== Вторая строка: режим слоя (Авто/Ручной) + кнопка "Области" =====
                var lblMode = new Label { Text = "Режим:", Location = new Point(24, 35), Width = 45, TextAlign = ContentAlignment.MiddleLeft };
                row.Controls.Add(lblMode);

                var modeCombo = new ComboBox { Location = new Point(70, 30), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
                modeCombo.Items.AddRange(new object[] { "Авто (по периметру)", "Ручной (области)" });
                modeCombo.SelectedIndex = layer.Mode == DecalPatternMode.Manual ? 1 : 0;

                var btnAreas = new Button
                {
                    Text = $"📐 Области ({layer.ManualAreas.Count})",
                    Location = new Point(206, 30),
                    Width = 130,
                    Height = 25,
                    FlatStyle = FlatStyle.Flat,
                    Visible = layer.Mode == DecalPatternMode.Manual
                };
                btnAreas.Click += (s, e) =>
                {
                    var areasForm = ShowManualAreasDialog(layer);
                    areasForm.FormClosed += (s2, e2) => { btnAreas.Text = $"📐 Области ({layer.ManualAreas.Count})"; };
                };
                row.Controls.Add(btnAreas);

                modeCombo.SelectedIndexChanged += (s, e) =>
                {
                    layer.Mode = modeCombo.SelectedIndex == 1 ? DecalPatternMode.Manual : DecalPatternMode.Auto;
                    btnAreas.Visible = layer.Mode == DecalPatternMode.Manual;
                    ApplyLiveChanges();
                };
                row.Controls.Add(modeCombo);

                var chkDoorAsWall = new CheckBox
                {
                    Text = "декаль на дверь",
                    Location = new Point(340, 33),
                    Width = 140,
                    Checked = !layer.TreatDoorAsWall
                };
                var doorAsWallTip = new ToolTip();
                doorAsWallTip.SetToolTip(chkDoorAsWall,
                    "Включено — дверь получает свою декаль по стороне (DoorN/S/E/W), а стена строится в обход неё.\n" +
                    "Выключено — дверь считается обычной стеной, узор идёт по контуру без разрыва (без отдельной декали двери).");
                chkDoorAsWall.CheckedChanged += (s, e) =>
                {
                    layer.TreatDoorAsWall = !chkDoorAsWall.Checked;
                    ApplyLiveChanges();
                };
                row.Controls.Add(chkDoorAsWall);

                listPanel.Controls.Add(row);
                rowY += 66;
            }
        }

        btnAddLayer.Click += (s, e) =>
        {
            int maxNum = 0;
            foreach (var l in room.AutoDecalRule.Layers)
            {
                int num = ExtractLayerNumber(l.Name);
                if (num > maxNum) maxNum = num;
            }
            room.AutoDecalRule.Layers.Add(new DecalLayer { Name = $"Слой {maxNum + 1}" });
            RebuildList();
            ApplyLiveChanges();
        };

        int ExtractLayerNumber(string name)
        {
            int start = name.LastIndexOf(' ');
            if (start < 0) return 0;
            int.TryParse(name.Substring(start + 1), out int num);
            return num;
        }

        btnInherit.Click += (s, e) =>
        {
            var roomTypeInstance = _roomTypeManager.GetRoomType(room.RoomType);
            var inheritedRule = _decalInheritanceManager.ResolveEffectiveRule(roomTypeInstance.GetType());

            if (inheritedRule == null || inheritedRule.Layers.Count == 0)
            {
                MessageBox.Show(
                    $"Для типа «{_roomTypeManager.GetRoomType(room.RoomType).DisplayName}» (и его предков) не задано ни одного правила в окне «Наследование декалей».",
                    "Унаследовать");
                return;
            }

            // Заменяем текущий набор слоёв унаследованным (а не добавляем поверх)
            room.AutoDecalRule.Layers.Clear();
            foreach (var layer in inheritedRule.Layers)
                room.AutoDecalRule.Layers.Add(layer.Clone());

            // Сносим ВСЕ декали, попадающие в реальные клетки комнаты (не только
            // помеченные владельцем узора), — так не остаётся старых декалей от
            // прежнего набора слоёв, конфликтующих с новым узором (в т.ч. legacy
            // с PatternOwnerId == null из старых сохранённых проектов).
            var grid = _map.ActiveGrid;
            if (grid != null)
                grid.Decals.RemoveAll(d =>
                    room.Contains((int)Math.Floor(d.X), (int)Math.Floor(d.Y)));

            RebuildList();
            ApplyLiveChanges();
        };

        RebuildList();

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 45 };
        var btnClose = new Button { Text = "Закрыть", Width = 100, Height = 30, Location = new Point(formWidth - 8 - 100, 8) };
        btnClose.Click += (s, e) => _decalRuleForm?.Close();
        btnPanel.Controls.Add(btnClose);
        _decalRuleForm.Controls.Add(btnPanel);

        _decalRuleForm.FormClosed += (s, e) => { _decalRuleForm = null; EndEditDecalArea(); CancelDrawDecalArea(); };
        _decalRuleForm.Show(this);
    }

    private void ShowLayerPaletteColorPicker(Action<Color> onColorPicked)
    {
        var palettes = _indexer.GetPalettes();
        if (palettes.Count == 0)
        {
            MessageBox.Show("В текущем репозитории не найдено ни одной палитры (\"- type: palette\").", "Палитра");
            return;
        }

        var pickerForm = new Form
        {
            Text = "Выбор цвета из палитры",
            Size = new Size(320, 400),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var paletteCombo = new ComboBox
        {
            Location = new Point(10, 10),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        foreach (var p in palettes) paletteCombo.Items.Add(p);
        paletteCombo.DisplayMember = "Name";
        paletteCombo.SelectedIndex = 0;
        pickerForm.Controls.Add(paletteCombo);

        var swatchPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 45),
            Size = new Size(280, 300),
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        pickerForm.Controls.Add(swatchPanel);

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
                    string paletteHex = (string)swatch.Tag;
                    string decalHex = ToDecalColorFormat(paletteHex);
                    onColorPicked(ParseHexColor(decalHex));
                    pickerForm.Close();
                };

                swatchPanel.Controls.Add(swatch);
            }
        }

        paletteCombo.SelectedIndexChanged += (s, e) => RebuildSwatches();
        RebuildSwatches();

        pickerForm.ShowDialog(this);
    }

    // ==================== Интерактивное редактирование области на канвасе ====================

    private (float x, float y) TileToScreen(int tileX, int tileY)
    {
        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        int activeIndex = _map.Grids.IndexOf(_map.ActiveGrid!);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _map.ActiveGrid!.Position.X * tileSize;
        float gridOffsetY = (_map.ActiveGrid.Position.Y + layerOffsetY) * tileSize;
        return (tileX * tileSize + gridOffsetX - _viewOffset.X, tileY * tileSize + gridOffsetY - _viewOffset.Y);
    }

    // В отличие от GetTilePosition (Floor — какая клетка под курсором), тут нужна
    // ближайшая ГРАНИЦА между клетками (угол прямоугольника) — поэтому Round, а не Floor
    private (int x, int y) GetTileCornerPosition(Point mouseLocation)
    {
        var precise = GetPrecisePosition(mouseLocation);
        return ((int)Math.Round(precise.x), (int)Math.Round(precise.y));
    }

    private void BeginEditDecalArea(Room room, ManualDecalArea area, Action onChanged)
    {
        _editingDecalAreaRoom = room;
        _editingDecalArea = area;
        _editingDecalAreaApplyCallback = onChanged;
        _toolManager.ResetTool();
        _renderer.SetDecalAreaEditRect(area.X, area.Y, area.Width, area.Height);
        _renderer.SetDecalAreaEditSubtractions(area.Subtractions);
        // Статус удалён — тип комнаты теперь в ComboBox
        Render();
    }

    private void EndEditDecalArea()
    {
        if (_editingDecalArea == null) return;
        _editingDecalArea = null;
        _editingDecalAreaRoom = null;
        _editingDecalAreaApplyCallback = null;
        _draggingDecalCornerIndex = -1;
        _isDraggingDecalWholeArea = false;
        _decalAreaDragSnapshot = null;
        _renderer.ClearDecalAreaEditRect();
        UpdateTypeLabel();
        Render();
    }

    // ===== Растягивание НОВОЙ ручной области мышью (как рамка выделения) =====
    private void BeginDrawDecalArea()
    {
        _isDrawingDecalArea = true;
        _decalAreaDrawMouseDown = false;
        _decalAreaPending = null;
        _toolManager.ResetTool();
        _editingDecalArea = null;
        _editingDecalAreaRoom = null;
        _editingDecalAreaApplyCallback = null;
        _renderer.ClearDecalAreaEditRect();
        Render();
    }

    private void CancelDrawDecalArea()
    {
        _isDrawingDecalArea = false;
        _decalAreaDrawMouseDown = false;
        _decalAreaPending = null;
        _decalAreaPendingLayer = null;
        _editingDecalAreaCommitAction = null;
        _editingDecalAreaCancelAction?.Invoke();
        _editingDecalAreaCancelAction = null;
        _renderer.ClearDecalAreaEditRect();
        _renderer.ClearDecalAreaDrawRect();
        UpdateTypeLabel();
        Render();
    }

    private void HandleDecalAreaDrawMouseDown(MouseEventArgs e)
    {
        if (_map.ActiveGrid == null) return;
        if (e.Button != MouseButtons.Left) return;

        var (cx, cy) = GetTileCornerPosition(e.Location);
        _decalAreaDrawMouseDown = true;
        _decalAreaDrawStart = (cx, cy);
        _decalAreaPending = new ManualDecalArea
        {
            X = cx,
            Y = cy,
            Width = 1,
            Height = 1,
            Mode = ManualAreaMode.WallRegion
        };
        _renderer.SetDecalAreaDrawRect(cx, cy, 1, 1);
        Render();
    }

    private void HandleDecalAreaDrawMouseMove(MouseEventArgs e)
    {
        if (_decalAreaPending == null) return;
        var (nx, ny) = GetTileCornerPosition(e.Location);

        int sx = _decalAreaDrawStart.x;
        int sy = _decalAreaDrawStart.y;
        var area = _decalAreaPending;

        int newX = Math.Min(nx, sx);
        int newY = Math.Min(ny, sy);
        int newW = Math.Max(1, Math.Abs(nx - sx));
        int newH = Math.Max(1, Math.Abs(ny - sy));

        // Рамка привязана к углам тайла: между пересечениями границ тайла значения не
        // меняются. Пропускаем пересчёт декалей и рендер в этих "пустых" пикселях —
        // иначе каждый пиксель движения вызывает полное перестроение узора и картинка
        // тормозит (слайдшоу). Пересоздаём декали только когда прямоугольник реально сменился.
        if (newX == area.X && newY == area.Y && newW == area.Width && newH == area.Height)
            return;

        area.X = newX;
        area.Y = newY;
        area.Width = newW;
        area.Height = newH;

        _renderer.SetDecalAreaDrawRect(area.X, area.Y, area.Width, area.Height);
        // Живой пересчёт декалей по мере растягивания рамки, чтобы узор
        // обновлялся при ЛЮБОМ изменении области (а не только после отпускания).
        RecalculateDecalPatterns();
        Render();
    }

    private void HandleDecalAreaDrawMouseUp(MouseEventArgs e)
    {
        // Коммит-колбэк добавления читает _decalAreaPending, поэтому сбрасываем
        // его ПОСЛЕ вызова.
        _isDrawingDecalArea = false;
        _decalAreaDrawMouseDown = false;

        if (_decalAreaPending == null)
        {
            CancelDrawDecalArea();
            return;
        }

        _renderer.ClearDecalAreaDrawRect();

        _editingDecalAreaCommitAction?.Invoke();
        _editingDecalAreaCancelAction?.Invoke();
        _editingDecalAreaCancelAction = null;
        _editingDecalAreaCommitAction = null;
        _decalAreaPending = null;
        _decalAreaPendingLayer = null;
        Render();
    }

    private void HandleDecalAreaEditMouseDown(MouseEventArgs e)
    {
        if (_editingDecalArea == null || _map.ActiveGrid == null) return;
        if (e.Button != MouseButtons.Left) return;

        var area = _editingDecalArea;
        var corners = new (int x, int y)[]
        {
            (area.X, area.Y),                             // 0: верх-лево (TL)
            (area.X + area.Width, area.Y),                 // 1: верх-право (TR)
            (area.X, area.Y + area.Height),                 // 2: низ-лево (BL)
            (area.X + area.Width, area.Y + area.Height)      // 3: низ-право (BR)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            var (sx, sy) = TileToScreen(corners[i].x, corners[i].y);
            if (Math.Abs(e.Location.X - sx) <= DecalCornerHitPx && Math.Abs(e.Location.Y - sy) <= DecalCornerHitPx)
            {
                _draggingDecalCornerIndex = i;
                _isDraggingDecalWholeArea = false;
                return;
            }
        }

        // Клик внутри прямоугольника (не по углу) — сдвиг всей области
        var tilePos = GetTilePosition(e.Location);
        bool insideRect = tilePos.x >= area.X && tilePos.x < area.X + area.Width &&
                           tilePos.y >= area.Y && tilePos.y < area.Y + area.Height;
        if (insideRect)
        {
            _isDraggingDecalWholeArea = true;
            _draggingDecalCornerIndex = -1;
            _decalAreaDragSnapshot = area.Clone();
            _decalAreaDragStartMouseTile = GetTileCornerPosition(e.Location);
        }
    }

    private void HandleDecalAreaEditMouseMove(MouseEventArgs e)
    {
        if (_editingDecalArea == null) return;
        var area = _editingDecalArea;

        if (_draggingDecalCornerIndex >= 0)
        {
            var (nx, ny) = GetTileCornerPosition(e.Location);

            // Противоположный (неподвижный) угол вычисляется из ТЕКУЩЕГО состояния
            // прямоугольника — он самосогласован, т.к. на прошлом кадре мы всегда
            // сохраняли неподвижную сторону нетронутой
            int fixedX = _draggingDecalCornerIndex == 1 || _draggingDecalCornerIndex == 3 ? area.X : area.X + area.Width;
            int fixedY = _draggingDecalCornerIndex == 2 || _draggingDecalCornerIndex == 3 ? area.Y : area.Y + area.Height;

            int newX = Math.Min(nx, fixedX);
            int newY = Math.Min(ny, fixedY);
            int newW = Math.Max(1, Math.Abs(nx - fixedX));
            int newH = Math.Max(1, Math.Abs(ny - fixedY));

            // Тот же гейт, что в растягивании: между пересечениями границ тайла
            // прямоугольник не меняется — не пересчитываем декали на каждый пиксель.
            if (newX == area.X && newY == area.Y && newW == area.Width && newH == area.Height)
                return;

            area.X = newX;
            area.Y = newY;
            area.Width = newW;
            area.Height = newH;

            _renderer.SetDecalAreaEditRect(area.X, area.Y, area.Width, area.Height);
            _editingDecalAreaApplyCallback?.Invoke();
            Render();
            return;
        }

        if (_isDraggingDecalWholeArea && _decalAreaDragSnapshot != null)
        {
            var (curX, curY) = GetTileCornerPosition(e.Location);
            int dx = curX - _decalAreaDragStartMouseTile.x;
            int dy = curY - _decalAreaDragStartMouseTile.y;

            int newX = _decalAreaDragSnapshot.X + dx;
            int newY = _decalAreaDragSnapshot.Y + dy;

            // При переносе всей области также пропускаем кадры, где сдвиг не поменялся.
            if (newX == area.X && newY == area.Y)
                return;

            area.X = newX;
            area.Y = newY;
            for (int i = 0; i < area.Subtractions.Count && i < _decalAreaDragSnapshot.Subtractions.Count; i++)
            {
                area.Subtractions[i].X = _decalAreaDragSnapshot.Subtractions[i].X + dx;
                area.Subtractions[i].Y = _decalAreaDragSnapshot.Subtractions[i].Y + dy;
            }

            _renderer.SetDecalAreaEditRect(area.X, area.Y, area.Width, area.Height);
            _editingDecalAreaApplyCallback?.Invoke();
            Render();
        }
    }

    private void HandleDecalAreaEditMouseUp(MouseEventArgs e)
    {
        _draggingDecalCornerIndex = -1;
        _isDraggingDecalWholeArea = false;
        _decalAreaDragSnapshot = null;

        // Завершение перетаскивания — фиксируем одно состояние в undo/redo
        SaveState();
    }
}