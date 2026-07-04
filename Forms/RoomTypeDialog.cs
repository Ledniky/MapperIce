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
        
        Text = "Выбор и настройка типов комнат";
        Size = new Size(700, 550);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 300,
            Orientation = Orientation.Vertical
        };

        // ===== ЛЕВАЯ ПАНЕЛЬ: ДЕРЕВО =====
        var leftPanel = new Panel { Dock = DockStyle.Fill };
        
        var btnAddCategory = new Button
        {
            Text = "📁 Добавить категорию",
            Dock = DockStyle.Top,
            Height = 30,
            FlatStyle = FlatStyle.Flat
        };
        btnAddCategory.Click += (s, e) => AddCategory();
        leftPanel.Controls.Add(btnAddCategory);

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            Indent = 20,
            ShowRootLines = true,
            ShowPlusMinus = true
        };
        _treeView.AfterSelect += OnTreeViewSelect;
        _treeView.NodeMouseDoubleClick += OnTreeViewDoubleClick;
        _treeView.KeyDown += OnTreeViewKeyDown;
        leftPanel.Controls.Add(_treeView);

        splitContainer.Panel1.Controls.Add(leftPanel);

        // ===== ПРАВАЯ ПАНЕЛЬ: РЕДАКТОР =====
        _editorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true
        };
        splitContainer.Panel2.Controls.Add(_editorPanel);

        Controls.Add(splitContainer);

        // Кнопки внизу
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var btnApply = new Button
        {
            Text = "Применить",
            Location = new Point(bottomPanel.Width - 190, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnApply.Click += (s, e) => ApplyType();
        bottomPanel.Controls.Add(btnApply);

        var btnClose = new Button
        {
            Text = "Закрыть",
            Location = new Point(bottomPanel.Width - 100, 10),
            Width = 80,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnClose.Click += (s, e) => Close();
        bottomPanel.Controls.Add(btnClose);

        Controls.Add(bottomPanel);

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
        
        // Если есть кастомные категории без типов - тоже показываем
        // (уже добавлены через GetCategories)
    }

// В RoomTypeDialog.cs - измените OnTreeViewSelect:

private void OnTreeViewSelect(object? sender, TreeViewEventArgs e)
{
    if (e.Node?.Tag is CategoryNode node)
    {
        if (!node.IsCategory && node.RoomType != null)
        {
            _selectedType = node.RoomType;
            
            // СРАЗУ ПРИМЕНЯЕМ ТИП ПРИ ВЫБОРЕ
            _manager.SelectType(_selectedType.Name);
            
            // Обновляем подсветку в дереве
            UpdateTreeViewSelection();
            
            // Показываем редактор
            ShowEditor(node.RoomType);
            
            // Обновляем информацию в главном окне
            OnTypeSelected?.Invoke(_selectedType.Name);
        }
        else
        {
            _selectedType = null;
            ShowCategoryInfo(node.Name);
        }
    }
}

// Добавьте событие
public event Action<string>? OnTypeSelected;

// Добавьте метод обновления подсветки
private void UpdateTreeViewSelection()
{
    string selectedTypeName = _manager.SelectedType;
    
    foreach (TreeNode categoryNode in _treeView.Nodes)
    {
        foreach (TreeNode typeNode in categoryNode.Nodes)
        {
            if (typeNode.Tag is CategoryNode node && node.RoomType != null)
            {
                if (node.RoomType.Name == selectedTypeName)
                {
                    typeNode.BackColor = Color.LightBlue;
                }
                else
                {
                    typeNode.BackColor = Color.White;
                }
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

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            RowCount = 10,
            ColumnCount = 2,
            AutoSize = false
        };

        var txtName = new TextBox { Dock = DockStyle.Fill, Text = type.Name };
        var txtCategory = new TextBox { Dock = DockStyle.Fill, Text = type.Category };
        var txtWall = new TextBox { Dock = DockStyle.Fill, Text = type.WallProto };
        var txtFloor = new TextBox { Dock = DockStyle.Fill, Text = type.FloorProto };
        var txtDoor = new TextBox { Dock = DockStyle.Fill, Text = type.DoorProto };
        var txtGlassDoor = new TextBox { Dock = DockStyle.Fill, Text = type.GlassDoorProto };
        var txtFill = new TextBox { Dock = DockStyle.Fill, Text = $"{type.FillColor.A},{type.FillColor.R},{type.FillColor.G},{type.FillColor.B}" };
        var txtLine = new TextBox { Dock = DockStyle.Fill, Text = $"{type.LineColor.A},{type.LineColor.R},{type.LineColor.G},{type.LineColor.B}" };
        var txtPriority = new TextBox { Dock = DockStyle.Fill, Text = _manager.GetPriorityForType(type.Name).ToString() };

        var btnPickColor = new Button { Text = "🎨", Width = 30, Height = 30 };
        var btnPickLine = new Button { Text = "🎨", Width = 30, Height = 30 };

        AddRow(table, "Название:", txtName, 0);
        AddRow(table, "Категория:", txtCategory, 1);
        AddRow(table, "Стена (proto):", txtWall, 2);
        AddRow(table, "Пол (proto):", txtFloor, 3);
        AddRow(table, "Дверь (proto):", txtDoor, 4);
        AddRow(table, "Стеклянная дверь:", txtGlassDoor, 5);
        AddRowWithButton(table, "Цвет заливки:", txtFill, btnPickColor, 6);
        AddRowWithButton(table, "Цвет линии:", txtLine, btnPickLine, 7);
        AddRow(table, "Приоритет:", txtPriority, 8);

        var btnSave = new Button { Text = "💾 Сохранить изменения", Dock = DockStyle.Fill, Height = 35 };
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
                    // Для встроенных типов создаём кастомную копию
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
        table.Controls.Add(btnSave, 0, 9);
        table.SetColumnSpan(btnSave, 2);

        _editorPanel.Controls.Add(table);
    }

    private void ShowCategoryInfo(string categoryName)
    {
        _editorPanel.Controls.Clear();
        _isEditing = false;

        var label = new Label
        {
            Text = $"📁 Категория: {categoryName}\n\nВыберите тип для редактирования\nили создайте новый тип в этой категории.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        _editorPanel.Controls.Add(label);
    }

    private void AddRow(TableLayoutPanel table, string labelText, Control control, int row)
    {
        table.Controls.Add(new Label { Text = labelText, AutoSize = true, Font = new Font("Segoe UI", 9) }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void AddRowWithButton(TableLayoutPanel table, string labelText, Control control, Button button, int row)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        control.Dock = DockStyle.Left;
        control.Width = panel.Width - 35;
        button.Dock = DockStyle.Right;
        button.Click += (s, e) =>
        {
            var txt = control as TextBox;
            if (txt != null)
            {
                var parts = txt.Text.Split(',').Select(int.Parse).ToArray();
                using var dialog = new ColorDialog();
                dialog.Color = Color.FromArgb(parts[0], parts[1], parts[2], parts[3]);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txt.Text = $"{dialog.Color.A},{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
                }
            }
        };
        panel.Controls.Add(control);
        panel.Controls.Add(button);
        table.Controls.Add(new Label { Text = labelText, AutoSize = true, Font = new Font("Segoe UI", 9) }, 0, row);
        table.Controls.Add(panel, 1, row);
    }

    private void AddCategory()
    {
        var input = InputBox.Show("Введите название категории:", "Новая категория");
        if (!string.IsNullOrEmpty(input))
        {
            // Создаём пустую категорию с одним типом-заглушкой
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
        // Просто применяем тип без сообщения
    }
}

    private class CategoryNode
    {
        public string Name { get; set; } = "";
        public bool IsCategory { get; set; } = false;
        public RoomType? RoomType { get; set; }
    }
}

// Простой диалог ввода
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