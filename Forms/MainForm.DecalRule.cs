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
            Text = $"Decal Rule — комната {room.RoomType}",
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

                    var row = new Panel { Location = new Point(5, rowY), Size = new Size(330, 30) };

                    var lblInfo = new Label
                    {
                        Text = $"X:{area.X} Y:{area.Y} Ш:{area.Width} В:{area.Height}",
                        Location = new Point(0, 7),
                        Width = 170,
                        Font = new Font("Segoe UI", 8)
                    };
                    row.Controls.Add(lblInfo);

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
                            ApplyLiveChanges();
                            lblInfo.Text = $"X:{area.X} Y:{area.Y} Ш:{area.Width} В:{area.Height}";
                        });
                    };
                    row.Controls.Add(btnEditOnMap);

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
                    rowY += 34;
                }
            }

            var btnAddArea = new Button { Text = "➕ Добавить область", Location = new Point(5, 335), Width = 160, Height = 26, FlatStyle = FlatStyle.Flat };
            btnAddArea.Click += (s, e) =>
            {
                // Стартовый прямоугольник — размером с комнату; сразу можно уточнить
                // границы, перетаскивая углы кнопкой "✏ На карте"
                layer.ManualAreas.Add(new ManualDecalArea
                {
                    X = room.X,
                    Y = room.Y,
                    Width = Math.Max(1, room.Width),
                    Height = Math.Max(1, room.Height)
                });
                RebuildAreas();
                ApplyLiveChanges();
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
                    var clone = _decalPackManager.CloneForOwnUse(source, $"{source.Name} ({room.RoomType} room)");
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
                    $"Для типа «{room.RoomType}» (и его предков) не задано ни одного правила в окне «Наследование декалей».",
                    "Унаследовать");
                return;
            }

            foreach (var layer in inheritedRule.Layers)
                room.AutoDecalRule.Layers.Add(layer.Clone());

            RebuildList();
            ApplyLiveChanges();
        };

        RebuildList();

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 45 };
        var btnClose = new Button { Text = "Закрыть", Width = 100, Height = 30, Location = new Point(formWidth - 8 - 100, 8) };
        btnClose.Click += (s, e) => _decalRuleForm?.Close();
        btnPanel.Controls.Add(btnClose);
        _decalRuleForm.Controls.Add(btnPanel);

        _decalRuleForm.FormClosed += (s, e) => { _decalRuleForm = null; EndEditDecalArea(); };
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
        _typeLabel.Text = "Область декалей: тащите жёлтые угловые квадраты (шаг 1 тайл). Внутри — сдвиг всей области. ESC — закрыть.";
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
            _decalAreaDragSnapshot = new ManualDecalArea { X = area.X, Y = area.Y, Width = area.Width, Height = area.Height };
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

            area.X = Math.Min(nx, fixedX);
            area.Y = Math.Min(ny, fixedY);
            area.Width = Math.Max(1, Math.Abs(nx - fixedX));
            area.Height = Math.Max(1, Math.Abs(ny - fixedY));

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

            area.X = _decalAreaDragSnapshot.X + dx;
            area.Y = _decalAreaDragSnapshot.Y + dy;

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
    }
}