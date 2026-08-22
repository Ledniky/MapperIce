// Forms/DecalInheritanceDialog.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class DecalInheritanceDialog : Form
{
    private readonly DecalInheritanceManager _inheritance;
    private readonly DecalPackManager _packManager;
    private readonly PrototypeIndexer _indexer;
    private readonly TreeView _treeView;
    private readonly Panel _editorPanel;
    private DecalTypeNode? _selectedNode;

    public DecalInheritanceDialog(DecalInheritanceManager inheritance, DecalPackManager packManager, PrototypeIndexer indexer)
    {
        _inheritance = inheritance;
        _packManager = packManager;
        _indexer = indexer;

        Text = "Наследование декалей";
        Size = new Size(780, 560);
        MinimumSize = new Size(660, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;

        var headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(780, 40),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        headerPanel.Controls.Add(new Label
        {
            Text = "Наследование декалей по типам комнат",
            Location = new Point(10, 8),
            Size = new Size(500, 25),
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        });
        Controls.Add(headerPanel);

        var mainPanel = new Panel
        {
            Location = new Point(0, 40),
            Size = new Size(780, 470),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        var leftPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(260, 470),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
        };
        _treeView = new TreeView
        {
            Location = new Point(0, 0),
            Size = new Size(260, 470),
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
        };
        leftPanel.Controls.Add(_treeView);
        mainPanel.Controls.Add(leftPanel);

        _editorPanel = new Panel
        {
            Location = new Point(265, 0),
            Size = new Size(515, 470),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        mainPanel.Controls.Add(_editorPanel);
        Controls.Add(mainPanel);

        var bottomPanel = new Panel
        {
            Location = new Point(0, 510),
            Size = new Size(780, 50),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        var btnClose = new Button { Text = "Закрыть", Location = new Point(675, 10), Size = new Size(85, 30), FlatStyle = FlatStyle.Flat };
        btnClose.Click += (s, e) => Close();
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);

        _treeView.AfterSelect += (s, e) =>
        {
            _selectedNode = e.Node?.Tag as DecalTypeNode;
            ShowEditorFor(_selectedNode);
        };

        _inheritance.OnChanged += OnInheritanceChanged;
        FormClosed += (s, e) => { _inheritance.OnChanged -= OnInheritanceChanged; };

        RebuildTree(isFirstBuild: true);
    }

    private void OnInheritanceChanged()
    {
        var keepType = _selectedNode?.Type;
        RebuildTree(isFirstBuild: false);
        if (keepType != null) SelectNodeByType(keepType);
    }

    private void RebuildTree(bool isFirstBuild)
    {
        // Запоминаем, какие узлы были раскрыты пользователем (по Type, не по TreeNode —
        // старые TreeNode всё равно уничтожаются при Nodes.Clear()), чтобы после
        // перестроения (например по нажатию "Создать своё правило") восстановить их,
        // а не разворачивать всё дерево заново
        var expandedTypes = isFirstBuild ? new HashSet<Type>() : CollectExpandedTypes();

        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();
        var root = _inheritance.BuildTree();
        var rootNode = CreateTreeNode(root);
        _treeView.Nodes.Add(rootNode);

        if (isFirstBuild)
        {
            // Только корень виден изначально — сами отделы (Medical, Engineering и т.д.)
            // свёрнуты при первом открытии окна
            rootNode.Expand();
        }
        else
        {
            RestoreExpandedState(rootNode, expandedTypes);
        }

        _treeView.EndUpdate();
    }

    private HashSet<Type> CollectExpandedTypes()
    {
        var result = new HashSet<Type>();
        void Walk(TreeNode n)
        {
            if (n.IsExpanded && n.Tag is DecalTypeNode dn) result.Add(dn.Type);
            foreach (TreeNode c in n.Nodes) Walk(c);
        }
        foreach (TreeNode root in _treeView.Nodes) Walk(root);
        return result;
    }

    private void RestoreExpandedState(TreeNode node, HashSet<Type> expandedTypes)
    {
        if (node.Tag is DecalTypeNode dn && expandedTypes.Contains(dn.Type))
            node.Expand();

        foreach (TreeNode child in node.Nodes)
            RestoreExpandedState(child, expandedTypes);
    }

    private TreeNode CreateTreeNode(DecalTypeNode node)
    {
        bool hasOwn = _inheritance.HasExplicitRule(node.Type);
        var tn = new TreeNode(node.DisplayName + (hasOwn ? "  ●" : "")) { Tag = node };
        tn.ForeColor = node.IsAbstractType ? Color.DimGray : (hasOwn ? Color.DarkGreen : Color.Black);
        foreach (var child in node.Children.OrderBy(c => c.DisplayName))
            tn.Nodes.Add(CreateTreeNode(child));
        return tn;
    }

    private void SelectNodeByType(Type type)
    {
        TreeNode? Find(TreeNode n)
        {
            if (n.Tag is DecalTypeNode dn && dn.Type == type) return n;
            foreach (TreeNode c in n.Nodes)
            {
                var found = Find(c);
                if (found != null) return found;
            }
            return null;
        }
        foreach (TreeNode root in _treeView.Nodes)
        {
            var found = Find(root);
            if (found != null) { _treeView.SelectedNode = found; found.EnsureVisible(); return; }
        }
    }

    private void ShowEditorFor(DecalTypeNode? node)
    {
        _editorPanel.Controls.Clear();
        if (node == null) return;

        int y = 8;
        _editorPanel.Controls.Add(new Label
        {
            Text = node.DisplayName,
            Location = new Point(8, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        });
        y += 30;

        bool hasOwn = _inheritance.HasExplicitRule(node.Type);
        var effective = _inheritance.ResolveEffectiveRule(node.Type);

        string statusText = hasOwn
            ? "🟢 Своё правило (переопределяет родителя)"
            : effective != null
                ? "⬆ Наследуется от родителя"
                : "— Правило нигде выше не задано";
        _editorPanel.Controls.Add(new Label
        {
            Text = statusText,
            Location = new Point(8, y),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8)
        });
        y += 24;

        var btnMakeOwn = new Button
        {
            Text = hasOwn ? "Убрать своё (наследовать снова)" : "Создать своё правило",
            Location = new Point(8, y),
            Width = 260,
            Height = 26,
            FlatStyle = FlatStyle.Flat
        };
        var makeOwnTip = new ToolTip();
        makeOwnTip.SetToolTip(btnMakeOwn, hasOwn
            ? "Удаляет собственное правило — тип снова начнёт наследовать от родителя"
            : "Создаёт независимую копию правила для этого типа. Паки каждого слоя " +
              "тоже клонируются персонально для него — правка цвета/позиций дальше " +
              "не затронет родителя и другие типы, использующие те же паки");
        btnMakeOwn.Click += (s, e) =>
        {
            if (hasOwn)
            {
                _inheritance.ClearRule(node.Type);
            }
            else
            {
                // Готовим "приватную" версию правила ДО вызова GetOrCreateOwn: у каждого
                // слоя, ссылающегося на пак, делаем персональную копию именно для этого
                // типа (CloneForOwnUse). Без этого шага слои после Clone() продолжали бы
                // указывать SourcePackId на тот же самый общий пак, что и родитель —
                // и правка цвета/позиций через общий список паков "протекала" бы сразу
                // во все типы, которые на него ссылаются.
                DecalRuleSet? seed = effective;
                if (seed != null)
                {
                    seed = seed.Clone();
                    foreach (var layer in seed.Layers)
                    {
                        if (string.IsNullOrEmpty(layer.SourcePackId)) continue;
                        var source = _packManager.GetById(layer.SourcePackId);
                        if (source == null) continue;

                        var clone = _packManager.CloneForOwnUse(source, $"{source.Name} ({node.DisplayName})");
                        layer.SourcePackId = clone.Id;
                    }
                }

                _inheritance.GetOrCreateOwn(node.Type, seedFrom: seed);
            }
            // OnChanged перестроит дерево и переоткроет редактор на этом же узле
        };
        _editorPanel.Controls.Add(btnMakeOwn);
        y += 32;

        var btnCascade = new Button
        {
            Text = "Применить также ко всем потомкам (снять их переопределения)",
            Location = new Point(8, y),
            Width = 380,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Enabled = hasOwn
        };
        btnCascade.Click += (s, e) =>
        {
            if (MessageBox.Show("Все дочерние типы этого узла потеряют собственные правила и начнут наследовать это. Продолжить?",
                "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _inheritance.ClearDescendantOverrides(node);
        };
        _editorPanel.Controls.Add(btnCascade);
        y += 40;

        if (!hasOwn)
        {
            RenderLayersReadOnly(effective, y);
            return;
        }

        var working = _inheritance.GetOrCreateOwn(node.Type);
        RenderLayersEditable(node.Type, node.DisplayName, working, y);
    }

    private void RenderLayersReadOnly(DecalRuleSet? ruleSet, int y)
    {
        if (ruleSet == null || ruleSet.Layers.Count == 0)
        {
            _editorPanel.Controls.Add(new Label { Text = "(слоёв нет)", Location = new Point(8, y), AutoSize = true, ForeColor = Color.Gray });
            return;
        }

        foreach (var layer in ruleSet.Layers)
        {
            var pack = !string.IsNullOrEmpty(layer.SourcePackId) ? _packManager.GetById(layer.SourcePackId) : null;
            string state = layer.Enabled ? "" : " (выкл)";
            _editorPanel.Controls.Add(new Label
            {
                Text = $"• {layer.Name}: {pack?.Name ?? "(не выбран)"}{state}",
                Location = new Point(8, y),
                AutoSize = true,
                ForeColor = Color.DimGray
            });
            y += 22;
        }
    }

    private void RenderLayersEditable(Type type, string typeDisplayName, DecalRuleSet ruleSet, int startY)
    {
        int y = startY;

        var btnAddLayer = new Button { Text = "➕ Добавить слой", Location = new Point(8, y), Width = 150, Height = 26, FlatStyle = FlatStyle.Flat };
        _editorPanel.Controls.Add(btnAddLayer);
        y += 32;

        var rowsHost = new Panel { Location = new Point(8, y), Size = new Size(_editorPanel.Width - 30, 300), AutoScroll = false };
        _editorPanel.Controls.Add(rowsHost);

        void RebuildRows()
        {
            rowsHost.Controls.Clear();
            int rowY = 0;

            for (int i = 0; i < ruleSet.Layers.Count; i++)
            {
                var layer = ruleSet.Layers[i];
                int idx = i;

                var row = new Panel { Location = new Point(0, rowY), Size = new Size(rowsHost.Width, 30) };

                var chk = new CheckBox { Checked = layer.Enabled, Location = new Point(0, 5), Width = 20 };
                chk.CheckedChanged += (s, e) => { layer.Enabled = chk.Checked; _inheritance.Save(); };
                row.Controls.Add(chk);

                var txtName = new TextBox { Text = layer.Name, Location = new Point(22, 3), Width = 90 };
                void CommitName()
                {
                    if (layer.Name == txtName.Text || string.IsNullOrWhiteSpace(txtName.Text)) return;
                    layer.Name = txtName.Text;
                    _inheritance.Save();
                }
                txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitName(); } };
                txtName.Leave += (s, e) => CommitName();
                row.Controls.Add(txtName);

                var currentPack = !string.IsNullOrEmpty(layer.SourcePackId) ? _packManager.GetById(layer.SourcePackId) : null;
                var btnPickPack = new Button
                {
                    Text = currentPack?.Name ?? "(не выбран)",
                    Location = new Point(116, 2),
                    Width = 100,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var pickTip = new ToolTip();
                pickTip.SetToolTip(btnPickPack, "Пак задаёт узор (позиции декалей). Цвет настраивается отдельной кнопкой справа — за пак он больше не отвечает.");
                btnPickPack.Click += (s, e) =>
                {
                    var dialog = new DecalPackDialog(_packManager, _indexer)
                    {
                        RescanCallback = () =>
                        {
                            var scanned = DecalPackScanner.ScanFromIndexer(_indexer, forceRescan: true);
                            var (added, updated) = _packManager.MergeScanned(scanned);
                            MessageBox.Show($"Добавлено новых: {added}, обновлено: {updated}", "Обновление паков");
                        }
                    };
                    dialog.OnPackSelected += (pack) =>
                    {
                        layer.SourcePackId = pack.Id;
                        btnPickPack.Text = pack.Name;
                        _inheritance.Save();
                    };
                    dialog.Show(this);
                };
                row.Controls.Add(btnPickPack);

                // Цвет теперь задаётся ЗДЕСЬ, локально для этого узла (типа комнаты), а не
                // в общем паке — так один и тот же пак (BrickTileWhite и т.п.) может быть
                // синим у Command и красным у Security, не затрагивая ни сам пак, ни другие
                // узлы/комнаты. null (не задан) — используется цвет пака по умолчанию.
                Color EffectiveColor() => ParseColorHex(layer.Color ?? currentPack?.Color ?? "#FFFFFFFF");

                var btnColor = new Button
                {
                    Location = new Point(219, 2),
                    Width = 36,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = EffectiveColor(),
                    Text = layer.Color == null ? "· авто" : "",
                    Font = new Font("Segoe UI", 6),
                    ForeColor = GetReadableTextColor(EffectiveColor())
                };
                var colorTip = new ToolTip();
                colorTip.SetToolTip(btnColor, $"Цвет декали для «{typeDisplayName}». Сейчас: {(layer.Color == null ? "по умолчанию из пака" : layer.Color)}");
                btnColor.Click += (s, e) =>
                {
                    if (ArgbColorPickerDialog.Pick(this, EffectiveColor(), out var picked))
                    {
                        layer.Color = ToHexColorLocal(picked);
                        btnColor.BackColor = picked;
                        btnColor.Text = "";
                        btnColor.ForeColor = GetReadableTextColor(picked);
                        colorTip.SetToolTip(btnColor, $"Цвет декали для «{typeDisplayName}». Сейчас: {layer.Color}");
                        _inheritance.Save();
                    }
                };
                row.Controls.Add(btnColor);

                var btnPalette = new Button
                {
                    Text = "🎨",
                    Location = new Point(257, 2),
                    Width = 22,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    Enabled = _indexer.GetPalettes().Count > 0
                };
                var paletteTip = new ToolTip();
                paletteTip.SetToolTip(btnPalette,
                    _indexer.GetPalettes().Count > 0
                        ? "Выбрать цвет из палитры репозитория (только для этого узла)"
                        : "Недоступно — в репозитории не найдено палитр");
                btnPalette.Click += (s, e) =>
                {
                    ShowPaletteColorPickerLocal(color =>
                    {
                        layer.Color = ToHexColorLocal(color);
                        _inheritance.Save();
                        RebuildRows();
                    });
                };
                row.Controls.Add(btnPalette);

                var btnResetColor = new Button
                {
                    Text = "⟲",
                    Location = new Point(281, 2),
                    Width = 20,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    Enabled = layer.Color != null
                };
                var resetTip = new ToolTip();
                resetTip.SetToolTip(btnResetColor, "Сбросить — снова использовать цвет пака по умолчанию");
                btnResetColor.Click += (s, e) =>
                {
                    layer.Color = null;
                    _inheritance.Save();
                    RebuildRows();
                };
                row.Controls.Add(btnResetColor);

                var btnUp = new Button { Text = "↑", Location = new Point(304, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnUp.Click += (s, e) =>
                {
                    if (idx <= 0) return;
                    (ruleSet.Layers[idx - 1], ruleSet.Layers[idx]) = (ruleSet.Layers[idx], ruleSet.Layers[idx - 1]);
                    _inheritance.Save();
                    RebuildRows();
                };
                row.Controls.Add(btnUp);

                var btnDown = new Button { Text = "↓", Location = new Point(328, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDown.Click += (s, e) =>
                {
                    if (idx >= ruleSet.Layers.Count - 1) return;
                    (ruleSet.Layers[idx + 1], ruleSet.Layers[idx]) = (ruleSet.Layers[idx], ruleSet.Layers[idx + 1]);
                    _inheritance.Save();
                    RebuildRows();
                };
                row.Controls.Add(btnDown);

                var btnDel = new Button { Text = "🗑", Location = new Point(352, 2), Width = 22, Height = 24, FlatStyle = FlatStyle.Flat };
                btnDel.Click += (s, e) =>
                {
                    ruleSet.Layers.RemoveAt(idx);
                    _inheritance.Save();
                    RebuildRows();
                };
                row.Controls.Add(btnDel);

                rowsHost.Controls.Add(row);
                rowY += 34;
            }
        }

        btnAddLayer.Click += (s, e) =>
        {
            ruleSet.Layers.Add(new DecalLayer { Name = $"Слой {ruleSet.Layers.Count + 1}" });
            _inheritance.Save();
            RebuildRows();
        };

        RebuildRows();
    }

    private static Color ParseColorHex(string hex)
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

    private static string ToHexColorLocal(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private static Color GetReadableTextColor(Color background)
    {
        int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return brightness < 128 ? Color.White : Color.Black;
    }

    /// <summary>
    /// Отдельное окошко: выбор палитры (из "- type: palette" репозитория) + сетка свотчей.
    /// Клик по свотчу вызывает onColorPicked и закрывает окно. Самостоятельная копия
    /// логики из DecalPackDialog.ShowPaletteColorPicker — этот диалог не должен зависеть
    /// ни от MainForm, ни от DecalPackDialog.
    /// </summary>
    private void ShowPaletteColorPickerLocal(Action<Color> onColorPicked)
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
                var swatchColor = ParseColorHex(kvp.Value);
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
                    string decalHex = ToDecalColorFormatLocal(paletteHex);
                    onColorPicked(ParseColorHex(decalHex));
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
    private static string ToDecalColorFormatLocal(string paletteHex)
    {
        var h = paletteHex.TrimStart('#');
        if (h.Length == 6) return $"#{h.ToUpperInvariant()}FF";
        if (h.Length == 8) return $"#{h.ToUpperInvariant()}";
        return "#FFFFFFFF";
    }
}