// Forms/DragGhostForm.cs
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace MapperIce.Forms;

/// <summary>
/// Floating-"призрак" перетаскиваемого прототипа. Содержимое (иконка) рисуется
/// РОВНО ОДИН РАЗ через UpdateLayeredWindow (честная поканальная альфа — тот же
/// механизм, которым системный проводник рисует drag-image), а перемещение идёт
/// напрямую через SetWindowPos, минуя WinForms Location/layout/перерисовку.
/// Раньше окно двигалось через Location и использовало программный TransparencyKey —
/// оба пути пересчитывали/перекладывали содержимое на каждый кадр, отсюда и лаги.
/// </summary>
internal class DragGhostForm : Form
{
    private const int IconBoxSize = 32;
    private Size _size;

    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx; public int cy; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount;
        public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
        public uint biClrUsed; public uint biClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }

    private const int ULW_ALPHA = 0x2;
    private const byte AC_SRC_OVER = 0x0;
    private const byte AC_SRC_ALPHA = 0x1;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

        private Bitmap? _content;

    public DragGhostForm(Image? icon, string fallbackText)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        // НЕ используем "using" здесь — Load срабатывает позже, уже после выхода
        // из конструктора (когда окно реально создаётся хендлом), а не сразу.
        // Раньше битмап уничтожался в конце конструктора, и ApplyLayeredBitmap
        // получал уже Dispose'нутый объект — отсюда ArgumentException на .Width.
        // Освобождаем его сами в OnFormClosed, когда он больше не нужен.
        _content = BuildContentBitmap(icon, fallbackText);
        _size = _content.Size;
        Size = _size;

        // Отрисовываем поверхность один раз при первом показе — дальше MoveTo()
        // только двигает уже готовое изображение, без единой перерисовки.
        Load += (s, e) => ApplyLayeredBitmap(_content!);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _content?.Dispose();
        _content = null;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x80;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private static Bitmap BuildContentBitmap(Image? icon, string fallbackText)
    {
        if (icon != null)
        {
            var bmp = new Bitmap(IconBoxSize, IconBoxSize, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, 0, 0, IconBoxSize, IconBoxSize);
            return bmp;
        }

        using var font = new Font("Segoe UI", 8, FontStyle.Bold);
        SizeF textSize;
        using (var mb = new Bitmap(1, 1)) using (var mg = Graphics.FromImage(mb)) textSize = mg.MeasureString(fallbackText, font);
        int w = Math.Max(8, (int)Math.Ceiling(textSize.Width) + 8);
        int h = Math.Max(8, (int)Math.Ceiling(textSize.Height) + 6);

        var tbmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var tg = Graphics.FromImage(tbmp);
        using var bgBrush = new SolidBrush(Color.FromArgb(160, 40, 40, 40));
        tg.FillRectangle(bgBrush, 0, 0, w, h);
        using var textBrush = new SolidBrush(Color.White);
        tg.DrawString(fallbackText, font, textBrush, 4, 3);
        return tbmp;
    }

    /// <summary>
    /// Одноразовая передача изображения через UpdateLayeredWindow с честной
    /// поканальной альфой (ULW_ALPHA) — требует premultiplied alpha, конвертация
    /// сделана вручную построчно (безопасно, без unsafe-блоков).
    /// </summary>
    private void ApplyLayeredBitmap(Bitmap content)
    {
        int w = content.Width, h = content.Height;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h, // top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            }
        };

        IntPtr hBitmap = CreateDIBSection(screenDc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
        IntPtr oldBitmap = SelectObject(memDc, hBitmap);

        try
        {
            var rect = new Rectangle(0, 0, w, h);
            var srcData = content.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[srcData.Stride];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(srcData.Scan0 + y * srcData.Stride, row, 0, srcData.Stride);
                    for (int x = 0; x < w; x++)
                    {
                        int i = x * 4;
                        byte b = row[i], g = row[i + 1], r = row[i + 2], a = row[i + 3];
                        row[i] = (byte)(b * a / 255);
                        row[i + 1] = (byte)(g * a / 255);
                        row[i + 2] = (byte)(r * a / 255);
                        row[i + 3] = a;
                    }
                    Marshal.Copy(row, 0, bits + y * srcData.Stride, srcData.Stride);
                }
            }
            finally { content.UnlockBits(srcData); }

            var ptSrc = new POINT { X = 0, Y = 0 };
            var ptDst = new POINT { X = Left, Y = Top };
            var size = new SIZE { cx = w, cy = h };
            var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };

            UpdateLayeredWindow(Handle, IntPtr.Zero, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Двигает окно напрямую через SetWindowPos — не проходит через
    /// WinForms Location/layout и не перерисовывает содержимое.
    /// </summary>
    public void MoveTo(Point screenLocation)
    {
        int x = screenLocation.X - _size.Width / 2;
        int y = screenLocation.Y - _size.Height / 2;
        SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }
}