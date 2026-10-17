// Forms/ToastNotification.cs
//
// Универсальное всплывающее уведомление. Рисуется ВНУТРИ главного окна —
// это дочерний контрол, а не отдельное окно: он не вылезает за пределы формы,
// не висит поверх других программ и перемещается вместе с окном.
// Появляется в правом нижнем углу клиентской области, левее панели инструментов,
// держится заданное время и исчезает.

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
    /// Показать уведомление внутри окна владельца.
    /// </summary>
    /// <param name="owner">Главное окно, внутри которого показывается уведомление.</param>
    /// <param name="message">Текст уведомления.</param>
    /// <param name="title">Заголовок уведомления (необязательно).</param>
    /// <param name="durationMs">Сколько миллисекунд уведомление видно.</param>
    public static void Show(Form owner, string message, string? title = null, int durationMs = 3500)
    {
        if (owner == null || owner.IsDisposed || owner.Disposing) return;

        var toast = new ToastControl(message, title);
        owner.Controls.Add(toast);

        const int margin = 14;
        int x = Math.Max(0, owner.ClientSize.Width - ToolPanelWidth - margin - toast.Width);
        int y = Math.Max(0, owner.ClientSize.Height - margin - toast.Height);
        toast.Location = new Point(x, y);
        toast.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        toast.BringToFront();
        toast.Start(durationMs);
    }
}

/// <summary>
/// Дочерний контрол главного окна с сообщением. Скруглённый тёмный прямоугольник
/// с заголовком и текстом; сам удаляет себя по истечении времени.
/// </summary>
internal sealed class ToastControl : Control
{
    private const int CornerRadius = 14;
    private const int PaddingX = 16;
    private const int MaxTextWidth = 320;

    private static readonly Color BackColorValue = Color.FromArgb(38, 38, 42);
    private static readonly Color TitleColor = Color.FromArgb(230, 230, 230);
    private static readonly Color MessageColor = Color.FromArgb(215, 215, 215);

    private static readonly Font TitleFont = new("Arial", 11, FontStyle.Bold);
    private static readonly Font MessageFont = new("Arial", 10);

    private readonly string _message;
    private readonly string? _title;
    private System.Windows.Forms.Timer? _timer;

    internal ToastControl(string message, string? title)
    {
        _message = message;
        _title = title;

        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = BackColorValue;
        ForeColor = MessageColor;

        Size msgSize = TextRenderer.MeasureText(
            message, MessageFont, new Size(MaxTextWidth, 0), TextFormatFlags.WordBreak);

        int textWidth = Math.Min(msgSize.Width, MaxTextWidth);
        int width = textWidth + PaddingX * 2;
        int height = (title == null ? 18 : 40) + msgSize.Height;

        Size = new Size(width, height);
        Region = CreateRoundedRegion(width, height, CornerRadius);
    }

    internal void Start(int durationMs)
    {
        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(1, durationMs) };
        _timer.Tick += (s, e) =>
        {
            _timer?.Stop();
            Parent?.Controls.Remove(this);
            Dispose();
        };
        _timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        int textLeft = PaddingX;
        int textWidth = Width - PaddingX * 2;

        if (!string.IsNullOrEmpty(_title))
        {
            var titleRect = new Rectangle(textLeft, 8, textWidth, 22);
            TextRenderer.DrawText(e.Graphics, _title, TitleFont, titleRect, TitleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        int messageTop = string.IsNullOrEmpty(_title) ? 9 : 32;
        var messageRect = new Rectangle(textLeft, messageTop, textWidth, Height - messageTop - 6);
        TextRenderer.DrawText(e.Graphics, _message, MessageFont, messageRect, MessageColor,
            TextFormatFlags.Left | TextFormatFlags.WordBreak);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }
        base.Dispose(disposing);
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(width, height));
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d, 0, d, d, 270, 90);
        path.AddArc(width - d, height - d, d, d, 0, 90);
        path.AddArc(0, height - d, d, d, 90, 90);
        path.CloseFigure();
        var region = new Region(path);
        path.Dispose();
        return region;
    }
}
