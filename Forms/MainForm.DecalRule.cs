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

        int formWidth = 460;
        int formHeight = 500;

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

        var modePanel = new Panel { Location = new Point(0, 0), Size = new Size(formWidth - 8, 34) };
        modePanel.Controls.Add(new Label { Text = "Режим:", Location = new Point(8, 9), AutoSize = true });
        var modeCombo = new ComboBox { Location = new Point(68, 5), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        modeCombo.Items.AddRange(new object[] { "Авто (по периметру)", "Ручной (области — в разработке)" });
        modeCombo.SelectedIndexChanged += (s, e) =>
        {
            room.DecalMode = modeCombo.SelectedIndex == 1 ? DecalPatternMode.Manual : DecalPatternMode.Auto;
            ApplyLiveChanges();
        };
        modeCombo.SelectedIndex = room.DecalMode == DecalPatternMode.Manual ? 1 : 0;
        modePanel.Controls.Add(modeCombo);
        _decalRuleForm.Controls.Add(modePanel);

        // Панель "➕ Добавить слой" — СРАЗУ под modePanel (y=34), а не поверх неё (y=0).
        // Раньше стояла на (0,0) и полностью перекрывала выбор режима непрозрачным фоном.
        var topButtons = new Panel { Location = new Point(0, 34), Size = new Size(formWidth - 8, 30) };
        var btnAddLayer = new Button { Text = "➕ Добавить слой", Location = new Point(5, 2), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat };
        topButtons.Controls.Add(btnAddLayer);

        var btnInherit = new Button { Text = "⬇ Унаследовать", Location = new Point(160, 2), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat };
        var inheritTip = new ToolTip();
        inheritTip.SetToolTip(btnInherit, "Добавить слои из унаследованного правила (по типу комнаты и её предкам в дереве Наследования декалей)");
        topButtons.Controls.Add(btnInherit);

        _decalRuleForm.Controls.Add(topButtons);

        // ===== ОДИН СПИСОК СЛОЁВ НА ВСЮ ФОРМУ — каждая строка: имя, чекбокс вкл/выкл, выбор пака =====
        var listPanel = new Panel
        {
            Location = new Point(0, 34 + 30),
            Size = new Size(formWidth - 8, formHeight - 34 - 30 - 50),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
        _decalRuleForm.Controls.Add(listPanel);

        void RebuildList()
        {
            listPanel.Controls.Clear();
            int rowY = 5;

            for (int i = 0; i < room.AutoDecalRule.Layers.Count; i++)
            {
                var layer = room.AutoDecalRule.Layers[i];
                int idx = i;

                var row = new Panel { Location = new Point(5, rowY), Size = new Size(listPanel.Width - 30, 30) };

                var chkEnabled = new CheckBox { Checked = layer.Enabled, Location = new Point(0, 5), Width = 20 };
                chkEnabled.CheckedChanged += (s, e) => { layer.Enabled = chkEnabled.Checked; ApplyLiveChanges(); };
                row.Controls.Add(chkEnabled);

                var txtName = new TextBox { Text = layer.Name, Location = new Point(22, 3), Width = 100 };
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
                    Location = new Point(128, 2),
                    Width = 140,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                btnPickPack.Click += (s, e) =>
                {
                    var dialog = new DecalPackDialog(_decalPackManager)
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
                    dialog.Show(this); // немодально
                };
                row.Controls.Add(btnPickPack);

                var btnUp = new Button { Text = "↑", Location = new Point(272, 2), Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
                btnUp.Click += (s, e) =>
                {
                    if (idx <= 0) return;
                    (room.AutoDecalRule.Layers[idx - 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx - 1]);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnUp);

                var btnDown = new Button { Text = "↓", Location = new Point(298, 2), Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDown.Click += (s, e) =>
                {
                    if (idx >= room.AutoDecalRule.Layers.Count - 1) return;
                    (room.AutoDecalRule.Layers[idx + 1], room.AutoDecalRule.Layers[idx]) = (room.AutoDecalRule.Layers[idx], room.AutoDecalRule.Layers[idx + 1]);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnDown);

                var btnDel = new Button { Text = "🗑", Location = new Point(324, 2), Width = 24, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDel.Click += (s, e) =>
                {
                    room.AutoDecalRule.Layers.RemoveAt(idx);
                    RebuildList();
                    ApplyLiveChanges();
                };
                row.Controls.Add(btnDel);

                listPanel.Controls.Add(row);
                rowY += 34;
            }
        }

btnAddLayer.Click += (s, e) =>
        {
            room.AutoDecalRule.Layers.Add(new DecalLayer { Name = $"Слой {room.AutoDecalRule.Layers.Count + 1}" });
            RebuildList();
            ApplyLiveChanges();
        };

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

            // Добавляем поверх уже существующих слоёв комнаты, а не заменяем их —
            // так можно унаследовать и на пустой, и на уже частично настроенной вручную
            // комнате, не теряя то, что там уже стояло
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

        _decalRuleForm.FormClosed += (s, e) => { _decalRuleForm = null; };
        _decalRuleForm.Show(this);
    }
}