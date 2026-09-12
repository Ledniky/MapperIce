// Forms/ProjectSettingsDialog.cs

using System.Drawing;
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
        Size = new Size(500, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Arial", 10);
        DragDropCursorHelper.SuppressForbiddenCursor(this);

        CreateUI();
    }

    private void CreateUI()
    {
        var lblMode = new Label
        {
            Text = "Режим гридов:",
            Location = new Point(20, 25),
            Width = 100,
            Height = 20,
            Font = new Font("Arial", 10)
        };
        Controls.Add(lblMode);

        _cmbGridType = new ComboBox
        {
            Location = new Point(130, 20),
            Width = 180,
            Height = 30,
            Font = new Font("Arial", 10),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        _cmbGridType.Items.Add("Моно-грид");
        _cmbGridType.Items.Add("Динамичный");
        _cmbGridType.SelectedIndex = _grid.IsStaticGrid ? 0 : 1;
        Controls.Add(_cmbGridType);

        _lblDescription = new Label
        {
            Text = GetDescriptionText(_cmbGridType.SelectedIndex),
            Location = new Point(20, 55),
            Width = 460,
            Height = 380,
            Font = new Font("Arial", 10),
            ForeColor = Color.Gray,
            AutoSize = false
        };
        Controls.Add(_lblDescription);

        // Кнопки
        var btnOk = new Button
        {
            Text = "Применить",
            DialogResult = DialogResult.OK,
            Location = new Point(340, 20),
            Width = 80,
            Height = 30,
            Font = new Font("Arial", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
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
            Location = new Point(340, 20 + btnOk.Height + 10),
            Width = 80,
            Height = 30,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter
        };
        btnCancel.Click += (s, e) => Close();
        Controls.Add(btnCancel);

        _cmbGridType.SelectedIndexChanged += (s, e) =>
        {
            _lblDescription.Text = GetDescriptionText(_cmbGridType.SelectedIndex);
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private string GetDescriptionText(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            return "Моно-грид (станция):\n\n" +
                   "Грид не двигается, используется для станций и карт.\n" +
                   "Применяется: BecomesStation, Roof, NavMap.";
        }
        else
        {
            return "Динамичный грид (шаттл):\n\n" +
                   "Грид может двигаться и сталкиваться с объектами.\n" +
                   "Применяется: Physics (Dynamic), Shuttle, ImplicitRoof.";
        }
    }

}
