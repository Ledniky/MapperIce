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
    private bool _isEditing = false;

    public RoomTypeDialog(RoomTypeManager manager)
    {
        _manager = manager;

        // Фиксированные размеры окна
        this.Text = "Выбор и настройка типов комнат";
        this.Size = new Size(800, 600);
        this.MinimumSize = new Size(700, 500);
        this.MaximumSize = new Size(1200, 800);
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
            Size = new Size(800, 40),
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
            Size = new Size(800, 510)
        };

        // ===== ЛЕВАЯ ПАНЕЛЬ (300px) =====
        var leftPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(300, 510),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Панель с кнопками (верхняя часть) - высота 45px
        var buttonPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(300, 45),
            BackColor = Color.FromArgb(248, 248, 248)
        };

        // Кнопка "Создать тип"
        var btnCreateType = new Button
        {
            Text = "➕ Создать тип",
            Location = new Point(5, 8),
            Size = new Size(95, 30),
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
            ForeColor = Color.Black
        };
        btnCreateType.Click += (s, e) => CreateNewType();
        buttonPanel.Controls.Add(btnCreateType);

        // Кнопка "Удалить"
        var btnDeleteType = new Button
        {
            Text = "🗑️ Удалить",
            Location = new Point(105, 8),
            Size = new Size(70, 30),
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
            ForeColor = Color.Black
        };
        btnDeleteType.Click += (s, e) => DeleteSelectedType();
        buttonPanel.Controls.Add(btnDeleteType);

        // Кнопка "Импортировать"
        var btnImport = new Button
        {
            Text = "📥 Импорт",
            Location = new Point(180, 8),
            Size = new Size(55, 30),
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
            ForeColor = Color.Black
        };
        btnImport.Click += (s, e) => ImportType();
        buttonPanel.Controls.Add(btnImport);

        // Кнопка "Экспортировать"
        var btnExport = new Button
        {
            Text = "📤 Экспорт",
            Location = new Point(240, 8),
            Size = new Size(55, 30),
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
            ForeColor = Color.Black
        };
        btnExport.Click += (s, e) => ExportType();
        buttonPanel.Controls.Add(btnExport);

        leftPanel.Controls.Add(buttonPanel);

        // TreeView - начинается после панели кнопок (Y = 45)
        _treeView = new TreeView
        {
            Location = new Point(0, 45),
            Size = new Size(300, 465),
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
            Size = new Size(490, 510),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Padding = new Padding(10),
            AutoScroll = true
        };

        // ===== НИЖНЯЯ ПАНЕЛЬ =====
        var bottomPanel = new Panel
        {
            Location = new Point(0, 550),
            Size = new Size(800, 50),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnApply = new Button
        {
            Text = "Применить",
            Location = new Point(590, 10),
            Size = new Size(90, 30),
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
            Location = new Point(690, 10),
            Size = new Size(90, 30),
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

    private void OnTreeViewSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is CategoryNode node)
        {
            if (!node.IsCategory && node.RoomType != null)
            {
                _selectedType = node.RoomType;
                _manager.SelectType(_selectedType.Name);
                UpdateTreeViewSelection();
                ShowEditor(node.RoomType);
                OnTypeSelected?.Invoke(_selectedType.Name);
            }
            else
            {
                _selectedType = null;
                ShowCategoryInfo(node.Name);
            }
        }
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

        // Заголовок
        var titleLabel = new Label
        {
            Text = $"✏️ Редактирование: {type.Name}",
            Location = new Point(5, 5),
            Size = new Size(470, 30),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = type.IsCustom ? Color.Blue : Color.Black
        };
        _editorPanel.Controls.Add(titleLabel);

        // Поля ввода
        int y = 45;
        int labelWidth = 120;
        int controlWidth = 330;
        int rowHeight = 35;

        var txtName = CreateTextBox(type.Name, 5, y, labelWidth, controlWidth);
        var lblName = CreateLabel("Название:", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtCategory = CreateTextBox(type.Category, 5, y, labelWidth, controlWidth);
        var lblCategory = CreateLabel("Категория:", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtWall = CreateTextBox(type.WallProto, 5, y, labelWidth, controlWidth);
        var lblWall = CreateLabel("Стена (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtFloor = CreateTextBox(type.FloorProto, 5, y, labelWidth, controlWidth);
        var lblFloor = CreateLabel("Пол (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtDoor = CreateTextBox(type.DoorProto, 5, y, labelWidth, controlWidth);
        var lblDoor = CreateLabel("Дверь (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtGlassDoor = CreateTextBox(type.GlassDoorProto, 5, y, labelWidth, controlWidth);
        var lblGlassDoor = CreateLabel("Стекл. дверь:", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtFill = CreateTextBox($"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}", 
            5, y, labelWidth, controlWidth - 40);
        var lblFill = CreateLabel("Цвет заливки:", 5, y + 5, labelWidth);
        var btnPickFill = new Button
        {
            Text = "🎨",
            Location = new Point(5 + labelWidth + 5 + controlWidth - 30, y - 4),
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand
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

        var txtLine = CreateTextBox($"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}", 
            5, y, labelWidth, controlWidth - 40);
        var lblLine = CreateLabel("Цвет линии:", 5, y + 5, labelWidth);
        var btnPickLine = new Button
        {
            Text = "🎨",
            Location = new Point(5 + labelWidth + 5 + controlWidth - 30, y - 4),
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand
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

        var txtPriority = CreateTextBox(_manager.GetPriorityForType(type.Name).ToString(), 5, y, labelWidth, controlWidth);
        var lblPriority = CreateLabel("Приоритет:", 5, y + 5, labelWidth);
        y += rowHeight + 10;

        var btnSave = new Button
        {
            Text = "💾 Сохранить изменения",
            Location = new Point(5, y),
            Size = new Size(470, 40),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(150, 200, 150) },
            BackColor = Color.FromArgb(220, 240, 220),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.Click += (s, e) =>
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
        _editorPanel.Controls.Add(btnSave);

        // Добавляем метки и поля
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
            Size = new Size(width, 20),
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private TextBox CreateTextBox(string text, int x, int y, int labelWidth, int controlWidth)
    {
        return new TextBox
        {
            Text = text,
            Location = new Point(x + labelWidth + 5, y),
            Size = new Size(controlWidth, 25),
            Font = new Font("Segoe UI", 9)
        };
    }

    private void ShowCategoryInfo(string categoryName)
    {
        _editorPanel.Controls.Clear();
        _isEditing = false;

        var label = new Label
        {
            Text = $"📁 Категория: {categoryName}\n\nВыберите тип для редактирования\nили создайте новый тип в этой категории.",
            Location = new Point(20, 20),
            Size = new Size(450, 150),
            Font = new Font("Segoe UI", 12),
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
        _editorPanel.Controls.Clear();
        _isEditing = true;

        var titleLabel = new Label
        {
            Text = "✨ Создание нового типа",
            Location = new Point(5, 5),
            Size = new Size(470, 30),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.Blue
        };
        _editorPanel.Controls.Add(titleLabel);

        int y = 45;
        int labelWidth = 120;
        int controlWidth = 330;
        int rowHeight = 35;

        var defaultColor = Color.FromArgb(200, 230, 230, 230);
        var defaultLineColor = Color.FromArgb(255, 180, 180, 180);

        var txtName = CreateTextBox("", 5, y, labelWidth, controlWidth);
        var lblName = CreateLabel("Название:*", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtCategory = CreateTextBox("General", 5, y, labelWidth, controlWidth);
        var lblCategory = CreateLabel("Категория:*", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtWall = CreateTextBox("WallSolid", 5, y, labelWidth, controlWidth);
        var lblWall = CreateLabel("Стена (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtFloor = CreateTextBox("Plating", 5, y, labelWidth, controlWidth);
        var lblFloor = CreateLabel("Пол (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtDoor = CreateTextBox("Airlock", 5, y, labelWidth, controlWidth);
        var lblDoor = CreateLabel("Дверь (proto):", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtGlassDoor = CreateTextBox("AirlockGlass", 5, y, labelWidth, controlWidth);
        var lblGlassDoor = CreateLabel("Стекл. дверь:", 5, y + 5, labelWidth);
        y += rowHeight;

        var txtFill = CreateTextBox($"{defaultColor.A},{defaultColor.R},{defaultColor.G},{defaultColor.B}", 
            5, y, labelWidth, controlWidth - 40);
        var lblFill = CreateLabel("Цвет заливки:", 5, y + 5, labelWidth);
        var btnPickFill = new Button
        {
            Text = "🎨",
            Location = new Point(controlWidth - 30, y),
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand
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

        var txtLine = CreateTextBox($"{defaultLineColor.A},{defaultLineColor.R},{defaultLineColor.G},{defaultLineColor.B}", 
            5, y, labelWidth, controlWidth - 40);
        var lblLine = CreateLabel("Цвет линии:", 5, y + 5, labelWidth);
        var btnPickLine = new Button
        {
            Text = "🎨",
            Location = new Point(controlWidth - 30, y),
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            Cursor = Cursors.Hand
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

        var txtPriority = CreateTextBox("0", 5, y, labelWidth, controlWidth);
        var lblPriority = CreateLabel("Приоритет:", 5, y + 5, labelWidth);
        y += rowHeight + 10;

        var btnCreate = new Button
        {
            Text = "✅ Создать тип",
            Location = new Point(5, y),
            Size = new Size(470, 40),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) },
            BackColor = Color.FromArgb(200, 240, 200),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
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
                _selectedType = null;
                ShowCategoryInfo(txtCategory.Text);
                MessageBox.Show($"Тип '{txtName.Text}' успешно создан!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        _editorPanel.Controls.Add(btnCreate);

        var btnCancel = new Button
        {
            Text = "❌ Отмена",
            Location = new Point(5, y + 45),
            Size = new Size(470, 30),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 150) },
            BackColor = Color.FromArgb(240, 220, 220),
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnCancel.Click += (s, e) =>
        {
            _selectedType = null;
            ShowCategoryInfo("Все категории");
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