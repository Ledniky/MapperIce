// Forms/MainForm.LinkedEntitiesPanel.cs
//
// Панель «Привязанные сущности» — отдельная панель справа, которая открывается
// поверх панели инструментов (или подменяет её) по клику на кнопку
// «Привязанные сущности» в панели инструментов. В верхней части панели есть
// кнопка «Назад», закрывающая её. Сюда перенесён выпадающий список дверей,
// который раньше находился в панели инструментов (см. _doorToolCombo).

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    private Panel _linkedEntitiesPanel = null!;
    private Button _btnLinkedEntities = null!;

    // === ПАНЕЛЬ ПРИВЯЗАННЫХ СУЩНОСТЕЙ ===
    private void CreateLinkedEntitiesPanel()
    {
        _linkedEntitiesPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.None,
            Padding = new Padding(0),
            Visible = false
        };
        DragDropCursorHelper.SuppressForbiddenCursor(_linkedEntitiesPanel);

        var leftLine = new Panel
        {
            Dock = DockStyle.Left,
            Width = 1,
            BackColor = Color.Gray
        };
        _linkedEntitiesPanel.Controls.Add(leftLine);

        int leftMargin = 2;
        int rightMargin = 2;
        int y = leftMargin;
        int contentWidth = 200 - leftMargin - rightMargin;

        // Кнопка «Назад» — закрывает панель и возвращает к панели инструментов
        var backButton = new Button
        {
            Text = "← Назад",
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 35,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Arial", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        backButton.Click += (s, e) => HideLinkedEntitiesPanel();
        _linkedEntitiesPanel.Controls.Add(backButton);
        y += 35 + 2;

        var title = new Label
        {
            Text = "Привязанные сущности",
            Font = new Font("Arial", 10, FontStyle.Bold),
            Location = new Point(leftMargin, y),
            Width = contentWidth,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(220, 220, 220)
        };
        _linkedEntitiesPanel.Controls.Add(title);
        y += 25 + 2;

        // === ЗАГОЛОВОК СЕКЦИИ ===
        var doorsLabel = new Label
        {
            Text = "Двери/окна:",
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 8, FontStyle.Bold),
            ForeColor = Color.DarkGray
        };
        _linkedEntitiesPanel.Controls.Add(doorsLabel);
        y += 20 + 2;

        // === ВЫПАДАЮЩИЙ СПИСОК ДВЕРЕЙ (перенесён из панели инструментов) ===
        _doorToolCombo = new ComboBox
        {
            Location = new Point(leftMargin + 2, y),
            Width = contentWidth - 4,
            Height = 40,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 34,
            DropDownHeight = 160,
            Font = new Font("Arial", 10)
        };
        foreach (var label in _doorToolMap.Keys)
            _doorToolCombo.Items.Add(label);
        if (_doorToolCombo.Items.Count > 0)
            _doorToolCombo.SelectedIndex = 0;

        // Рисуем реальный спрайт прототипа (если есть и уже загружен через
        // GetCachedProtoIcon), иначе — emoji-заглушку из _doorToolIcons.Fallback.
        // Тот же приём, что и в ProtoList_DrawItem: иконка слева, текст справа от неё.
        _doorToolCombo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                string label = _doorToolCombo.Items[e.Index].ToString() ?? "";
                const int iconSize = 28;
                const int padding = 4;
                int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                int textX = e.Bounds.Left + padding;

                Image? icon = null;
                string fallback = "";
                if (_doorToolIcons.TryGetValue(label, out var info))
                {
                    fallback = info.Fallback;
                    icon = info.ProtoId switch
                    {
                        "Airlock" => _doorIconAirlock,
                        "AirlockGlass" => _doorIconAirlockGlass,
                        _ => null
                    };
                }
                else if (label.StartsWith("🪟 "))
                {
                    fallback = "🪟";
                    _windowIconCache.TryGetValue(label, out icon);
                }

                if (icon != null)
                {
                    int drawY = e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2;
                    e.Graphics.DrawImage(icon, e.Bounds.Left + padding, drawY, icon.Width, icon.Height);
                    textX = e.Bounds.Left + padding + iconSize + padding;
                }
                else if (!string.IsNullOrEmpty(fallback))
                {
                    TextRenderer.DrawText(e.Graphics, fallback, new Font("Segoe UI", 14),
                        new Rectangle(e.Bounds.Left + padding, iconY, iconSize, iconSize),
                        e.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    textX = e.Bounds.Left + padding + iconSize + padding;
                }

                TextRenderer.DrawText(e.Graphics, label, _doorToolCombo.Font,
                    new Rectangle(textX, e.Bounds.Top, e.Bounds.Width - (textX - e.Bounds.Left), e.Bounds.Height),
                    _doorToolCombo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            e.DrawFocusRectangle();
        };

        _doorToolCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_doorToolCombo.SelectedItem is string key)
            {
                // Проверяем, является ли пункт прототипом окна (начинается с 🪟)
                if (key.StartsWith("🪟 "))
                {
                    // Берём точный ID напрямую из кэша label->id (заполняется в
                    // UpdateWindowCombo) вместо того чтобы угадывать его обратным
                    // Replace/EndsWith по тексту пункта — EndsWith цеплял любой ID,
                    // заканчивающийся на то же слово (например, AirlockAssemblyMining
                    // вместо настоящего окна "Mining")
                    if (_windowProtoIdCache.TryGetValue(key, out var foundId))
                    {
                        System.Diagnostics.Debug.WriteLine($"[WindowSelect] key='{key}' found={foundId != null} id='{foundId}'");
                        _selectedWindowProto = foundId;
                        _toolManager.SetTool(ToolManager.Tool.ReplaceWallWithWindow);
                        return;
                    }
                }

                if (_doorToolMap.TryGetValue(key, out var tool))
                {
                    _toolManager.SetTool(tool);
                }
            }
        };
        _linkedEntitiesPanel.Controls.Add(_doorToolCombo);
        y += 40 + 2;
    }

    private void ShowLinkedEntitiesPanel()
    {
        // Панель привязанных сущностей открывается поверх панели инструментов
        bool alreadyOpen = _linkedEntitiesPanel.Visible;
        HideLinkedEntitiesPanel();

        if (!alreadyOpen)
        {
            // Поднимаем панель поверх инструментов за счёт BringToFront
            if (!Controls.Contains(_linkedEntitiesPanel))
                Controls.Add(_linkedEntitiesPanel);
            _linkedEntitiesPanel.BringToFront();
            _linkedEntitiesPanel.Visible = true;
        }
    }

    private void HideLinkedEntitiesPanel()
    {
        if (_linkedEntitiesPanel != null)
            _linkedEntitiesPanel.Visible = false;
    }
}