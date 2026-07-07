// Forms/RoomTypeDialog.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class RoomTypeDialog : Form
{
    private readonly RoomTypeManager _manager;
    private readonly TreeView _treeView;
    private readonly Panel _editorPanel;
    private RoomType? _selectedType;
    private RoomType? _previousSelectedType;
    private bool _isEditing = false;
    private Button? _btnCreateType;
    private Button? _btnDeleteType;
    private Button? _btnImport;
    private Button? _btnExport;
    private Button? _btnSave;

    public RoomTypeDialog(RoomTypeManager manager)
    {
        _manager = manager;

        // Фиксированные размеры окна
        this.Text = "Выбор и настройка типов комнат";
        this.Size = new Size(670, 600);
        this.MinimumSize = new Size(600, 500);
        this.MaximumSize = new Size(1000, 800);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.ShowInTaskbar = false;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.TopMost = false;

        // ===== ВЕРХНЯЯ ПАНЕЛЬ С ЗАГОЛОВКОМ =====
        var headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(670, 40),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var headerLabel = new Label
        {
            Text = "Управление типами комнат",
            Location = new Point(10, 8),
            Size = new Size(300, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 50)
        };
        headerPanel.Controls.Add(headerLabel);

        // ===== ОСНОВНАЯ ПАНЕЛЬ =====
        var mainPanel = new Panel
        {
            Location = new Point(0, 40),
            Size = new Size(670, 510)
        };

        // ===== ЛЕВАЯ ПАНЕЛЬ (300px) =====
        var leftPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(300, 510),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Панель с кнопками (верхняя часть) - высота 40px для одного ряда
        var buttonPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(300, 40),
            BackColor = Color.FromArgb(248, 248, 248)
        };

        // Ширина каждой кнопки = (300 - 5*5) / 4 = 275 / 4 ≈ 68px
        int btnWidth = 68;
        int btnSpacing = 5;

        // Кнопка "Создать тип"
        _btnCreateType = new Button
        {
            Text = "➕",
            Location = new Point(btnSpacing, 7),
            Size = new Size(btnWidth, 26),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 1,
                BorderColor = Color.FromArgb(100, 150, 200),
                MouseDownBackColor = Color.FromArgb(200, 220, 240),
                MouseOverBackColor = Color.FromArgb(210, 230, 250)
            },
            BackColor = Color.FromArgb(220, 235, 250),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnCreateType.Click += (s, e) => CreateNewType();
        buttonPanel.Controls.Add(_btnCreateType);

        // Кнопка "Удалить" - чёрный текст
        _btnDeleteType = new Button
        {
            Text = "🗑️",
            Location = new Point(btnSpacing + btnWidth + btnSpacing, 7),
            Size = new Size(btnWidth, 26),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 1,
                BorderColor = Color.FromArgb(200, 150, 150),
                MouseDownBackColor = Color.FromArgb(240, 220, 220),
                MouseOverBackColor = Color.FromArgb(250, 230, 230)
            },
            BackColor = Color.FromArgb(250, 240, 240),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDeleteType.Click += (s, e) => DeleteSelectedType();
        buttonPanel.Controls.Add(_btnDeleteType);

        // Кнопка "Импортировать"
        _btnImport = new Button
        {
            Text = "📥",
            Location = new Point(btnSpacing + (btnWidth + btnSpacing) * 2, 7),
            Size = new Size(btnWidth, 26),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 1,
                BorderColor = Color.FromArgb(180, 180, 150),
                MouseDownBackColor = Color.FromArgb(240, 240, 220),
                MouseOverBackColor = Color.FromArgb(250, 250, 230)
            },
            BackColor = Color.FromArgb(245, 245, 220),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnImport.Click += (s, e) => ImportType();
        buttonPanel.Controls.Add(_btnImport);

        // Кнопка "Экспортировать"
        _btnExport = new Button
        {
            Text = "📤",
            Location = new Point(btnSpacing + (btnWidth + btnSpacing) * 3, 7),
            Size = new Size(btnWidth, 26),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 1,
                BorderColor = Color.FromArgb(150, 180, 150),
                MouseDownBackColor = Color.FromArgb(220, 240, 220),
                MouseOverBackColor = Color.FromArgb(230, 250, 230)
            },
            BackColor = Color.FromArgb(220, 245, 220),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnExport.Click += (s, e) => ExportType();
        buttonPanel.Controls.Add(_btnExport);

        leftPanel.Controls.Add(buttonPanel);

        // TreeView - начинается после панели кнопок (Y = 40)
        _treeView = new TreeView
        {
            Location = new Point(0, 40),
            Size = new Size(300, 470),
            Font = new Font("Segoe UI", 10),
            Indent = 20,
            ShowRootLines = true,
            ShowPlusMinus = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        _treeView.AfterSelect += OnTreeViewSelect;
        _treeView.NodeMouseDoubleClick += OnTreeViewDoubleClick;
        _treeView.KeyDown += OnTreeViewKeyDown;
        leftPanel.Controls.Add(_treeView);

        // ===== ПРАВАЯ ПАНЕЛЬ =====
        _editorPanel = new Panel
        {
            Location = new Point(305, 0),
            Size = new Size(360, 510),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Padding = new Padding(8),
            AutoScroll = true
        };

        // ===== НИЖНЯЯ ПАНЕЛЬ =====
        var bottomPanel = new Panel
        {
            Location = new Point(0, 550),
            Size = new Size(670, 50),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnApply = new Button
        {
            Text = "Применить",
            Location = new Point(470, 10),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(220, 230, 240),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnApply.Click += (s, e) => ApplyType();
        bottomPanel.Controls.Add(btnApply);

        var btnClose = new Button
        {
            Text = "Закрыть",
            Location = new Point(565, 10),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => this.Close();
        bottomPanel.Controls.Add(btnClose);

        // Добавляем все панели на форму
        this.Controls.Add(headerPanel);
        this.Controls.Add(mainPanel);
        this.Controls.Add(bottomPanel);

        mainPanel.Controls.Add(leftPanel);
        mainPanel.Controls.Add(_editorPanel);

        // Обновляем дерево
        UpdateTreeView();
    }

    private void UpdateTreeView()
    {
        _treeView.Nodes.Clear();
        var categories = _manager.GetCategories();

        foreach (var category in categories.OrderBy(c => c.Key))
        {
            var node = new TreeNode($"📁 {category.Key}");
            node.Tag = new CategoryNode { Name = category.Key, IsCategory = true };

            foreach (var type in category.Value.OrderBy(t => t.Name))
            {
                var typeNode = new TreeNode(type.Name)
                {
                    Tag = new CategoryNode { Name = type.Name, IsCategory = false, RoomType = type },
                    ForeColor = type.IsCustom ? Color.Blue : Color.Black
                };
                node.Nodes.Add(typeNode);
            }
            _treeView.Nodes.Add(node);
        }
    }

    private void SelectTypeInTree(string typeName)
    {
        foreach (TreeNode categoryNode in _treeView.Nodes)
        {
            foreach (TreeNode typeNode in categoryNode.Nodes)
            {
                if (typeNode.Tag is CategoryNode node && node.RoomType != null && node.RoomType.Name == typeName)
                {
                    _treeView.SelectedNode = typeNode;
                    typeNode.EnsureVisible();
                    return;
                }
            }
        }
    }

    private void OnTreeViewSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is CategoryNode node)
        {
            if (!node.IsCategory && node.RoomType != null)
            {
                _selectedType = node.RoomType;
                _previousSelectedType = node.RoomType; // Сохраняем последний выбранный тип
                _manager.SelectType(_selectedType.Name);
                UpdateTreeViewSelection();
                ShowEditor(node.RoomType);
                OnTypeSelected?.Invoke(_selectedType.Name);

                UpdateDeleteButtonState();
            }
            else
            {
                _selectedType = null;
                ShowCategoryInfo(node.Name);

                if (_btnDeleteType != null)
                    _btnDeleteType.Enabled = false;
            }
        }
    }

    private void UpdateDeleteButtonState()
    {
        if (_btnDeleteType == null) return;
        _btnDeleteType.Enabled = _selectedType != null && _selectedType.IsCustom;
    }

    public event Action<string>? OnTypeSelected;

    private void UpdateTreeViewSelection()
    {
        string selectedTypeName = _manager.SelectedType;

        foreach (TreeNode categoryNode in _treeView.Nodes)
        {
            foreach (TreeNode typeNode in categoryNode.Nodes)
            {
                if (typeNode.Tag is CategoryNode node && node.RoomType != null)
                {
                    typeNode.BackColor = node.RoomType.Name == selectedTypeName ?
                        Color.LightBlue : Color.White;
                }
            }
        }
    }

    private void OnTreeViewDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_treeView.SelectedNode?.Tag is CategoryNode node && !node.IsCategory)
        {
            ApplyType();
        }
    }

    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_treeView.SelectedNode?.Tag is CategoryNode node)
        {
            if (e.KeyCode == Keys.Enter && !node.IsCategory && node.RoomType != null)
            {
                ApplyType();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Delete && node.RoomType?.IsCustom == true)
            {
                DeleteType(node.Name);
                e.Handled = true;
            }
        }
    }

    private void ShowEditor(RoomType type)
    {
        _editorPanel.Controls.Clear();
        _isEditing = true;

        int panelWidth = _editorPanel.Width - 16;

        var titleLabel = new Label
        {
            Text = $"✏️ Редактирование: {type.Name}",
            Location = new Point(5, 5),
            Size = new Size(panelWidth - 10, 25),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = type.IsCustom ? Color.Blue : Color.Black
        };
        _editorPanel.Controls.Add(titleLabel);

        int y = 34;
        int labelWidth = 85;
        int rowHeight = 35;
        int leftMargin = 5;
        int btnColorSize = 30;
        int btnColorHeight = 30;
        int spacing = 8;

        int controlWidthFull = panelWidth - labelWidth - 15;
        int controlWidthWithButton = panelWidth - labelWidth - 15 - btnColorSize - spacing;

        // Поле "Название"
        var txtName = CreateTextBox(type.Name, leftMargin, y, labelWidth, controlWidthFull);
        var lblName = CreateLabel("Название:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Категория"
        var txtCategory = CreateTextBox(type.Category, leftMargin, y, labelWidth, controlWidthFull);
        var lblCategory = CreateLabel("Категория:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Стена"
        var txtWall = CreateTextBox(type.WallProto, leftMargin, y, labelWidth, controlWidthFull);
        var lblWall = CreateLabel("Стена:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Пол"
        var txtFloor = CreateTextBox(type.FloorProto, leftMargin, y, labelWidth, controlWidthFull);
        var lblFloor = CreateLabel("Пол:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Дверь"
        var txtDoor = CreateTextBox(type.DoorProto, leftMargin, y, labelWidth, controlWidthFull);
        var lblDoor = CreateLabel("Дверь:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Стекл. дверь"
        var txtGlassDoor = CreateTextBox(type.GlassDoorProto, leftMargin, y, labelWidth, controlWidthFull);
        var lblGlassDoor = CreateLabel("Стекл. дверь:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Цвет заливки
        var txtFill = CreateTextBox($"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}",
            leftMargin, y, labelWidth, controlWidthWithButton);
        var lblFill = CreateLabel("Цвет заливки:", leftMargin, y + 3, labelWidth);
        var btnPickFill = new Button
        {
            Text = "🎨",
            Location = new Point(leftMargin + labelWidth + 5 + controlWidthWithButton + spacing,
                                 y + (rowHeight - btnColorHeight) / 2 + 1 - 9),
            Size = new Size(btnColorSize, btnColorHeight),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10)
        };
        btnPickFill.Click += (s, e) =>
        {
            var parts = txtFill.Text.Split(',').Select(int.Parse).ToArray();
            using var dialog = new ColorDialog();
            dialog.Color = Color.FromArgb(parts[0], parts[1], parts[2], parts[3]);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtFill.Text = $"{dialog.Color.A},{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
            }
        };
        _editorPanel.Controls.Add(btnPickFill);
        y += rowHeight;

        // Цвет линии
        var txtLine = CreateTextBox($"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}",
            leftMargin, y, labelWidth, controlWidthWithButton);
        var lblLine = CreateLabel("Цвет линии:", leftMargin, y + 3, labelWidth);
        var btnPickLine = new Button
        {
            Text = "🎨",
            Location = new Point(leftMargin + labelWidth + 5 + controlWidthWithButton + spacing,
                                 y + (rowHeight - btnColorHeight) / 2 + 1 - 9),
            Size = new Size(btnColorSize, btnColorHeight),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10)
        };
        btnPickLine.Click += (s, e) =>
        {
            var parts = txtLine.Text.Split(',').Select(int.Parse).ToArray();
            using var dialog = new ColorDialog();
            dialog.Color = Color.FromArgb(parts[0], parts[1], parts[2], parts[3]);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtLine.Text = $"{dialog.Color.A},{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
            }
        };
        _editorPanel.Controls.Add(btnPickLine);
        y += rowHeight;

        // Поле "Приоритет"
        var txtPriority = CreateTextBox(_manager.GetPriorityForType(type.Name).ToString(), leftMargin, y, labelWidth, controlWidthFull);
        var lblPriority = CreateLabel("Приоритет:", leftMargin, y + 3, labelWidth);
        y += rowHeight + 6;

        // Кнопка "Сохранить"
        _btnSave = new Button
        {
            Text = "💾 Сохранить",
            Location = new Point(leftMargin, y),
            Size = new Size(panelWidth - 10, 35),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 150, 200) },
            BackColor = Color.FromArgb(180, 230, 255),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(0, 70, 120),
            Visible = type.IsCustom
        };
        _btnSave.Click += (s, e) =>
        {
            try
            {
                var fill = txtFill.Text.Split(',').Select(int.Parse).ToArray();
                var line = txtLine.Text.Split(',').Select(int.Parse).ToArray();
                var priority = int.Parse(txtPriority.Text);

                if (type.IsCustom)
                {
                    _manager.EditCustomType(
                        type.Name,
                        txtName.Text,
                        txtCategory.Text,
                        txtWall.Text,
                        txtFloor.Text,
                        txtDoor.Text,
                        txtGlassDoor.Text,
                        Color.FromArgb(fill[0], fill[1], fill[2], fill[3]),
                        Color.FromArgb(line[0], line[1], line[2], line[3]),
                        priority
                    );
                }
                else
                {
                    _manager.CreateCustomType(
                        txtName.Text,
                        txtCategory.Text,
                        txtWall.Text,
                        txtFloor.Text,
                        txtDoor.Text,
                        txtGlassDoor.Text,
                        Color.FromArgb(fill[0], fill[1], fill[2], fill[3]),
                        Color.FromArgb(line[0], line[1], line[2], line[3]),
                        priority
                    );
                }
                UpdateTreeView();
                MessageBox.Show("Тип сохранён!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        _editorPanel.Controls.Add(_btnSave);

        // Добавляем все элементы на панель
        _editorPanel.Controls.AddRange(new Control[] {
        lblName, txtName, lblCategory, txtCategory, lblWall, txtWall,
        lblFloor, txtFloor, lblDoor, txtDoor, lblGlassDoor, txtGlassDoor,
        lblFill, txtFill, lblLine, txtLine, lblPriority, txtPriority
    });
    }

    private Label CreateLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 16),
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private TextBox CreateTextBox(string text, int x, int y, int labelWidth, int controlWidth)
    {
        return new TextBox
        {
            Text = text,
            Location = new Point(x + labelWidth + 5, y),
            Size = new Size(controlWidth, 20),
            Font = new Font("Segoe UI", 8)
        };
    }

    private void ShowCategoryInfo(string categoryName)
    {
        _editorPanel.Controls.Clear();
        _isEditing = false;

        var label = new Label
        {
            Text = $"📁 Категория: {categoryName}\n\nВыберите тип для редактирования\nили создайте новый тип в этой категории.",
            Location = new Point(10, 10),
            Size = new Size(330, 100),
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        _editorPanel.Controls.Add(label);
    }

    private void AddCategory()
    {
        var input = InputBox.Show("Введите название категории:", "Новая категория");
        if (!string.IsNullOrEmpty(input))
        {
            _manager.CreateCustomType(
                $"{input}_Placeholder",
                input,
                "WallSolid",
                "Plating",
                "Airlock",
                "AirlockGlass",
                Color.FromArgb(200, 230, 230, 230),
                Color.FromArgb(255, 180, 180, 180),
                0
            );
            UpdateTreeView();
        }
    }

    private void CreateNewType()
    {
        // Сохраняем текущий выбранный тип перед переходом в режим создания
        _previousSelectedType = _selectedType;
        
        _editorPanel.Controls.Clear();
        _isEditing = true;

        int panelWidth = _editorPanel.Width - 16;

        var titleLabel = new Label
        {
            Text = "✨ Создание нового типа",
            Location = new Point(5, 5),
            Size = new Size(panelWidth - 10, 25),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.Blue
        };
        _editorPanel.Controls.Add(titleLabel);

        int y = 34;
        int labelWidth = 85;
        int rowHeight = 35;
        int leftMargin = 5;
        int btnColorSize = 30;
        int btnColorHeight = 30;
        int spacing = 8;

        int controlWidthFull = panelWidth - labelWidth - 15;
        int controlWidthWithButton = panelWidth - labelWidth - 15 - btnColorSize - spacing;

        var defaultColor = Color.FromArgb(200, 230, 230, 230);
        var defaultLineColor = Color.FromArgb(255, 180, 180, 180);

        // Поле "Название"
        var txtName = CreateTextBox("", leftMargin, y, labelWidth, controlWidthFull);
        var lblName = CreateLabel("Название:*", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Категория"
        var txtCategory = CreateTextBox("Custom", leftMargin, y, labelWidth, controlWidthFull);
        var lblCategory = CreateLabel("Категория:*", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Стена"
        var txtWall = CreateTextBox("WallSolid", leftMargin, y, labelWidth, controlWidthFull);
        var lblWall = CreateLabel("Стена:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Пол"
        var txtFloor = CreateTextBox("Plating", leftMargin, y, labelWidth, controlWidthFull);
        var lblFloor = CreateLabel("Пол:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Дверь"
        var txtDoor = CreateTextBox("Airlock", leftMargin, y, labelWidth, controlWidthFull);
        var lblDoor = CreateLabel("Дверь:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Поле "Стекл. дверь"
        var txtGlassDoor = CreateTextBox("AirlockGlass", leftMargin, y, labelWidth, controlWidthFull);
        var lblGlassDoor = CreateLabel("Стекл. дверь:", leftMargin, y + 3, labelWidth);
        y += rowHeight;

        // Цвет заливки
        var txtFill = CreateTextBox($"{defaultColor.A},{defaultColor.R},{defaultColor.G},{defaultColor.B}",
            leftMargin, y, labelWidth, controlWidthWithButton);
        var lblFill = CreateLabel("Цвет заливки:", leftMargin, y + 3, labelWidth);
        var btnPickFill = new Button
        {
            Text = "🎨",
            Location = new Point(leftMargin + labelWidth + 5 + controlWidthWithButton + spacing,
                                 y + (rowHeight - btnColorHeight) / 2 + 1 - 9),
            Size = new Size(btnColorSize, btnColorHeight),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10)
        };
        btnPickFill.Click += (s, e) =>
        {
            var parts = txtFill.Text.Split(',').Select(int.Parse).ToArray();
            using var dialog = new ColorDialog();
            dialog.Color = Color.FromArgb(parts[0], parts[1], parts[2], parts[3]);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtFill.Text = $"{dialog.Color.A},{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
            }
        };
        _editorPanel.Controls.Add(btnPickFill);
        y += rowHeight;

        // Цвет линии
        var txtLine = CreateTextBox($"{defaultLineColor.A},{defaultLineColor.R},{defaultLineColor.G},{defaultLineColor.B}",
            leftMargin, y, labelWidth, controlWidthWithButton);
        var lblLine = CreateLabel("Цвет линии:", leftMargin, y + 3, labelWidth);
        var btnPickLine = new Button
        {
            Text = "🎨",
            Location = new Point(leftMargin + labelWidth + 5 + controlWidthWithButton + spacing,
                                 y + (rowHeight - btnColorHeight) / 2 + 1 - 9),
            Size = new Size(btnColorSize, btnColorHeight),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10)
        };
        btnPickLine.Click += (s, e) =>
        {
            var parts = txtLine.Text.Split(',').Select(int.Parse).ToArray();
            using var dialog = new ColorDialog();
            dialog.Color = Color.FromArgb(parts[0], parts[1], parts[2], parts[3]);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtLine.Text = $"{dialog.Color.A},{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
            }
        };
        _editorPanel.Controls.Add(btnPickLine);
        y += rowHeight;

        // Поле "Приоритет"
        var txtPriority = CreateTextBox("0", leftMargin, y, labelWidth, controlWidthFull);
        var lblPriority = CreateLabel("Приоритет:", leftMargin, y + 3, labelWidth);
        y += rowHeight + 6;

        // Кнопка "Создать тип"
        var btnCreate = new Button
        {
            Text = "✅ Создать тип",
            Location = new Point(leftMargin, y),
            Size = new Size(panelWidth - 10, 35),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 0) },
            BackColor = Color.FromArgb(180, 255, 180),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(0, 100, 0)
        };
        btnCreate.Click += (s, e) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Введите название типа!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtCategory.Text))
                {
                    MessageBox.Show("Введите категорию!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var fill = txtFill.Text.Split(',').Select(int.Parse).ToArray();
                var line = txtLine.Text.Split(',').Select(int.Parse).ToArray();
                var priority = int.Parse(txtPriority.Text);

                _manager.CreateCustomType(
                    txtName.Text,
                    txtCategory.Text,
                    txtWall.Text,
                    txtFloor.Text,
                    txtDoor.Text,
                    txtGlassDoor.Text,
                    Color.FromArgb(fill[0], fill[1], fill[2], fill[3]),
                    Color.FromArgb(line[0], line[1], line[2], line[3]),
                    priority
                );

                UpdateTreeView();

                // Находим и выбираем созданный тип в дереве
                SelectTypeInTree(txtName.Text);

                var createdType = _manager.GetRoomType(txtName.Text);
                if (createdType != null)
                {
                    _selectedType = createdType;
                    _previousSelectedType = createdType;
                    _manager.SelectType(createdType.Name);
                    UpdateTreeViewSelection();
                    ShowEditor(createdType);
                    UpdateDeleteButtonState();
                }

                MessageBox.Show($"Тип '{txtName.Text}' успешно создан!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        _editorPanel.Controls.Add(btnCreate);

        // Кнопка "Отмена"
        var btnCancel = new Button
        {
            Text = "❌ Отмена",
            Location = new Point(leftMargin, y + 40),
            Size = new Size(panelWidth - 10, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 150) },
            BackColor = Color.FromArgb(240, 220, 220),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(150, 0, 0)
        };
        btnCancel.Click += (s, e) =>
        {
            // Возвращаемся к предыдущему выбранному типу, если он был
            if (_previousSelectedType != null)
            {
                _selectedType = _previousSelectedType;
                _manager.SelectType(_selectedType.Name);
                UpdateTreeViewSelection();
                ShowEditor(_selectedType);
                UpdateDeleteButtonState();
                
                // Выделяем его в дереве
                SelectTypeInTree(_selectedType.Name);
            }
            else
            {
                _selectedType = null;
                ShowCategoryInfo("Все категории");
                _treeView.SelectedNode = null;
                if (_btnDeleteType != null)
                    _btnDeleteType.Enabled = false;
            }
        };
        _editorPanel.Controls.Add(btnCancel);

        _editorPanel.Controls.AddRange(new Control[] {
            lblName, txtName, lblCategory, txtCategory, lblWall, txtWall,
            lblFloor, txtFloor, lblDoor, txtDoor, lblGlassDoor, txtGlassDoor,
            lblFill, txtFill, lblLine, txtLine, lblPriority, txtPriority
        });
    }

    private void DeleteSelectedType()
    {
        if (_selectedType == null)
        {
            MessageBox.Show("Сначала выберите тип для удаления!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_selectedType.IsCustom)
        {
            MessageBox.Show("Нельзя удалить встроенный тип!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show($"Удалить тип '{_selectedType.Name}'?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _manager.DeleteCustomType(_selectedType.Name);
            _selectedType = null;
            UpdateTreeView();
            _editorPanel.Controls.Clear();
            ShowCategoryInfo("Все категории");
            MessageBox.Show($"Тип успешно удалён!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (_btnDeleteType != null)
                _btnDeleteType.Enabled = false;
        }
    }

    private void ImportType()
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Импорт типа комнаты",
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _manager.ImportType(openFileDialog.FileName);

                var categories = _manager.GetCategories().Keys.ToList();
                if (categories.Count > 0)
                {
                    var categoryDialog = new CategorySelectionDialog(categories);
                    if (categoryDialog.ShowDialog() == DialogResult.OK)
                    {
                        var selectedCategory = categoryDialog.SelectedCategory;
                        if (!string.IsNullOrEmpty(selectedCategory))
                        {
                            var importedType = _manager.GetAllTypeNames().LastOrDefault();
                            if (importedType != null)
                            {
                                var type = _manager.GetRoomType(importedType);
                                if (type != null && type.IsCustom)
                                {
                                    _manager.EditCustomType(
                                        importedType,
                                        type.Name,
                                        selectedCategory,
                                        type.WallProto,
                                        type.FloorProto,
                                        type.DoorProto,
                                        type.GlassDoorProto,
                                        type.FillColor,
                                        type.LineColor,
                                        _manager.GetPriorityForType(type.Name)
                                    );
                                }
                            }
                        }
                    }
                }

                UpdateTreeView();
                MessageBox.Show("Тип успешно импортирован!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportType()
    {
        if (_selectedType == null)
        {
            MessageBox.Show("Сначала выберите тип для экспорта!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var saveFileDialog = new SaveFileDialog
        {
            Title = $"Экспорт типа: {_selectedType.Name}",
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
            FileName = $"{_selectedType.Name}.json"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _manager.ExportType(_selectedType.Name, saveFileDialog.FileName);
                MessageBox.Show($"Тип '{_selectedType.Name}' успешно экспортирован!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void DeleteType(string name)
    {
        if (MessageBox.Show($"Удалить тип '{name}'?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            _manager.DeleteCustomType(name);
            UpdateTreeView();
        }
    }

    private void ApplyType()
    {
        if (_selectedType != null)
        {
            _manager.SelectType(_selectedType.Name);
        }
    }

    private class CategoryNode
    {
        public string Name { get; set; } = "";
        public bool IsCategory { get; set; } = false;
        public RoomType? RoomType { get; set; }
    }
}

public class CategorySelectionDialog : Form
{
    private ComboBox _categoryComboBox;
    private Button _btnOk;
    private Button _btnCancel;
    public string SelectedCategory { get; private set; } = "";

    public CategorySelectionDialog(List<string> categories)
    {
        this.Text = "Выберите категорию";
        this.Size = new Size(350, 130);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.ShowInTaskbar = false;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var label = new Label
        {
            Text = "Выберите категорию для импортированного типа:",
            Location = new Point(10, 10),
            Size = new Size(320, 20),
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(label);

        _categoryComboBox = new ComboBox
        {
            Location = new Point(10, 35),
            Size = new Size(320, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        _categoryComboBox.Items.AddRange(categories.ToArray());
        if (_categoryComboBox.Items.Count > 0)
            _categoryComboBox.SelectedIndex = 0;
        this.Controls.Add(_categoryComboBox);

        _btnOk = new Button
        {
            Text = "OK",
            Location = new Point(170, 70),
            Size = new Size(75, 25),
            DialogResult = DialogResult.OK,
            Font = new Font("Segoe UI", 9)
        };
        _btnOk.Click += (s, e) => { SelectedCategory = _categoryComboBox.SelectedItem?.ToString() ?? ""; };
        this.Controls.Add(_btnOk);

        _btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(255, 70),
            Size = new Size(75, 25),
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(_btnCancel);

        this.AcceptButton = _btnOk;
        this.CancelButton = _btnCancel;
    }
}

public static class InputBox
{
    public static string Show(string prompt, string title)
    {
        var form = new Form
        {
            Text = title,
            Size = new Size(350, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ShowInTaskbar = false
        };

        var label = new Label { Text = prompt, Location = new Point(10, 10), AutoSize = true };
        var textBox = new TextBox { Location = new Point(10, 35), Width = 310 };
        var btnOk = new Button { Text = "OK", Location = new Point(160, 70), Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Отмена", Location = new Point(245, 70), Width = 80, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : "";
    }
}