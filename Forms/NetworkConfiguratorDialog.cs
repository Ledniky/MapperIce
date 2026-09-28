// Forms/NetworkConfiguratorDialog.cs
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

/// <summary>
/// Окно связи «кнопок/рычагов» (DeviceLinkSource) с устройствами (DeviceLinkSink)
/// в беспроводной сети. Слева — передатчики, справа — приёмники; пользователь
/// выбирает порт источника и порт приёмника и создаёт связь. При выборе сущности
/// через setHighlight подсвечиваются её связи на канвасе.
/// </summary>
public class NetworkConfiguratorDialog : Form
{
    private readonly Grid _grid;
    private readonly PrototypeIndexer _indexer;
    private readonly Action<IReadOnlyCollection<(int x, int y)>> _setHighlight;
    private readonly Action _requestRender;
    private readonly Action<bool> _setCanvasPickMode;

    private readonly ListBox _txList = new();
    private readonly ListBox _rxList = new();
    private readonly ComboBox _sourcePortCombo = new();
    private readonly ComboBox _targetPortCombo = new();
    private readonly ListView _linksList = new();
    private readonly Button _btnLink = new();
    private readonly Button _btnUnlink = new();
    private readonly Button _btnPick = new();

    private MapEntity? _selectedTx;
    private MapEntity? _selectedRx;
    private bool _pickMode;

    public NetworkConfiguratorDialog(
        Grid grid,
        PrototypeIndexer indexer,
        Action<IReadOnlyCollection<(int x, int y)>> setHighlight,
        Action requestRender,
        Action<bool> setCanvasPickMode)
    {
        _grid = grid;
        _indexer = indexer;
        _setHighlight = setHighlight;
        _requestRender = requestRender;
        _setCanvasPickMode = setCanvasPickMode;

        Text = "Конфигуратор сетей";
        Size = new Size(820, 480);
        MinimumSize = new Size(680, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        BuildUi();
        ReloadEntityLists();
    }

    private void BuildUi()
    {
        var label = new Label
        {
            Text = "Выберите передатчик (кнопку/рычаг) слева и один или несколько приёмников справа (Ctrl/Shift — множественный выбор), укажите порты и нажмите «Связать» — связь создастся со всеми отмеченными устройствами.",
            Location = new Point(12, 10),
            Size = new Size(796, 34),
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray
        };
        Controls.Add(label);

        // Левый столбец — передатчики
        var txLabel = new Label { Text = "Передатчики (кнопки/рычаги)", Location = new Point(12, 46), Size = new Size(260, 18), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        Controls.Add(txLabel);

        _txList.Location = new Point(12, 66);
        _txList.Size = new Size(260, 130);
        _txList.HorizontalScrollbar = true;
        _txList.SelectionMode = SelectionMode.MultiExtended;
        _txList.SelectedIndexChanged += OnTxChanged;
        Controls.Add(_txList);

        var srcLabel = new Label { Text = "Порт источника:", Location = new Point(12, 204), Size = new Size(115, 20), Font = new Font("Segoe UI", 9) };
        Controls.Add(srcLabel);
        _sourcePortCombo.Location = new Point(122, 202);
        _sourcePortCombo.Width = 150;
        _sourcePortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        Controls.Add(_sourcePortCombo);

        // Кнопка связать (по центру между столбцами)
        _btnLink.Text = "⇄ Связать";
        _btnLink.Location = new Point(281, 96);
        _btnLink.Size = new Size(92, 50);
        _btnLink.FlatStyle = FlatStyle.Flat;
        _btnLink.BackColor = Color.FromArgb(220, 245, 220);
        _btnLink.Click += (s, e) => CreateLink();
        Controls.Add(_btnLink);

        // Правый столбец — приёмники
        var rxLabel = new Label { Text = "Приёмники (устройства)", Location = new Point(386, 46), Size = new Size(260, 18), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        Controls.Add(rxLabel);

        _rxList.Location = new Point(386, 66);
        _rxList.Size = new Size(260, 130);
        _rxList.HorizontalScrollbar = true;
        _rxList.SelectionMode = SelectionMode.MultiExtended;
        _rxList.SelectedIndexChanged += OnRxChanged;
        Controls.Add(_rxList);

        var tgtLabel = new Label { Text = "Порт приёмника:", Location = new Point(386, 204), Size = new Size(115, 20), Font = new Font("Segoe UI", 9) };
        Controls.Add(tgtLabel);
        _targetPortCombo.Location = new Point(496, 202);
        _targetPortCombo.Width = 150;
        _targetPortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        Controls.Add(_targetPortCombo);

        // Список связей выбранного передатчика
        var linksLabel = new Label { Text = "Связи выбранного передатчика:", Location = new Point(12, 236), Size = new Size(300, 18), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        Controls.Add(linksLabel);

        _linksList.View = View.Details;
        _linksList.FullRowSelect = true;
        _linksList.GridLines = true;
        _linksList.Location = new Point(12, 256);
        _linksList.Size = new Size(634, 170);
        _linksList.Columns.Add("Цель", 300);
        _linksList.Columns.Add("Порт источника → порт приёмника", 320);
        Controls.Add(_linksList);

        _btnUnlink.Text = "Удалить связь";
        _btnUnlink.Location = new Point(12, 432);
        _btnUnlink.Size = new Size(120, 28);
        _btnUnlink.FlatStyle = FlatStyle.Flat;
        _btnUnlink.BackColor = Color.FromArgb(250, 235, 235);
        _btnUnlink.Click += (s, e) => RemoveSelectedLink();
        Controls.Add(_btnUnlink);

        // Кнопка «Выделить» — выбор объекта на канвасе кликом
        _btnPick.Text = "🔍 Выделить";
        _btnPick.Location = new Point(140, 432);
        _btnPick.Size = new Size(120, 28);
        _btnPick.FlatStyle = FlatStyle.Flat;
        _btnPick.BackColor = Color.FromArgb(225, 235, 250);
        _btnPick.Click += (s, e) => TogglePickMode();
        Controls.Add(_btnPick);

        FormClosed += (s, e) =>
        {
            _setCanvasPickMode(false);
            _setHighlight(Array.Empty<(int, int)>());
        };
    }

    private void TogglePickMode()
    {
        _pickMode = !_pickMode;
        _btnPick.Text = _pickMode ? "Отмена" : "🔍 Выделить";
        _btnPick.BackColor = _pickMode ? Color.FromArgb(255, 235, 180) : Color.FromArgb(225, 235, 250);
        _setCanvasPickMode(_pickMode);
    }

    /// <summary>
    /// Вызывается из MainForm, когда в режиме выбора пользователь кликнул по сущности.
    /// add=false (без Ctrl/Shift) — заменить выделение («или»); add=true (Ctrl/Shift) —
    /// добавить к выделению («и»). Работает одинаково для передатчиков и приёмников.
    /// </summary>
    public bool SelectEntity(MapEntity entity, bool add)
    {
        if (entity == null) return false;

        string proto = entity.Proto;
        bool isDoorLike = _indexer.HasComponent(proto, "Door");
        bool isReceiver = isDoorLike || _indexer.HasDeviceLinkSink(proto);
        bool isSource = _indexer.HasDeviceLinkSource(proto) && !isReceiver;

        if (isSource)
        {
            int idx = FindIndexIn(_txList, entity);
            if (idx < 0) return false;

            SelectWithModifier(_txList, idx, add);
            _selectedTx = entity;
            LoadSourcePorts(entity);
            ReloadLinksList();
            UpdateHighlight();
            return true;
        }

        if (isReceiver)
        {
            int idx = FindIndexIn(_rxList, entity);
            if (idx < 0) return false;

            SelectWithModifier(_rxList, idx, add);
            _selectedRx = entity;
            LoadTargetPorts(entity);
            UpdateHighlight();
            return true;
        }

        return false;
    }

    private static void SelectWithModifier(ListBox list, int idx, bool add)
    {
        if (add)
        {
            list.SetSelected(idx, true); // добавить («и»)
        }
        else
        {
            list.ClearSelected();        // заменить («или»)
            list.SetSelected(idx, true);
        }
    }

    private void LoadSourcePorts(MapEntity entity)
    {
        _sourcePortCombo.Items.Clear();
        foreach (var port in _indexer.GetDeviceLinkSourcePorts(entity.Proto))
            _sourcePortCombo.Items.Add(port);
        if (_sourcePortCombo.Items.Count > 0) _sourcePortCombo.SelectedIndex = 0;
    }

    private void LoadTargetPorts(MapEntity entity)
    {
        _targetPortCombo.Items.Clear();
        foreach (var port in _indexer.GetDeviceLinkSinkPorts(entity.Proto))
            _targetPortCombo.Items.Add(port);
        if (_targetPortCombo.Items.Count > 0) _targetPortCombo.SelectedIndex = 0;
    }

    private static int FindIndexIn(ListBox list, MapEntity entity)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is EntityItem it &&
                (ReferenceEquals(it.Entity, entity) ||
                 (Math.Abs(it.Entity.X - entity.X) < 0.01f && Math.Abs(it.Entity.Y - entity.Y) < 0.01f)))
                return i;
        }
        return -1;
    }

    private void ReloadEntityLists()
    {
        _txList.Items.Clear();
        _rxList.Items.Clear();

        var entities = _grid.Entities.OfType<MapEntity>()
            .Where(e => !string.IsNullOrEmpty(e.Proto))
            .ToList();

        foreach (var entity in entities)
        {
            string proto = entity.Proto;

            // Дверь/шлюз (напр. гермозатвор) — это потребитель сигнала, его кладём
            // в приёмники В ПЕРВУЮ очередь, даже если у него случайно есть и
            // DeviceLinkSource (он тогда не должен уходить в передатчики).
            bool isDoorLike = _indexer.HasComponent(proto, "Door");
            bool isReceiver = isDoorLike || _indexer.HasDeviceLinkSink(proto);
            bool isSource = _indexer.HasDeviceLinkSource(proto) && !isReceiver;

            if (isReceiver)
                _rxList.Items.Add(new EntityItem(entity, _indexer));
            else if (isSource)
                _txList.Items.Add(new EntityItem(entity, _indexer));
        }

        if (_txList.Items.Count > 0) _txList.SelectedIndex = 0;
        if (_rxList.Items.Count > 0) _rxList.SelectedIndex = 0;
    }

    private void OnTxChanged(object? sender, EventArgs e)
    {
        if (_txList.SelectedItem is not EntityItem item)
        {
            _selectedTx = null;
            _sourcePortCombo.Items.Clear();
            _linksList.Items.Clear();
            return;
        }

        _selectedTx = item.Entity;
        _sourcePortCombo.Items.Clear();
        foreach (var port in _indexer.GetDeviceLinkSourcePorts(item.Entity.Proto))
            _sourcePortCombo.Items.Add(port);
        if (_sourcePortCombo.Items.Count > 0) _sourcePortCombo.SelectedIndex = 0;

        ReloadLinksList();
        UpdateHighlight();
    }

    private void OnRxChanged(object? sender, EventArgs e)
    {
        var indices = _rxList.SelectedIndices;
        if (indices.Count == 0)
        {
            _selectedRx = null;
            _targetPortCombo.Items.Clear();
            UpdateHighlight();
            return;
        }

        // При мультивыборе порт приёмника берём из последнего выбранного устройства
        var last = _rxList.Items[indices[indices.Count - 1]] as EntityItem;
        _selectedRx = last?.Entity;

        _targetPortCombo.Items.Clear();
        if (last != null)
        {
            foreach (var port in _indexer.GetDeviceLinkSinkPorts(last.Entity.Proto))
                _targetPortCombo.Items.Add(port);
        }
        if (_targetPortCombo.Items.Count > 0) _targetPortCombo.SelectedIndex = 0;

        UpdateHighlight();
    }

    private void CreateLink()
    {
        if (_selectedTx == null) return;
        if (_sourcePortCombo.SelectedItem is not string srcPort) return;
        if (_targetPortCombo.SelectedItem is not string tgtPort) return;

        bool changed = false;

        // Создаём связь с каждым выбранным приёмником (одна «связь» на источник → все отмеченные устройства)
        foreach (var item in _rxList.SelectedItems.Cast<EntityItem>())
        {
            var rx = item.Entity;

            bool exists = _selectedTx.NetworkLinks.Any(l =>
                Math.Abs(l.TargetX - rx.X) < 0.01f &&
                Math.Abs(l.TargetY - rx.Y) < 0.01f &&
                l.SourcePort == srcPort && l.TargetPort == tgtPort);
            if (exists) continue;

            _selectedTx.NetworkLinks.Add(new EntityNetworkLink
            {
                TargetX = rx.X,
                TargetY = rx.Y,
                SourcePort = srcPort,
                TargetPort = tgtPort
            });
            changed = true;
        }

        if (changed)
        {
            ReloadLinksList();
            UpdateHighlight();
            _requestRender();
        }
    }

    private void RemoveSelectedLink()
    {
        if (_selectedTx == null) return;
        if (_linksList.SelectedItems.Count == 0) return;
        if (_linksList.SelectedItems[0].Tag is EntityNetworkLink link)
        {
            _selectedTx.NetworkLinks.Remove(link);
            ReloadLinksList();
            UpdateHighlight();
            _requestRender();
        }
    }

    private void ReloadLinksList()
    {
        _linksList.Items.Clear();
        if (_selectedTx == null) return;

        foreach (var link in _selectedTx.NetworkLinks)
        {
            var targetEntity = _grid.Entities.OfType<MapEntity>()
                .FirstOrDefault(e =>
                    Math.Abs(e.X - link.TargetX) < 0.01f &&
                    Math.Abs(e.Y - link.TargetY) < 0.01f);

            string targetName = targetEntity != null
                ? EntityItem.LabelFor(targetEntity, _indexer)
                : $"({link.TargetX:0.#}, {link.TargetY:0.#})";

            var lv = new ListViewItem(new[] { targetName, $"{link.SourcePort} → {link.TargetPort}" });
            lv.Tag = link;
            _linksList.Items.Add(lv);
        }
    }

    private void UpdateHighlight()
    {
        var positions = new HashSet<(int x, int y)>();

        if (_selectedTx != null)
        {
            var src = ((int)Math.Round(_selectedTx.X), (int)Math.Round(_selectedTx.Y));
            positions.Add(src);
            foreach (var link in _selectedTx.NetworkLinks)
                positions.Add(((int)Math.Round(link.TargetX), (int)Math.Round(link.TargetY)));
        }

        if (_rxList.SelectedItems.Count > 0)
        {
            foreach (var item in _rxList.SelectedItems.Cast<EntityItem>())
            {
                var rx = item.Entity;
                var cell = ((int)Math.Round(rx.X), (int)Math.Round(rx.Y));
                positions.Add(cell);

                // Подсветить источники, которые уже ссылаются на этот приёмник
                foreach (var entity in _grid.Entities.OfType<MapEntity>())
                {
                    foreach (var link in entity.NetworkLinks)
                    {
                        if (Math.Abs(link.TargetX - rx.X) < 0.01f &&
                            Math.Abs(link.TargetY - rx.Y) < 0.01f)
                            positions.Add(((int)Math.Round(entity.X), (int)Math.Round(entity.Y)));
                    }
                }
            }
        }

        _setHighlight(positions);
    }

    /// <summary>Обёртка для отображения сущности в списке (имя + id + координаты).</summary>
    private class EntityItem
    {
        public MapEntity Entity { get; }
        private readonly PrototypeIndexer _indexer;

        public EntityItem(MapEntity entity, PrototypeIndexer indexer)
        {
            Entity = entity;
            _indexer = indexer;
        }

        public static string LabelFor(MapEntity entity, PrototypeIndexer indexer)
        {
            var localized = indexer.FindPrototype(entity.Proto)?.LocalizedName;
            string name = string.IsNullOrWhiteSpace(localized) ? entity.Proto : localized;

            // Координаты показываем целочисленной клеткой (как хранятся ключи связей).
            // Целые числа инвариантны к локаль — не получится "(31,5,8,5)" в ru-культуре.
            int ix = (int)Math.Round(entity.X);
            int iy = (int)Math.Round(entity.Y);
            return $"{name} ({entity.Proto}) @ ({ix},{iy})";
        }

        public override string ToString() => LabelFor(Entity, _indexer);
    }
}