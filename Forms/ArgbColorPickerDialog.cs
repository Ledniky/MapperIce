// Forms/ArgbColorPickerDialog.cs
namespace MapperIce.Forms;

/// <summary>
/// Стандартный System.Windows.Forms.ColorDialog не умеет редактировать альфа-канал —
/// какой бы Color ни передать на вход, после любого реального выбора цвета пользователем
/// он всегда возвращает A=255. А декали (и вообще многие цвета в проекте) хранятся и
/// экспортируются как "#RRGGBBAA" с реальной прозрачностью (см. color.yml:
/// "#FFFFFF66" — альфа 0x66, не 0xFF). Из-за этого любая правка цвета через голый
/// ColorDialog молча "сбрасывала" прозрачность на полную непрозрачность.
///
/// Этот диалог — единая точка выбора ЛЮБОГО цвета в программе: кнопка "Цвет (RGB)..."
/// открывает системный ColorDialog только для RGB (текущая альфа при этом сохраняется
/// и не трогается), плюс отдельный слайдер + числовое поле альфы (0–255), плюс
/// редактируемое hex-поле "#RRGGBBAA" — можно вписать точное значение вручную. Все три
/// способа синхронизированы между собой и с превью-квадратом.
/// </summary>
public class ArgbColorPickerDialog : Form
{
    public Color SelectedColor { get; private set; }

    private readonly Panel _preview;
    private readonly TrackBar _alphaSlider;
    private readonly NumericUpDown _alphaNumeric;
    private readonly TextBox _hexBox;
    private Color _current;
    private bool _suppressSync;

    public ArgbColorPickerDialog(Color initial)
    {
        _current = initial;
        SelectedColor = initial;

        Text = "Выбор цвета";
        Size = new Size(300, 225);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        _preview = new Panel
        {
            Location = new Point(25, 10),
            Size = new Size(30, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _current
        };
        Controls.Add(_preview);

        var btnPickRgb = new Button
        {
            Text = "Цвет (RGB)...",
            Location = new Point(80, 10),
            Width = 190,
            Height = 26,
            FlatStyle = FlatStyle.Flat
        };
        btnPickRgb.Click += (s, e) =>
        {
            // Системному диалогу передаём непрозрачную версию текущего цвета — сам он
            // всё равно не умеет показывать/учитывать альфу. Свою альфу применяем поверх
            // выбранного RGB сами, ниже.
            using var dlg = new ColorDialog { Color = Color.FromArgb(255, _current.R, _current.G, _current.B), FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ApplyColor(Color.FromArgb(_current.A, dlg.Color.R, dlg.Color.G, dlg.Color.B));
            }
        };
        Controls.Add(btnPickRgb);

        Controls.Add(new Label
        {
            Text = "Прозрачность (альфа):",
            Location = new Point(10, 48),
            AutoSize = true,
            Font = new Font("Segoe UI", 8)
        });

        _alphaSlider = new TrackBar
        {
            Location = new Point(10, 66),
            Width = 210,
            Minimum = 0,
            Maximum = 255,
            TickFrequency = 32,
            Value = _current.A
        };
        _alphaSlider.ValueChanged += (s, e) =>
        {
            if (_suppressSync) return;
            ApplyColor(Color.FromArgb(_alphaSlider.Value, _current.R, _current.G, _current.B), skipSlider: true);
        };
        Controls.Add(_alphaSlider);

        _alphaNumeric = new NumericUpDown
        {
            Location = new Point(228, 68),
            Width = 50,
            Height = 15,
            Minimum = 0,
            Maximum = 255,
            Value = _current.A
        };
        _alphaNumeric.ValueChanged += (s, e) =>
        {
            if (_suppressSync) return;
            ApplyColor(Color.FromArgb((int)_alphaNumeric.Value, _current.R, _current.G, _current.B), skipNumeric: true);
        };
        Controls.Add(_alphaNumeric);

        Controls.Add(new Label
        {
            Text = "HEX (#RRGGBBAA):",
            Location = new Point(10, 116),
            AutoSize = true,
            Font = new Font("Segoe UI", 8)
        });
        _hexBox = new TextBox { Location = new Point(140, 112), Width = 138, Text = ToHex(_current) };
        void CommitHex()
        {
            if (TryParseHex(_hexBox.Text, out var parsed))
                ApplyColor(parsed, skipHex: true);
            else
                _hexBox.Text = ToHex(_current); // невалидный ввод — откатываем на текущее значение
        }
        _hexBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitHex(); } };
        _hexBox.Leave += (s, e) => CommitHex();
        Controls.Add(_hexBox);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(100, 150), Width = 85, Height = 28 };
        var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(193, 150), Width = 85, Height = 28 };
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }

    private void ApplyColor(Color color, bool skipSlider = false, bool skipNumeric = false, bool skipHex = false)
    {
        _current = color;
        SelectedColor = color;
        _preview.BackColor = color;

        _suppressSync = true;
        if (!skipSlider) _alphaSlider.Value = color.A;
        if (!skipNumeric) _alphaNumeric.Value = color.A;
        if (!skipHex) _hexBox.Text = ToHex(color);
        _suppressSync = false;
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Color.White;
        try
        {
            var h = hex.Trim().TrimStart('#');
            if (h.Length == 8)
            {
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                int a = Convert.ToInt32(h.Substring(6, 2), 16);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
            if (h.Length == 6)
            {
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                color = Color.FromArgb(255, r, g, b);
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>Удобный статический хелпер: модально показывает диалог, возвращает true
    /// и выбранный цвет (с альфой), если пользователь нажал OK.</summary>
    public static bool Pick(IWin32Window owner, Color initial, out Color result)
    {
        using var dlg = new ArgbColorPickerDialog(initial);
        if (dlg.ShowDialog(owner) == DialogResult.OK)
        {
            result = dlg.SelectedColor;
            return true;
        }
        result = initial;
        return false;
    }
}
