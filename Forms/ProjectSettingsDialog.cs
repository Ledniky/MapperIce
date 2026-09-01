// Forms/ProjectSettingsDialog.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

/// <summary>
/// Диалог настроек проекта: переключатель статичных/динамичных гридов.
/// </summary>
public class ProjectSettingsDialog : Form
{
    private readonly Grid _grid;
    private readonly PrototypeIndexer _indexer;
    private readonly DrawDepthManager _drawDepthManager;
    private readonly TileBuilder _tileBuilder;
    private readonly Dictionary<string, PipeSettings> _pipeLayers;
    private readonly Dictionary<string, AlarmSettings> _alarmSettings;
    private Action? _onApply;

    private ComboBox _cmbGridType;
    private Label _lblDescription;
    private Label _lblCurrent;
    private Label _lblPreview;

    public ProjectSettingsDialog(
        Grid grid,
        PrototypeIndexer indexer,
        DrawDepthManager drawDepthManager,
        TileBuilder tileBuilder,
        Dictionary<string, PipeSettings> pipeLayers,
        Dictionary<string, AlarmSettings> alarmSettings,
        Action onApply)
    {
        _grid = grid;
        _indexer = indexer;
        _drawDepthManager = drawDepthManager;
        _tileBuilder = tileBuilder;
        _pipeLayers = pipeLayers;
        _alarmSettings = alarmSettings;
        _onApply = onApply;

        Text = "Настройки проекта";
        Size = new Size(480, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Arial", 10);

        CreateUI();
    }

    private void CreateUI()
    {
        int y = 20;
        int left = 20;
        int contentWidth = 440;

        // Заголовок
        var title = new Label
        {
            Text = "Настройки грида",
            Font = new Font("Arial", 12, FontStyle.Bold),
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 25
        };
        Controls.Add(title);
        y += 35;

        // Текущее состояние
        _lblCurrent = new Label
        {
            Text = $"Текущий режим: {(_grid.IsStaticGrid ? "Статичный (станция)" : "Динамичный (шаттл)")}",
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 20,
            Font = new Font("Arial", 10)
        };
        Controls.Add(_lblCurrent);
        y += 28;

        // Выпадающий список
        _cmbGridType = new ComboBox
        {
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 30,
            Font = new Font("Arial", 10),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        _cmbGridType.Items.Add("Статичный грид (станция)");
        _cmbGridType.Items.Add("Динамичный грид (шаттл)");
        _cmbGridType.SelectedIndex = _grid.IsStaticGrid ? 0 : 1;
        _cmbGridType.SelectedIndexChanged += (s, e) =>
        {
            bool isStatic = _cmbGridType.SelectedIndex == 0;
            _lblCurrent.Text = $"Текущий режим: {(isStatic ? "Статичный (станция)" : "Динамичный (шаттл)")}";
            _lblPreview.Text = GetPreviewText(isStatic);
        };
        Controls.Add(_cmbGridType);
        y += 40;

        // Описание
        _lblDescription = new Label
        {
            Text = "Статичный грид: станция с BecomesStation, MapAtmosphere, Roof, NavMap. " +
                   "Не имеет Physics/Shuttle — не двигается.\n\n" +
                   "Динамичный грид: шаттл с Physics (Dynamic), Shuttle, ImplicitRoof. " +
                   "Может двигаться и сталкиваться.",
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 80,
            Font = new Font("Arial", 9),
            ForeColor = Color.DarkGray,
            AutoSize = false
        };
        Controls.Add(_lblDescription);
        y += 90;

        // Предпросмотр YAML
        var lblPreview = new Label
        {
            Text = "Предпросмотр ключевых компонентов:",
            Font = new Font("Arial", 9, FontStyle.Bold),
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 20
        };
        Controls.Add(lblPreview);
        y += 22;

        _lblPreview = new Label
        {
            Text = GetPreviewText(_cmbGridType.SelectedIndex == 0),
            Location = new Point(left, y),
            Width = contentWidth,
            Height = 50,
            Font = new Font("Consolas", 8),
            ForeColor = Color.FromArgb(0, 100, 0),
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle,
            AutoSize = false
        };
        Controls.Add(_lblPreview);
        y += 55;

        // Кнопки
        var btnOk = new Button
        {
            Text = "Применить",
            DialogResult = DialogResult.OK,
            Location = new Point(contentWidth - 100, y),
            Width = 80,
            Height = 30,
            Font = new Font("Arial", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White
        };
        btnOk.Click += (s, e) =>
        {
            _grid.IsStaticGrid = _cmbGridType.SelectedIndex == 0;
            _onApply?.Invoke();
            Close();
        };
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(contentWidth - 190, y),
            Width = 80,
            Height = 30,
            Font = new Font("Arial", 10)
        };
        btnCancel.Click += (s, e) => Close();
        Controls.Add(btnCancel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private string GetPreviewText(bool isStatic)
    {
        if (isStatic)
        {
            return "BecomesStation\nMapAtmosphere (space: False)\nRoof\nNavMap\nRadiationGridResistance\nExplosionAirtightGrid";
        }
        else
        {
            return "Physics (bodyType: Dynamic)\nShuttle (dampingModifier: 0.25)\nImplicitRoof\nGridAtmosphere\nGasTileOverlay";
        }
    }
}
