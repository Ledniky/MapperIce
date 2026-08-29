// Forms/MainForm.ToolPanel.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

    // === ПАНЕЛЬ ИНСТРУМЕНТОВ ===
    private void CreateToolPanel()
    {
        _toolPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.None,
            Padding = new Padding(0)
        };

        var leftLine = new Panel
        {
            Dock = DockStyle.Left,
            Width = 1,
            BackColor = Color.Gray
        };
        _toolPanel.Controls.Add(leftLine);

        int leftMargin = 2;
        int rightMargin = 2;
        int y = leftMargin;
        int contentWidth = 200 - leftMargin - rightMargin;

        var title = new Label
        {
            Text = "Инструменты",
            Font = new Font("Arial", 14, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 35,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(220, 220, 220)
        };
        _toolPanel.Controls.Add(title);
        y += 35 + 2;

        _typeLabel = new Label
        {
            Text = $"Комната: {_roomTypeManager.SelectedType}, ур: {_roomTypeManager.GetPriorityForType(_roomTypeManager.SelectedType)}",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DarkGray,
            Font = new Font("Arial", 8)
        };
        _toolPanel.Controls.Add(_typeLabel);
        y += 25 + 2;

        // КОМНАТЫ — 3 кнопки в ряд (создать, вычесть, восстановить)
        var roomPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        _btnCreateRoom = new Button
        {
            Text = "➕",
            Location = new Point(0, 0),
            Width = (roomPanel.Width / 3) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14)
        };
        _btnCreateRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.CreateRoom);
        };
        roomPanel.Controls.Add(_btnCreateRoom);

        _btnSubtractRoom = new Button
        {
            Text = "✂️",
            Location = new Point((roomPanel.Width / 3) + 1, 0),
            Width = (roomPanel.Width / 3) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14)
        };
        _btnSubtractRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.SubtractRoom);
        };
        roomPanel.Controls.Add(_btnSubtractRoom);

        _btnRestoreRoom = new Button
        {
            Text = "🔨",
            Location = new Point((roomPanel.Width / 3) * 2 + 2, 0),
            Width = (roomPanel.Width / 3) - 2,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14)
        };
        _btnRestoreRoom.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.RestoreRoom);
        };
        roomPanel.Controls.Add(_btnRestoreRoom);

        _btnRoomSettings = new Button
        {
            Text = "⚙",
            Location = new Point(roomPanel.Width - 42, 0),
            Width = 40,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnRoomSettings.Click += (s, e) => ShowRoomTypeDialog();
        roomPanel.Controls.Add(_btnRoomSettings);

        roomPanel.Resize += (s, e) =>
        {
            int bw = roomPanel.Width / 3;
            _btnCreateRoom.Width = bw - 1;
            _btnSubtractRoom.Location = new Point(bw + 1, 0);
            _btnSubtractRoom.Width = bw - 1;
            _btnRestoreRoom.Location = new Point(bw * 2 + 2, 0);
            _btnRestoreRoom.Width = bw - 2;
            _btnRoomSettings.Location = new Point(roomPanel.Width - 42, 0);
        };

        _toolPanel.Controls.Add(roomPanel);
        y += 40 + 2;

        // ДВЕРИ
        var doorPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        _btnAirlock = new Button
        {
            Location = new Point(0, 0),
            Width = (doorPanel.Width / 2) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Airlock",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "",
            Padding = new Padding(0)
        };
        _btnAirlock.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Door);
        };
        doorPanel.Controls.Add(_btnAirlock);

        _btnAirlockGlass = new Button
        {
            Location = new Point((doorPanel.Width / 2) + 1, 0),
            Width = (doorPanel.Width / 2) - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "AirlockGlass",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "",
            Padding = new Padding(0)
        };
        _btnAirlockGlass.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.DoorGlass);
        };
        doorPanel.Controls.Add(_btnAirlockGlass);

        doorPanel.Resize += (s, e) =>
        {
            int halfWidth = doorPanel.Width / 2;
            _btnAirlock.Width = halfWidth - 1;
            _btnAirlockGlass.Location = new Point(halfWidth + 1, 0);
            _btnAirlockGlass.Width = halfWidth - 1;
        };

        _toolPanel.Controls.Add(doorPanel);
        y += 40 + 2;

        // ТРУБЫ
        var pipeLabel = new Label
        {
            Text = "Трубы:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(pipeLabel);
        y += 20 + 2;

        var pipePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int buttonWidth = pipePanel.Width / 4;

        _btnPipeDistra = new Button
        {
            Location = new Point(0, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Distra",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "D",
            Padding = new Padding(0)
        };
        _btnPipeDistra.Click += (s, e) =>
        {
            _currentPipeLayer = "Distra";
            _toolManager.SetTool(ToolManager.Tool.PipeDistra);
        };
        pipePanel.Controls.Add(_btnPipeDistra);

        _btnPipeNormal = new Button
        {
            Location = new Point(buttonWidth, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Normal",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "N",
            Padding = new Padding(0)
        };
        _btnPipeNormal.Click += (s, e) =>
        {
            _currentPipeLayer = "Normal";
            _toolManager.SetTool(ToolManager.Tool.PipeNormal);
        };
        pipePanel.Controls.Add(_btnPipeNormal);

        _btnPipeWaste = new Button
        {
            Location = new Point(buttonWidth * 2, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "Waste",
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = "W",
            Padding = new Padding(0)
        };
        _btnPipeWaste.Click += (s, e) =>
        {
            _currentPipeLayer = "Waste";
            _toolManager.SetTool(ToolManager.Tool.PipeWaste);
        };
        pipePanel.Controls.Add(_btnPipeWaste);

        _btnPipeSettings = new Button
        {
            Text = "⚙",
            Location = new Point(buttonWidth * 3, 0),
            Width = buttonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnPipeSettings.Click += (s, e) => ShowPipeSettingsDialog();
        pipePanel.Controls.Add(_btnPipeSettings);

        pipePanel.Resize += (s, e) =>
        {
            int bw = pipePanel.Width / 4;
            _btnPipeDistra.Width = bw - 1;
            _btnPipeNormal.Location = new Point(bw, 0);
            _btnPipeNormal.Width = bw - 1;
            _btnPipeWaste.Location = new Point(bw * 2, 0);
            _btnPipeWaste.Width = bw - 1;
            _btnPipeSettings.Location = new Point(bw * 3, 0);
            _btnPipeSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(pipePanel);
        y += 40 + 2;

        // === СИГНАЛИЗАЦИЯ ===
        var alarmLabel = new Label
        {
            Text = "Сигнализация:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(alarmLabel);
        y += 20 + 2;

        var alarmPanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int alarmButtonWidth = alarmPanel.Width / 3;

        _btnAirAlarm = new Button
        {
            Location = new Point(0, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔊",
            Font = new Font("Segoe UI", 14)
        };
        _btnAirAlarm.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.AirAlarm);
        };
        alarmPanel.Controls.Add(_btnAirAlarm);

        _btnFireAlarm = new Button
        {
            Location = new Point(alarmButtonWidth, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "🔥",
            Font = new Font("Segoe UI", 14)
        };
        _btnFireAlarm.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.FireAlarm);
        };
        alarmPanel.Controls.Add(_btnFireAlarm);

        _btnAlarmSettings = new Button
        {
            Text = "⚙",
            Location = new Point(alarmButtonWidth * 2, 0),
            Width = alarmButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnAlarmSettings.Click += (s, e) => ShowAlarmSettingsDialog();
        alarmPanel.Controls.Add(_btnAlarmSettings);

        alarmPanel.Resize += (s, e) =>
        {
            int bw = alarmPanel.Width / 3;
            _btnAirAlarm.Width = bw - 1;
            _btnFireAlarm.Location = new Point(bw, 0);
            _btnFireAlarm.Width = bw - 1;
            _btnAlarmSettings.Location = new Point(bw * 2, 0);
            _btnAlarmSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(alarmPanel);
        y += 40 + 2;




        // === ПЕРЕМЕЩЕНИЕ ===
        var moveLabel = new Label
        {
            Text = "Перемещение:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(moveLabel);
        y += 20 + 2;

        var movePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        _btnMove = new Button
        {
            Text = "✥ Переместить",
            Location = new Point(0, 0),
            Width = movePanel.Width - 42,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnMove.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Move);
        };
        movePanel.Controls.Add(_btnMove);

        _btnMoveSettings = new Button
        {
            Text = "⚙",
            Location = new Point(movePanel.Width - 40, 0),
            Width = 40,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12)
        };
        _btnMoveSettings.Click += (s, e) => ShowMoveSettingsDialog();
        movePanel.Controls.Add(_btnMoveSettings);

        movePanel.Resize += (s, e) =>
        {
            _btnMove.Width = movePanel.Width - 42;
            _btnMoveSettings.Location = new Point(movePanel.Width - 40, 0);
        };

        _toolPanel.Controls.Add(movePanel);
        y += 40 + 2;


        var decalRuleLabel = new Label
        {
            Text = "Decal Rule:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(decalRuleLabel);
        y += 20 + 2;

        _btnDecalRule = new Button
        {
            Text = "🧱 Узор по периметру",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnDecalRule.Click += (s, e) => { _toolManager.SetTool(ToolManager.Tool.DecalRule); };
        _toolPanel.Controls.Add(_btnDecalRule);
        y += 40 + 2;

        // Отдельная кнопка — не инструмент канвы, а обычный диалог, работающий не с
        // конкретными установленными комнатами, а с абстрактными RoomType-классами
        _btnDecalInheritance = new Button
        {
            Text = "🌳 Наследование декалей",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Arial", 9, FontStyle.Bold)
        };
        _btnDecalInheritance.Click += (s, e) =>
                {
                    // Симметрично с диалогом конкретной комнаты (_decalRuleForm): одно окно,
                    // повторный клик по кнопке при уже открытом окне закрывает его, а не
                    // плодит новые копии. Плюс сбрасываем активный инструмент канвы — иначе
                    // "Узор по периметру" оставался включённым в фоне, и случайный клик по
                    // карте после закрытия окна наследования неожиданно открывал диалог
                    // конкретной комнаты
                    if (_decalInheritanceForm != null && !_decalInheritanceForm.IsDisposed)
                    {
                        _decalInheritanceForm.Close();
                        return;
                    }

                    _toolManager.ResetTool();

                    _decalInheritanceForm = new DecalInheritanceDialog(_decalInheritanceManager, _decalPackManager, _indexer);
                    _btnDecalInheritance.BackColor = Color.LightBlue;
                    _decalInheritanceForm.FormClosed += (fs, fe) =>
                    {
                        _decalInheritanceForm = null;
                        _btnDecalInheritance.BackColor = Color.White;
                    };
                    _decalInheritanceForm.Show(this);
                };
        _toolPanel.Controls.Add(_btnDecalInheritance);
        y += 40 + 2;







        // === УДАЛЕНИЕ ===
        var deleteLabel = new Label
        {
            Text = "Удаление:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _toolPanel.Controls.Add(deleteLabel);
        y += 20 + 2;

        var deletePanel = new Panel
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            BackColor = Color.Transparent
        };

        int deleteButtonWidth = deletePanel.Width / 3;

        _btnDelete = new Button
        {
            Text = "🗑",
            Location = new Point(0, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDelete.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.Delete);
        };
        deletePanel.Controls.Add(_btnDelete);

        _btnDeleteArea = new Button
        {
            Text = "🧹",
            Location = new Point(deleteButtonWidth, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDeleteArea.Click += (s, e) =>
        {
            _toolManager.SetTool(ToolManager.Tool.DeleteArea);
        };
        deletePanel.Controls.Add(_btnDeleteArea);

        _btnDeleteSettings = new Button
        {
            Text = "⚙",
            Location = new Point(deleteButtonWidth * 2, 0),
            Width = deleteButtonWidth - 1,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 12),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnDeleteSettings.Click += (s, e) => ShowDeleteSettingsDialog();
        deletePanel.Controls.Add(_btnDeleteSettings);

        deletePanel.Resize += (s, e) =>
        {
            int bw = deletePanel.Width / 3;
            _btnDelete.Width = bw - 1;
            _btnDeleteArea.Location = new Point(bw, 0);
            _btnDeleteArea.Width = bw - 1;
            _btnDeleteSettings.Location = new Point(bw * 2, 0);
            _btnDeleteSettings.Width = bw - 1;
        };

        _toolPanel.Controls.Add(deletePanel);
        y += 40 + 2;

        _toolPanel.Controls.Add(new Label
        {
            Text = "Повторное нажатие\nсбрасывает инструмент",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Arial", 9),
            BackColor = Color.FromArgb(230, 230, 230)
        });

        Controls.Add(_toolPanel);
    }


    private Button CreateButton(string text, EventHandler click)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 5, 10)
        };
    }


    private void AddRow(TableLayoutPanel table, string labelText, Control control, int row)
    {
        table.Controls.Add(new Label { Text = labelText, AutoSize = true }, 0, row);
        table.Controls.Add(control, 1, row);
    }


    private void UpdateTreeView(TreeView treeView)
    {
        treeView.Nodes.Clear();
        foreach (var category in _roomTypeManager.GetCategories().OrderBy(c => c.Key))
        {
            var node = new TreeNode(category.Key);
            foreach (var type in category.Value.OrderBy(t => t.Name))
            {
                node.Nodes.Add(new TreeNode(type.Name)
                {
                    Tag = type,
                    ForeColor = type.IsCustom ? Color.Blue : Color.Black
                });
            }
            treeView.Nodes.Add(node);
        }
    }
}
