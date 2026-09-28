// Forms/ToastNotification.cs
//
// Универсальное всплывающее окно уведомлений: появляется в правом нижнем углу
// (левее панели инструментов), плавно проявляется, держится заданное время и
// плавно исчезает. Используется для коротких информационных сообщений юзеру.

using System.Drawing.Drawing2D;

namespace MapperIce.Forms;

public static class ToastNotification
{
    /// <summary>
    /// Ширина правой панели инструментов в пикселях. Уведомление рисуется левее
    /// неё, чтобы не перекрывать панель.
    /// </summary>
    internal const int ToolPanelWidth = 200;

    /// <summary>
    /// Показать всплывающее уведомление в правом нижнем углу окна владельца.
    /// </summary>
    /// <param name="owner">Форма, относительно которой позиционируется уведомление (главное окно).</param>
    /// <param name="message">Текст уведомления.</param>
    /// <param name="title">Заголовок уведомления (необязательно).</param>
    /// <param name="durationMs">Сколько миллисекунд уведомление видно (после плавного появления).</param>
    public static void Show(Form owner, string message, string? title = null, int durationMs = 3500)
    {
        var toast = new ToastForm(owner, message, title, durationMs);
        toast.Show();
    }
}

internal class ToastForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly int _holdMs;
    private int _anim;

    // Стадии анимации: 0 — появление (0..FADE_IN_STEPS), затем ожидание,
    // затем исчезание (до 0), после чего форма закрывается.
    private const int FadeInSteps = 8;   // шагов на появление
    private const int FadeOutSteps = 12; // шагов на исчезание
    private const int TickMs = 25;
    private const int CornerRadius = 14;

    internal ToastForm(Form owner, string message, string? title, int holdMs)
    {
        _holdMs = Math.Max(0, holdMs);
        _anim = 0;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Opacity = 0;

        var lblTitle = new Label
        {
            AutoSize = false,
            Text = title ?? "",
            ForeColor = Color.FromArgb(230, 230, 230),
            Font = new Font("Arial", 11, FontStyle.Bold),
            Location = new Point(16, 10),
            Size = new Size(0, 22),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblMsg = new Label
        {
            Text = message,
            ForeColor = Color.FromArgb(215, 215, 215),
            Font = new Font("Arial", 10),
            Location = new Point(16, title == null ? 10 : 34),
            BackColor = Color.Transparent
        };

        // Пересчитываем размер по тексту
        Size msgSize = TextRenderer.MeasureText(message, lblMsg.Font, new Size(320, 0), TextFormatFlags.WordBreak);
        int textWidth = Math.Min(msgSize.Width, 320) + 32;
        lblMsg.Size = new Size(textWidth - 32, msgSize.Height);

        int width = textWidth;
        int height = (title == null ? 18 : 40) + msgSize.Height;

        if (title != null)
            lblTitle.Width = width - 32;

        Controls.Add(lblMsg);
        if (title != null)
            Controls.Add(lblTitle);

        ClientSize = new Size(width, height);
        BackColor = Color.FromArgb(38, 38, 42);
        Region = CreateRoundedRegion(width, height, CornerRadius);

        // Позиция: правый нижний угол клиентской области владельца,
        // левее панели инструментов (ширина ToolPanelWidth) + отступы
        if (owner != null && !owner.IsDisposed)
        {
            var screenRect = owner.RectangleToScreen(owner.ClientRectangle);
            const int margin = 14;
            int x = screenRect.Right - ToastNotification.ToolPanelWidth - margin - width;
            int y = screenRect.Bottom - margin - height;
            Location = new Point(x, y);
        }

        _timer = new System.Windows.Forms.Timer { Interval = TickMs };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        // Появление
        if (_anim < FadeInSteps)
        {
            _anim++;
            Opacity = _anim / (double)FadeInSteps;
            return;
        }

        // Ожидание
        if (_anim < FadeInSteps + _holdMs / TickMs)
        {
            _anim++;
            return;
        }

        // Исчезание
        int fadeProgress = _anim - FadeInSteps - _holdMs / TickMs;
        if (fadeProgress < FadeOutSteps)
        {
            _anim++;
            Opacity = 1.0 - fadeProgress / (double)FadeOutSteps;
            return;
        }

        _timer.Stop();
        Close();
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d, 0, d, d, 270, 90);
        path.AddArc(width - d, height - d, d, d, 0, 90);
        path.AddArc(0, height - d, d, d, 90, 90);
        path.CloseFigure();
        var region = new Region(path);
        path.Dispose();
        return region;
    }

    protected override bool ShowWithoutActivation => true;
}