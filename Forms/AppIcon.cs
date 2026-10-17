// Forms/AppIcon.cs
using System.Drawing;
using System.Windows.Forms;

namespace MapperIce.Forms;

/// <summary>
/// Иконка окна приложения. WinForms не берёт её из ресурса exe (ApplicationIcon):
/// по умолчанию форма показывает стандартную заглушку Windows Forms, поэтому
/// иконку задаём явно — из того же mapperice.ico, встроенного как EmbeddedResource.
/// </summary>
public static class AppIcon
{
    private const string ResourceName = "MapperIce.mapperice.ico";
    private static Icon? _icon;
    private static bool _loaded;

    public static Icon? Value
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                _icon = Load();
            }
            return _icon;
        }
    }

    // Ставит иконку окну (главное окно и диалоги, созданные после старта —
    // у диалогов иначе остаётся стандартная иконка WinForms).
    public static void ApplyToForm(Form form)
    {
        if (form is null || form.IsDisposed) return;
        var icon = Value;
        if (icon is null || ReferenceEquals(form.Icon, icon)) return;
        form.Icon = icon;
    }

    private static Icon? Load()
    {
        using var resource = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
        if (resource is not null)
        {
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return new Icon(new MemoryStream(buffer.ToArray()));
        }

        // Страховка, если ресурс не встроился: иконка самого exe.
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return null;
        try
        {
            return Icon.ExtractAssociatedIcon(exePath);
        }
        catch
        {
            return null;
        }
    }
}
