// Forms/AppTheme.cs
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapperIce.Forms;

/// <summary>
/// Централизованная светлая/тёмная тема приложения. Одна точка правды о цветах:
/// ApplyTheme() переключает AppTheme.Dark и прогоняет все открытые окна через
/// ApplyToForm. Тёмная тема оформляет и нерабочую область окна (заголовок, кнопки
/// «свернуть/закрыть») через DWM API, а также выпадающие списки и скроллбары.
/// Пользовательские цвета (палитра труб, превью цвета и т.п.) не затрагиваются.
/// </summary>
public static class AppTheme
{
    private static bool _dark;

    public static bool Dark
    {
        get => _dark;
        set => _dark = value;
    }

    // --- Палитра ---
    public static Color BackPanel => Dark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(240, 240, 240);
    public static Color BackMenu => Dark ? Color.FromArgb(55, 55, 58) : Color.FromArgb(240, 240, 240);
    public static Color BackGroup => Dark ? Color.FromArgb(38, 38, 41) : Color.FromArgb(230, 230, 230);
    public static Color BackField => Dark ? Color.FromArgb(28, 28, 31) : Color.White;
    public static Color BackSurface => Dark ? Color.FromArgb(33, 33, 36) : Color.White;
    public static Color Fore => Dark ? Color.FromArgb(218, 218, 222) : Color.Black;
    public static Color ForeMuted => Dark ? Color.FromArgb(150, 152, 158) : Color.Gray;
    public static Color HighlightBack => Dark ? Color.FromArgb(38, 79, 120) : SystemColors.Highlight;
    public static Color HighlightFore => Dark ? Color.White : SystemColors.HighlightText;

    // Подсветка пунктов меню: наведение, открытый пункт, рамка выпадающего списка, разделитель
    public static Color MenuHoverBack => Dark ? Color.FromArgb(75, 75, 80) : Color.FromArgb(213, 228, 242);
    public static Color MenuPressedBack => Dark ? Color.FromArgb(38, 79, 120) : Color.FromArgb(190, 215, 240);
    public static Color MenuEdge => Dark ? Color.FromArgb(95, 95, 100) : Color.FromArgb(160, 160, 160);
    public static Color MenuSeparator => Dark ? Color.FromArgb(85, 85, 90) : Color.FromArgb(200, 200, 200);

    // Кнопки-переключатели (вкл/выкл). В тёмной теме — приглушённые, тёмные,
    // чтобы «зелёные» кнопки не слепили и не выбивались из тёмного интерфейса.
    public static Color ToggleOnBack => Dark ? Color.FromArgb(45, 95, 55) : Color.LightGreen;
    public static Color ToggleOffBack => Dark ? BackGroup : Color.LightGray;

    // --- Win32 ---
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const uint WM_NCACTIVATE = 0x0086; // перерисовка нерабочей области при смене активности
    private const uint WM_NCPAINT = 0x0085;    // перерисовка рамки окна

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_FRAME = 0x0400;

    /// <summary>Красит нерабочую область окна (заголовок + системные кнопки) и сразу
    /// заставляет её перерисоваться. Главное — явно задать цвет заголовка через
    /// DWMWA_CAPTION_COLOR/DWMWA_TEXT_COLOR: когда окно само задаёт цвета, DWM
    /// перерисовывает заголовок сразу при вызове, а не только при смене активного окна.</summary>
    public static void ApplyTitleBar(Form form)
    {
        try
        {
            if (form == null || form.IsDisposed) return;
            IntPtr hwnd = form.Handle; // форсируем создание хэндла
            int useDark = Dark ? 1 : 0;
            // 20 — DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 2004+), 19 — более ранние сборки
            if (DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));

            // Явно задаём цвета заголовка и текста (COLORREF, Win10 10.0.19041+).
            // Один только атрибут тёмного режима заголовок перерисовывает неохотно;
            // явное задание цвета гарантирует немедленную перерисовку рамки.
            int caption = ColorTranslator.ToWin32(Dark ? Color.FromArgb(35, 35, 38) : Color.FromArgb(236, 236, 236));
            int inactiveCaption = ColorTranslator.ToWin32(Dark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(255, 255, 255));
            int text = ColorTranslator.ToWin32(Dark ? Color.FromArgb(235, 235, 240) : Color.FromArgb(12, 12, 12));
            // 35 = DWMWA_CAPTION_COLOR, 36 = DWMWA_TEXT_COLOR, 31 = DWMWA_INACTIVE_CAPTION_COLOR
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));
            DwmSetWindowAttribute(hwnd, 31, ref inactiveCaption, sizeof(int));

            // Надёжный триггер для DWM: кратковременно меняем размер окна на 1px.
            // Это заставляет DWM пересобрать рамку немедленно, даже если атрибуты выше
            // по какой-то причине не сработали (Win10, старые сборки, максимизированное окно).
            var r = form.Bounds;
            SetWindowPos(hwnd, IntPtr.Zero, r.X, r.Y, r.Width + 1, r.Height,
                SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            SetWindowPos(hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
                SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

            // Пересчёт рамки + перерисовка нерабочей области
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW);
        }
        catch
        {
            // DWM недоступен (старая ОС) — молча продолжаем
        }
    }

        // Таблица цветов меню: все значения берутся из палитры темы (читаются при каждой отрисовке)
    private sealed class MenuColorTable : ProfessionalColorTable
    {
        public MenuColorTable() { UseSystemColors = false; }

        public override Color MenuStripGradientBegin => BackMenu;
        public override Color MenuStripGradientEnd => BackMenu;
        public override Color ToolStripDropDownBackground => BackMenu;
        public override Color ImageMarginGradientBegin => BackMenu;
        public override Color ImageMarginGradientMiddle => BackMenu;
        public override Color ImageMarginGradientEnd => BackMenu;

        public override Color MenuItemSelected => MenuHoverBack;
        public override Color MenuItemSelectedGradientBegin => MenuHoverBack;
        public override Color MenuItemSelectedGradientEnd => MenuHoverBack;
        public override Color MenuItemPressedGradientBegin => MenuPressedBack;
        public override Color MenuItemPressedGradientMiddle => MenuPressedBack;
        public override Color MenuItemPressedGradientEnd => MenuPressedBack;
        public override Color MenuItemBorder => MenuHoverBack;

        public override Color MenuBorder => MenuEdge;
        public override Color ToolStripBorder => MenuEdge;
        public override Color SeparatorDark => MenuSeparator;
        public override Color SeparatorLight => MenuSeparator;
    }

    private static readonly ToolStripProfessionalRenderer _menuRenderer =
        new ToolStripProfessionalRenderer(new MenuColorTable()) { RoundedEdges = false };


    // --- Применение темы ---
    public static void ApplyToForm(Form form)
    {
        if (form == null || form.IsDisposed) return;

        if (ShouldThemeBack(form.BackColor))
            form.BackColor = BackPanel;
        form.ForeColor = Fore;

        ApplyTitleBar(form);

        ApplyToChildren(form.Controls);
        if (form.MainMenuStrip != null) ApplyMenu(form.MainMenuStrip);
        if (form.ContextMenuStrip != null) ApplyMenu(form.ContextMenuStrip);

        // Немедленно перерисовываем клиентскую область и дочерние контролы —
        // иначе меню/панели/поля показывали бы новую тему только при следующей
        // перерисовке окна (например, после закрытия диалога настроек).
        if (form.IsHandleCreated && form.Visible)
        {
            form.Invalidate(true);
            form.Update();
        }
    }

    private static void ApplyMenu(ToolStrip menu)
    {
        menu.BackColor = BackMenu;
        menu.ForeColor = Fore;
        menu.Renderer = _menuRenderer;
        foreach (ToolStripItem item in menu.Items)
            ApplyMenuItem(item);
    }

    private static void ApplyMenuItem(ToolStripItem item)
    {
        item.ForeColor = Fore;
        if (item is ToolStripMenuItem mi)
        {
            mi.BackColor = BackMenu;
            if (mi.DropDown != null)
            {
                mi.DropDown.BackColor = BackMenu;
                mi.DropDown.ForeColor = Fore;
                mi.DropDown.Renderer = _menuRenderer;
                foreach (ToolStripItem sub in mi.DropDown.Items)
                    ApplyMenuItem(sub);
            }
        }
    }

    private static void ApplyToChildren(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            ApplyToControl(c);
            if (c.HasChildren)
                ApplyToChildren(c.Controls);
        }
    }

    /// <summary>Окрашивает один контрол по типу, не трогая пользовательские цвета.</summary>
    public static void ApplyToControl(Control c)
    {
        switch (c)
        {
            case ComboBox cb:
                ApplyComboBox(cb);
                break;
            case Button btn:
                btn.ForeColor = Fore;
                if (IsToggleOnBack(btn.BackColor)) btn.BackColor = ToggleOnBack;
                else if (ShouldThemeBack(btn.BackColor)) btn.BackColor = BackGroup;
                break;
            case CheckedListBox:
            case ListBox:
            case ListView:
            case DataGridView:
            case TreeView:
                c.BackColor = BackField;
                c.ForeColor = Fore;
                DarkScrollBars(c);
                break;
            case TextBox:
            case RichTextBox:
            case MaskedTextBox:
            case MonthCalendar:
            case DateTimePicker:
                c.BackColor = BackField;
                c.ForeColor = Fore;
                DarkScrollBars(c);
                break;
            case NumericUpDown nud:
                nud.BackColor = BackField;
                nud.ForeColor = Fore;
                break;
            case TrackBar tb:
                tb.BackColor = BackField;
                tb.ForeColor = Fore;
                DarkScrollBars(tb);
                break;
            case MenuStrip ms:
                ApplyMenu(ms);
                break;
            case StatusStrip:
            case ToolStrip:
                c.BackColor = BackMenu;
                c.ForeColor = Fore;
                break;
            case LinkLabel:
            case TabPage:
                c.ForeColor = Fore;
                if (ShouldThemeBack(c.BackColor)) c.BackColor = BackGroup;
                break;
            case SplitContainer:
                c.BackColor = BackPanel;
                c.ForeColor = Fore;
                break;
            case Panel:
            case GroupBox:
                c.ForeColor = Fore;
                if (ShouldThemeBack(c.BackColor)) c.BackColor = BackPanel;
                break;
            case TabControl:
                c.BackColor = BackPanel;
                c.ForeColor = Fore;
                break;
            case Label:
            case CheckBox:
            case RadioButton:
                c.ForeColor = Fore;
                if (ShouldThemeBack(c.BackColor)) c.BackColor = BackPanel;
                break;
            default:
                c.ForeColor = Fore;
                break;
        }
    }

    private static void ApplyComboBox(ComboBox cb)
    {
        cb.BackColor = BackField;
        cb.ForeColor = Fore;

        // У "обычных" (не owner-draw) ComboBox выпадающий список рисует сама система —
        // он остаётся белым. Переводим на owner-draw с общим обработчиком, который
        // заливает пункты цветами темы. Кастомные owner-draw списки (иконки и т.п.)
        // не трогаем — у них уже есть свой DrawItem.
        if (cb.DrawMode == DrawMode.Normal)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            if (cb.ItemHeight <= 0) cb.ItemHeight = cb.Font.Height + 4;
            cb.DrawItem += GenericComboDrawItem;
        }
    }

    /// <summary>Общая отрисовка пунктов owner-draw ComboBox в цветах темы.</summary>
    private static void GenericComboDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cb) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        DrawItemBackground(e.Graphics, e.Bounds, selected);

        if (e.Index >= 0 && e.Index < cb.Items.Count)
        {
            string text = cb.GetItemText(cb.Items[e.Index]) ?? "";
            Color fore = selected ? HighlightFore : Fore;
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        e.DrawFocusRectangle();
    }

    /// <summary>Заливка фона пункта owner-draw списка (используется и кастомными DrawItem).</summary>
    public static void DrawItemBackground(Graphics g, Rectangle bounds, bool selected)
    {
        using var brush = new SolidBrush(selected ? HighlightBack : BackSurface);
        g.FillRectangle(brush, bounds);
    }

    /// <summary>Делает скроллбары контрола тёмными (Win10 1809+).</summary>
    private static void DarkScrollBars(Control c)
    {
        try
        {
            if (!c.IsHandleCreated) return;
            if (Dark)
                SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            else
                SetWindowTheme(c.Handle, "Explorer", null);
        }
        catch
        {
            // uxtheme недоступен — не критично
        }
    }

    // Все фоны, которые назначает САМА тема (в обеих палитрах). Нужны, чтобы при
    // переключении тёмная→светлая мы узнавали уже окрашенные нами контролы: после
    // включения тёмной темы панель становится (45,45,48) — это уже не «нейтральный»
    // цвет, и без этого списка светлая тема не возвращалась бы на панелях и окнах.
    private static readonly int[] _themeBackArgb =
    {
        Color.FromArgb(45, 45, 48).ToArgb(),   // тёмный BackPanel
        Color.FromArgb(55, 55, 58).ToArgb(),   // тёмный BackMenu
        Color.FromArgb(38, 38, 41).ToArgb(),   // тёмный BackGroup
        Color.FromArgb(28, 28, 31).ToArgb(),   // тёмный BackField
        Color.FromArgb(33, 33, 36).ToArgb(),   // тёмный BackSurface
    };

    /// <summary>Можно ли перекрасить фон контрола темой. Прозрачный фон НЕ трогаем
    /// (остаётся прозрачным), пользовательские цвета тоже не затираем — меняем только
    /// нейтральные (белый/светло-серый/системный) и уже назначенные самой темой.</summary>
    private static bool ShouldThemeBack(Color color)
    {
        if (color.IsEmpty) return true;
        if (color.A == 0) return false; // Transparent — сохраняем прозрачность

        // Белые и светло-серые оттенки, а также системный цвет окна/контрола
        if (color.R == color.G && color.G == color.B)
            return color.R >= 200;
        if (color.ToArgb() == SystemColors.Control.ToArgb()) return true;
        if (color.ToArgb() == SystemColors.Window.ToArgb()) return true;

        // Уже назначенный темой цвет — перекрашиваем в новую палитру
        return Array.IndexOf(_themeBackArgb, color.ToArgb()) >= 0;
    }

    // Цвета «зелёной» кнопки-переключателя (светлый LightGreen и наш тёмный) —
    // чтобы при переключении темы узнавать её и подменять на нужный вариант.
    private static readonly int[] _toggleOnArgb =
    {
        Color.LightGreen.ToArgb(),
        Color.FromArgb(45, 95, 55).ToArgb(),
    };

    private static bool IsToggleOnBack(Color color)
        => Array.IndexOf(_toggleOnArgb, color.ToArgb()) >= 0;
}