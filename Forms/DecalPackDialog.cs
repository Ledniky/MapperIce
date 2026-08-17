// Forms/DecalPackDialog.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class DecalPackDialog : Form
{
    private readonly DecalPackManager _manager;
    private readonly PrototypeIndexer? _indexer;
    private readonly TreeView _treeView;
    private readonly Panel _editorPanel;
    private DecalPack? _selectedPack;
    private string? _selectedCategory; // выбрана категория (папка), а не конкретный пак

    public event Action<DecalPack>? OnPackSelected;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public Action? RescanCallback { get; set; }

    // indexer необязателен (nullable) — диалог создаётся в нескольких местах, где-то
    // индексер уже под рукой, а без него просто не будет доступна кнопка "из палитры"
    public DecalPackDialog(DecalPackManager manager, PrototypeIndexer? indexer = null)
    {
        _manager = manager;
        _indexer = indexer;

        Text = "Паки декалей";
        Size = new Size(560, 520);
        MinimumSize = new Size(480, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;

        var headerPanel = new Panel
        {
            Location = new Point(0, 0), Size = new Size(560, 40),
            BackColor = Color.FromArgb(240, 240, 240), BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        headerPanel.Controls.Add(new Label
        {
            Text = "Паки декалей", Location = new Point(10, 8), Size = new Size(300, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50)
        });
        Controls.Add(headerPanel);

        var mainPanel = new Panel
        {
            Location = new Point(0, 40), Size = new Size(560, 380),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        // ===== ЛЕВАЯ ПАНЕЛЬ =====
        var leftPanel = new Panel
        {
            Location = new Point(0, 0), Size = new Size(250, 380), BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
        };

        var buttonPanel = new Panel { Location = new Point(0, 0), Size = new Size(250, 36), BackColor = Color.FromArgb(248, 248, 248) };

        // Только иконка, без текста — обновляет папку Extracted
        var btnRescan = new Button
        {
            Text = "🔄", Location = new Point(5, 5), Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 235, 250), Cursor = Cursors.Hand
        };
        var rescanTip = new ToolTip();
        rescanTip.SetToolTip(btnRescan, "Обновить паки в папке Extracted из репозитория");
        buttonPanel.Controls.Add(btnRescan);

        var btnAddCustom = new Button
        {
            Text = "➕", Location = new Point(40, 5), Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 250, 225), Cursor = Cursors.Hand
        };
        var addTip = new ToolTip();
        addTip.SetToolTip(btnAddCustom, "Создать новый пак в папке Custom");
        buttonPanel.Controls.Add(btnAddCustom);

        var btnRemove = new Button
        {
            Text = "🗑", Location = new Point(75, 5), Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(250, 240, 240), Cursor = Cursors.Hand, Enabled = false
        };
        buttonPanel.Controls.Add(btnRemove);

        var btnImport = new Button
        {
            Text = "📥", Location = new Point(110, 5), Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245, 245, 220), Cursor = Cursors.Hand
        };
        var importTip = new ToolTip();
        importTip.SetToolTip(btnImport, "Импортировать пак(и) из файла");
        buttonPanel.Controls.Add(btnImport);

        var btnExport = new Button
        {
            Text = "📤", Location = new Point(145, 5), Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 245, 220), Cursor = Cursors.Hand, Enabled = false
        };
        var exportTip = new ToolTip();
        exportTip.SetToolTip(btnExport, "Экспортировать выбранный пак или папку целиком");
        buttonPanel.Controls.Add(btnExport);

        leftPanel.Controls.Add(buttonPanel);

        _treeView = new TreeView
        {
            Location = new Point(0, 36), Size = new Size(250, 344),
            Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom, LabelEdit = true
        };
        leftPanel.Controls.Add(_treeView);
        mainPanel.Controls.Add(leftPanel);

        // ===== ПРАВАЯ ПАНЕЛЬ =====
        _editorPanel = new Panel
        {
            Location = new Point(255, 0), Size = new Size(300, 380),
            BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Padding = new Padding(8), AutoScroll = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        mainPanel.Controls.Add(_editorPanel);
        Controls.Add(mainPanel);

        // ===== НИЖНЯЯ ПАНЕЛЬ =====
        var bottomPanel = new Panel
        {
            Location = new Point(0, 420), Size = new Size(560, 50),
            BackColor = Color.FromArgb(240, 240, 240), BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnSelect = new Button
        {
            Text = "Выбрать", Location = new Point(360, 10), Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 230, 240), Cursor = Cursors.Hand
        };
        btnSelect.Click += (s, e) =>
        {
            if (_selectedPack != null) { OnPackSelected?.Invoke(_selectedPack); Close(); }
        };
        bottomPanel.Controls.Add(btnSelect);

        var btnClose = new Button
        {
            Text = "Закрыть", Location = new Point(455, 10), Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 240, 240), Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => Close();
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);

        _treeView.AfterSelect += (s, e) =>
        {
            if (e.Node?.Tag is DecalPack pack)
            {
                _selectedPack = pack;
                _selectedCategory = null;
                btnRemove.Enabled = true;
                btnExport.Enabled = true;
                ShowPackEditor(pack);
            }
            else
            {
                _selectedPack = null;
                _selectedCategory = e.Node?.Text;
                btnRemove.Enabled = false;
                btnExport.Enabled = _selectedCategory != null;
                _editorPanel.Controls.Clear();
            }
        };

        _treeView.NodeMouseDoubleClick += (s, e) =>
        {
            if (e.Node?.Tag is DecalPack pack) { OnPackSelected?.Invoke(pack); Close(); }
        };

        // Переименование категории (папки) прямо в дереве — F2/двойной клик по названию папки
        _treeView.BeforeLabelEdit += (s, e) =>
        {
            if (e.Node?.Tag is DecalPack) e.CancelEdit = true; // паки не переименовываются тут, только папки
        };
        _treeView.AfterLabelEdit += (s, e) =>
        {
            if (e.Node?.Tag != null) return; // это узел пака, не папка
            if (string.IsNullOrWhiteSpace(e.Label)) { e.CancelEdit = true; return; }

            string oldName = e.Node!.Text;
            string newName = e.Label!.Trim();
            _manager.RenameCategory(oldName, newName);
            e.CancelEdit = true; // дерево перестроится само через OnPacksChanged -> RefreshTree
        };

        btnRescan.Click += (s, e) => RescanCallback?.Invoke();

        btnAddCustom.Click += (s, e) =>
        {
            var newPack = new DecalPack
            {
                Name = $"Новый пак {_manager.Packs.Count(p => p.Category == "Custom") + 1}",
                Category = "Custom",
                Source = DecalPackSource.Custom
            };
            _manager.AddOrUpdate(newPack);
            RefreshTree();
            SelectNodeByPack(newPack);
        };

        btnRemove.Click += (s, e) =>
        {
            if (_selectedPack == null) return;
            if (MessageBox.Show($"Удалить пак '{_selectedPack.Name}'?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _manager.Remove(_selectedPack.Id);
                _selectedPack = null;
                RefreshTree();
            }
        };

        btnImport.Click += (s, e) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Импорт паков декалей",
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            int totalImported = 0, totalSkipped = 0;
            var errors = new List<string>();
            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var (imported, skipped) = _manager.ImportFromFile(filePath);
                    totalImported += imported;
                    totalSkipped += skipped;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            var summary = $"Импортировано: {totalImported}";
            if (totalSkipped > 0) summary += $"\nПропущено (уже есть): {totalSkipped}";
            if (errors.Count > 0) summary += $"\n\nОшибки:\n{string.Join("\n", errors)}";
            MessageBox.Show(summary, "Импорт завершён");
        };

        btnExport.Click += (s, e) =>
        {
            if (_selectedPack != null)
            {
                using var saveDialog = new SaveFileDialog
                {
                    Title = $"Экспорт пака: {_selectedPack.Name}",
                    Filter = "JSON файлы (*.json)|*.json",
                    FileName = $"{_selectedPack.Name}.json"
                };
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    _manager.ExportPack(_selectedPack.Id, saveDialog.FileName);
                    MessageBox.Show("Пак экспортирован!");
                }
            }
            else if (_selectedCategory != null)
            {
                using var saveDialog = new SaveFileDialog
                {
                    Title = $"Экспорт папки: {_selectedCategory}",
                    Filter = "JSON файлы (*.json)|*.json",
                    FileName = $"{_selectedCategory}.json"
                };
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    _manager.ExportCategory(_selectedCategory, saveDialog.FileName);
                    MessageBox.Show("Папка экспортирована!");
                }
            }
        };

        _manager.OnPacksChanged += RefreshTree;
        FormClosed += (s, e) => { _manager.OnPacksChanged -= RefreshTree; };

        RefreshTree();
    }

    private void RefreshTree()
    {
        string? previouslySelectedId = _selectedPack?.Id;

        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();

        foreach (var category in _manager.GetCategories())
        {
            var categoryNode = new TreeNode($"📁 {category}");
            foreach (var pack in _manager.Packs.Where(p => p.Category == category).OrderBy(p => p.Name))
                categoryNode.Nodes.Add(new TreeNode(pack.Name) { Tag = pack });

            _treeView.Nodes.Add(categoryNode);
            categoryNode.Expand();
        }

        _treeView.EndUpdate();

        if (previouslySelectedId != null)
        {
            var updated = _manager.GetById(previouslySelectedId);
            if (updated != null) SelectNodeByPack(updated);
        }
    }

    private void SelectNodeByPack(DecalPack pack)
    {
        foreach (TreeNode categoryNode in _treeView.Nodes)
            foreach (TreeNode node in categoryNode.Nodes)
                if (node.Tag is DecalPack p && p.Id == pack.Id)
                {
                    _treeView.SelectedNode = node;
                    node.EnsureVisible();
                    return;
                }
    }

    /// <summary>Полностью редактируемо — включая Extracted (раз сканер больше не единственный источник правды, цвет и позиции можно поправить вручную поверх результата сканирования).</summary>
    private void ShowPackEditor(DecalPack pack)
    {
        _editorPanel.Controls.Clear();
        int y = 5;

        _editorPanel.Controls.Add(new Label { Text = "Название:", Location = new Point(5, y + 3), AutoSize = true });
        var txtName = new TextBox { Text = pack.Name, Location = new Point(90, y), Width = 190 };
        void CommitName()
        {
            if (txtName.Text == pack.Name || string.IsNullOrWhiteSpace(txtName.Text)) return;
            pack.Name = txtName.Text.Trim();
            _manager.AddOrUpdate(pack);
        }
        txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitName(); } };
        txtName.Leave += (s, e) => CommitName();
        _editorPanel.Controls.Add(txtName);
        y += 30;

// Цвет теперь настраивается тут, привязан к паку (а не к слою Decal Rule)
        _editorPanel.Controls.Add(new Label { Text = "Цвет:", Location = new Point(5, y + 3), AutoSize = true });
        var packColor = ParseHexColor(pack.Color);
        var btnColor = new Button
        {
            Text = pack.Color, BackColor = packColor, ForeColor = GetContrastTextColor(packColor),
            Location = new Point(90, y), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat
        };
        btnColor.Click += (s, e) =>
        {
            using var dlg = new ColorDialog { Color = btnColor.BackColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                pack.Color = ToHexColor(dlg.Color);
                btnColor.BackColor = dlg.Color;
                btnColor.ForeColor = GetContrastTextColor(dlg.Color);
                btnColor.Text = pack.Color;
                _manager.AddOrUpdate(pack);
            }
        };
        _editorPanel.Controls.Add(btnColor);

        var btnPickFromPalette = new Button
        {
            Text = "🎨",
            Location = new Point(245, y),
            Width = 35,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Enabled = _indexer != null
        };
        var paletteTip = new ToolTip();
        paletteTip.SetToolTip(btnPickFromPalette, _indexer != null
            ? "Выбрать цвет из палитры репозитория"
            : "Недоступно — индексер прототипов не передан в это окно");
        btnPickFromPalette.Click += (s, e) =>
        {
            if (_indexer == null) return;
            ShowPaletteColorPicker(color =>
            {
                pack.Color = ToHexColor(color);
                btnColor.BackColor = color;
                btnColor.ForeColor = GetContrastTextColor(color);
                btnColor.Text = pack.Color;
                _manager.AddOrUpdate(pack);
            });
        };
        _editorPanel.Controls.Add(btnPickFromPalette);
        y += 34;

        _editorPanel.Controls.Add(new Label
        {
            Text = $"Позиции ({pack.Positions.Count} / {Enum.GetValues<DecalPosition>().Length}):",
            Location = new Point(5, y), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        y += 22;

        foreach (DecalPosition pos in Enum.GetValues<DecalPosition>())
        {
            _editorPanel.Controls.Add(new Label { Text = pos.ToString(), Location = new Point(5, y + 3), Width = 110, Font = new Font("Segoe UI", 8) });

            var txt = new TextBox
            {
                Text = pack.Positions.TryGetValue(pos, out var v) ? v : "",
                Location = new Point(120, y),
                Width = 165
            };
            var capturedPos = pos;
            void CommitPosition()
            {
                if (string.IsNullOrWhiteSpace(txt.Text)) pack.Positions.Remove(capturedPos);
                else pack.Positions[capturedPos] = txt.Text.Trim();
                _manager.AddOrUpdate(pack);
            }
            txt.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitPosition(); } };
            txt.Leave += (s, e) => CommitPosition();
            _editorPanel.Controls.Add(txt);

            y += 24;
        }
    }

    // Локальные копии — не хотим тянуть зависимость на MainForm ради этих двух утилит
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
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Color.White;
    }

    private static string ToHexColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private static Color GetContrastTextColor(Color background)
    {
        int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return brightness < 128 ? Color.White : Color.Black;
    }

    /// <summary>
    /// Отдельное окошко: выбор палитры (из "- type: palette" репозитория) + сетка свотчей.
    /// Клик по свотчу вызывает onColorPicked и закрывает окно. Тот же принцип, что и
    /// выбор цвета декали в MainForm.ShowCenterSettingsDialog, но самостоятельный —
    /// этот диалог не зависит от MainForm.
    /// </summary>
    private void ShowPaletteColorPicker(Action<Color> onColorPicked)
    {
        if (_indexer == null) return;

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
                    // Палитра хранит "#RRGGBB" (без альфы) — декали ожидают "#RRGGBBAA"
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

    // Цвета палитр хранятся без альфы ("#RRGGBB") — декали экспортируются как "#RRGGBBAA"
    private static string ToDecalColorFormat(string paletteHex)
    {
        var h = paletteHex.TrimStart('#');
        if (h.Length == 6) return $"#{h.ToUpperInvariant()}FF";
        if (h.Length == 8) return $"#{h.ToUpperInvariant()}";
        return "#FFFFFFFF";
    }
}