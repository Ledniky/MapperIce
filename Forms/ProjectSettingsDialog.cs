// Forms/ProjectSettingsDialog.cs

using System.Drawing;
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

/// <summary>
/// Диалог настроек проекта: переключатель статичных/динамичных гридов и
/// прототип «наружной стены» для стен, создаваемых инструментом «Комната».
/// Все изменения применяются при закрытии окна.
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
    private ComboBox _cmbExteriorWall;
    private CheckBox _chkReplaceWalls;

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
        Size = new Size(540, 470);
        MinimumSize = new Size(540, 470);
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
        // ===== Раздел 1: режим гридов =====
        var lblSectionMode = new Label
        {
            Text = "Режим грида",
            Location = new Point(20, 18),
            Width = 200,
            Height = 20,
            Font = new Font("Arial", 10, FontStyle.Bold)
        };
        Controls.Add(lblSectionMode);

        var lblMode = new Label
        {
            Text = "Режим гридов:",
            Location = new Point(20, 45),
            Width = 100,
            Height = 20,
            Font = new Font("Arial", 10)
        };
        Controls.Add(lblMode);

        _cmbGridType = new ComboBox
        {
            Location = new Point(130, 40),
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
            Location = new Point(135, 72),
            Width = 380,
            Height = 45,
            Font = new Font("Arial", 8.5f),
            ForeColor = Color.Gray,
            AutoSize = false
        };
        Controls.Add(_lblDescription);

        _cmbGridType.SelectedIndexChanged += (s, e) =>
        {
            _lblDescription.Text = GetDescriptionText(_cmbGridType.SelectedIndex);
        };

        // Разделитель
        Controls.Add(new Label
        {
            Text = "——————",
            Location = new Point(20, 128),
            Width = 480,
            Height = 16,
            Font = new Font("Arial", 9),
            ForeColor = Color.FromArgb(200, 200, 200)
        });

        // ===== Раздел 2: наружная стена =====
        var lblSectionWall = new Label
        {
            Text = "Наружная стена",
            Location = new Point(20, 148),
            Width = 200,
            Height = 20,
            Font = new Font("Arial", 10, FontStyle.Bold)
        };
        Controls.Add(lblSectionWall);

        var lblExterior = new Label
        {
            Text = "Прототип стены:",
            Location = new Point(20, 175),
            Width = 110,
            Height = 40,
            Font = new Font("Arial", 9),
            TextAlign = ContentAlignment.TopLeft
        };
        Controls.Add(lblExterior);

        _cmbExteriorWall = new ComboBox
        {
            Location = new Point(135, 172),
            Width = 260,
            Font = new Font("Arial", 9),
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        _cmbExteriorWall.AllowDrop = true;
        _cmbExteriorWall.DragEnter += (s, e) =>
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.Text)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        };
        _cmbExteriorWall.DragDrop += (s, e) =>
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.Text)) return;
            var dropped = (string)e.Data.GetData(DataFormats.Text)!;
            if (string.IsNullOrWhiteSpace(dropped)) return;
            _cmbExteriorWall.Text = dropped.Trim();
        };
        Controls.Add(_cmbExteriorWall);

        // Наполняем комбобокс прототипами-стенами + текущее значение
        foreach (var id in _indexer.GetPrototypeIds())
        {
            if (!IsWallProtoCandidate(id)) continue;
            if (!_cmbExteriorWall.Items.Contains(id))
                _cmbExteriorWall.Items.Add(id);
        }
        _cmbExteriorWall.Text = string.IsNullOrEmpty(_grid.ExteriorWallProto)
            ? "WallSolid"
            : _grid.ExteriorWallProto;

        _chkReplaceWalls = new CheckBox
        {
            Text = "Заменять наружные стены",
            Location = new Point(20, 258),
            Width = 480,
            Height = 24,
            Font = new Font("Arial", 9),
            Checked = _grid.ReplaceExteriorWalls
        };
        Controls.Add(_chkReplaceWalls);

        var lblExteriorHint = new Label
        {
            Text = "Стены на внешнем периметре комнаты (за которыми — пустой тайл) " +
                   "инструментом «Комната» создаются этим прототипом. " +
                   "«Сущности из левого меню» для пустоты не считаются.",
            Location = new Point(20, 206),
            Width = 490,
            Height = 42,
            Font = new Font("Arial", 8.5f),
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft
        };
        Controls.Add(lblExteriorHint);

        // ===== Кнопка закрытия =====
        var btnClose = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(360, 410),
            Width = 120,
            Height = 30,
            Font = new Font("Arial", 10),
            TextAlign = ContentAlignment.MiddleCenter
        };
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);

        // Все значения применяются при закрытии окна (крестик, Esc, кнопка)
        FormClosing += (s, e) =>
        {
            _grid.IsStaticGrid = _cmbGridType.SelectedIndex == 0;
            _grid.ExteriorWallProto = _cmbExteriorWall.Text.Trim();
            _grid.ReplaceExteriorWalls = _chkReplaceWalls.Checked;
            _onApply?.Invoke();
        };

        AcceptButton = btnClose;
        CancelButton = btnClose;
    }

    private bool IsWallProtoCandidate(string id)
    {
        // Стены — структуры (не abstract). Берём по слову Wall в id, как уже
        // принято для стен в редакторе (WallSolid, WallReinforced и аналоги).
        if (!id.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return false;
        var proto = _indexer.FindPrototype(id);
        if (proto == null) return false;
        if (proto.IsAbstract) return false;
        if (!proto.IsStructure) return false;
        return true;
    }

    private string GetDescriptionText(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            return "Моно-грид (Z-network):\n" +
                   "Грид не двигается, для станций и карт.";
        }
        else
        {
            return "Динамичный грид (шаттлы/станция):\n" +
                   "Грид может двигаться и сталкиваться с объектами.";
        }
    }

}