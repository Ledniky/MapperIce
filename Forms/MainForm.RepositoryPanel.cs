// Forms/MainForm.RepositoryPanel.cs

using System.Linq;
using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    private System.Windows.Forms.Timer? _searchDebounceTimer;
    private System.Windows.Forms.Timer? _moveTimer;
    private DragGhostForm? _dragGhost;
    // Последний запрос, по которому реально выполнен поиск. Нужен, чтобы не
    // перезапускать дорогой поиск на неизменившийся текст (и чтобы не
    // подгружать весь список при пустом поле).
    private string _lastSearchedText = "";

    // Кэш результата фильтрации прототипов по ключу (текст-запрос; выбранный
    // фильтр). GetPrototypeIds()/SearchPrototypes() + FindPrototype() внутри
    // каждой ветки switch — дорогие операции, а фильтр в списке переключается
    // часто. Кэш сбрасывается при переиндексации репозитория.
    private readonly Dictionary<(string query, string filter), List<string>> _searchFilterCache = new();

    // === ПАНЕЛЬ РЕПОЗИТОРИЕВ ===
    private void CreateRepositoryPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = _repoPanelCurrentWidth,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None
        };
        DragDropCursorHelper.SuppressForbiddenCursor(panel);
        _repoPanel = panel;

        var rightLine = new Panel
        {
            Dock = DockStyle.Right,
            Width = 1,
            BackColor = AppTheme.PanelEdge
        };
        panel.Controls.Add(rightLine);

        var listContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };
        _protoList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 46
        };
_protoList.DoubleClick += OnPrototypeDoubleClick;
_protoList.DrawItem += ProtoList_DrawItem;
_protoList.MouseDown += ProtoList_MouseDown;
listContainer.Controls.Add(_protoList);
        panel.Controls.Add(listContainer);

        _iconRedrawTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _iconRedrawTimer.Tick += (s, e) =>
        {
            if (_pendingIconRedraw)
            {
                _pendingIconRedraw = false;
                if (!_protoList.IsDisposed) _protoList.Invalidate();
                if (_doorToolCombo != null && !_doorToolCombo.IsDisposed) _doorToolCombo.Invalidate();
                if (_powerJunctionCombo != null && !_powerJunctionCombo.IsDisposed) _powerJunctionCombo.Invalidate();
            }
        };
        _iconRedrawTimer.Start();

        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 2, 0, 2)
        };

        _searchBox = new TextBox
        {
            Location = new Point(3, 2),
            Width = 155,
            Height = 22,
            Text = "Поиск прототипов...",
            Enabled = false
        };

        // Раньше поиск запускался на КАЖДОЕ нажатие клавиши, из-за чего почти
        // никогда не повторялся один и тот же текст запроса — _searchCache в
        // PrototypeIndexer фактически никогда не попадал в hit. Debounce ждёт
        // паузу в наборе (200мс) и запускает поиск один раз, а не на каждую букву.
        _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer!.Stop();
            string text = _searchBox.Text.Trim();
            // Пустой запрос (или плейсхолдер) НЕ запускает поиск: иначе при
            // очистке поля снова подгружаются ВСЕ прототипы (десятки тысяч),
            // и интерфейс зависает на секунды. Список при пустом поле
            // сохраняет последний результат поиска.
            if (string.IsNullOrEmpty(text) || text == "Поиск прототипов...")
                return;
            // Запрос не изменился относительно последнего выполненного — не перезапускаем
            // дорогой поиск (и кэш в PrototypeIndexer всё равно бы не сработал многократно).
            if (text == _lastSearchedText)
                return;
            _lastSearchedText = text;
            UpdatePrototypeList(text);
        };
        // Запуск debounce-таймера на отжатие клавиши (а не на каждую букву).
        // Сам Tick берёт актуальный текст из _searchBox, поэтому здесь достаточно
        // просто сбросить и перезапустить таймер.
        _searchBox.KeyUp += (s, e) =>
        {
            _searchDebounceTimer!.Stop();
            _searchDebounceTimer.Start();
        };
        _searchBox.Enter += (s, e) => { if (_searchBox.Text == "Поиск прототипов...") _searchBox.Text = ""; };
        _searchBox.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(_searchBox.Text)) _searchBox.Text = "Поиск прототипов..."; };
        searchPanel.Controls.Add(_searchBox);

        _filterCombo = new ComboBox
        {
            Location = new Point(163, 2),
            Width = 75,
            Height = 22,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8),
            Enabled = false
        };


        _filterCombo.Items.AddRange(new object[] { "Все", "Тайлы", "Структура", "Спавнер", "Декали" });
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (s, e) =>
        {
            _currentFilter = _filterCombo.SelectedItem?.ToString()?.ToLower() ?? "all";
            UpdatePrototypeList(_searchBox.Text);
        };
        searchPanel.Controls.Add(_filterCombo);
        panel.Controls.Add(searchPanel);

        // ============================================================
        // ПАНЕЛЬ КНОПОК РЕПОЗИТОРИЯ
        // ============================================================
        var btnPanel = new Panel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(0, 2, 0, 2) };

        // ============================================================
        // СТРОКА 1: Управление репозиториями
        // ============================================================
        _btnAddRepo = new Button { Text = "➕", Location = new Point(5, 5), Width = 30, Height = 25 };
        _btnAddRepo.Click += (s, e) => AddRepository();
        btnPanel.Controls.Add(_btnAddRepo);

        _btnRemoveRepo = new Button { Text = "🗑", Location = new Point(40, 5), Width = 30, Height = 25, Enabled = false };
        _btnRemoveRepo.Click += (s, e) => RemoveRepository();
        btnPanel.Controls.Add(_btnRemoveRepo);

        _btnIndexRepo = new Button
        {
            Text = "🔄 Обновить",
            Location = new Point(75, 5),
            Width = 40,
            Height = 25,
            Enabled = false,
            BackColor = Color.LightGreen,
            FlatStyle = FlatStyle.Flat
        };
        _btnIndexRepo.Click += (s, e) => IndexSelectedRepository();
        btnPanel.Controls.Add(_btnIndexRepo);

        // ============================================================
        // СТРОКА 2: Размещение прототипа + центрирование + настройки
        // ============================================================
        _btnPlaceProto = new Button
        {
            Text = "🔒",
            Location = new Point(3, 35),
            Width = 60,
            Height = 25,
            Enabled = false,
            BackColor = Color.FromArgb(220, 220, 220),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };

        _btnPlaceProto.EnabledChanged += (s, e) =>
        {
            if (_btnPlaceProto.Enabled)
            {
                _btnPlaceProto.Text = "➕";
                // Зелёный, как у остальных кнопок-переключателей (в тёмной теме — тёмнее)
                _btnPlaceProto.BackColor = AppTheme.ToggleOnBack;
                _btnPlaceProto.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }
            else
            {
                _btnPlaceProto.Text = "🔒";
                _btnPlaceProto.BackColor = AppTheme.ToggleOffBack;
                _btnPlaceProto.Font = new Font("Segoe UI", 8);
            }
        };

        _btnPlaceProto.Click += (s, e) => ArmPrototypePlacement();
        btnPanel.Controls.Add(_btnPlaceProto);

        _btnSnapEntityCenter = new Button
        {
            Text = "🔲",
            Location = new Point(68, 35),
            Width = 60,
            Height = 25,
            BackColor = _snapEntityToCenter ? AppTheme.ToggleOnBack : AppTheme.ToggleOffBack,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        _btnSnapEntityCenter.Click += (s, e) =>
        {
            _snapEntityToCenter = !_snapEntityToCenter;
            _btnSnapEntityCenter.BackColor = _snapEntityToCenter ? AppTheme.ToggleOnBack : AppTheme.ToggleOffBack;
        };
        btnPanel.Controls.Add(_btnSnapEntityCenter);

        _btnCenterSettings = new Button
        {
            Text = "⚙ 0.5/0.5",
            Location = new Point(133, 35),
            Width = 60,
            Height = 25,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnCenterSettings.Click += (s, e) => ShowCenterSettingsDialog();
        btnPanel.Controls.Add(_btnCenterSettings);








        _btnEntityRotationSnap = new Button
        {
            Text = "📐",
            Location = new Point(198, 35),
            Width = 30,
            Height = 25,
            BackColor = _snapEntityRotation ? AppTheme.ToggleOnBack : AppTheme.ToggleOffBack,
            FlatStyle = FlatStyle.Flat
        };
        _btnEntityRotationSnap.Click += (s, e) =>
        {
            _snapEntityRotation = !_snapEntityRotation;
            _btnEntityRotationSnap.BackColor = _snapEntityRotation ? AppTheme.ToggleOnBack : AppTheme.ToggleOffBack;

            if (_snapEntityRotation)
            {
                float step = (float)(Math.PI / 2);
                _currentEntityRotation = (float)(Math.Round(_currentEntityRotation / step) * step);
                Render();
            }
        };
        btnPanel.Controls.Add(_btnEntityRotationSnap);





        // ============================================================
        // КОНЕЦ ПАНЕЛИ КНОПОК
        // ============================================================
        panel.Controls.Add(btnPanel);

        _protoList.SelectedIndexChanged += (s, e) =>
        {
            var id = _protoList.SelectedItem?.ToString();
            bool valid = !string.IsNullOrEmpty(id) &&
                         !id.StartsWith("(") && !id.StartsWith("⚠") &&
                         !id.StartsWith("⏳") && !id.StartsWith("Ошибка") &&
                         !id.StartsWith("Нажмите");
            if (_btnPlaceProto != null) _btnPlaceProto.Enabled = valid;

            if (valid && _toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                _protoToPlace = id;
                // Статус удалён — тип комнаты теперь в ComboBox
            }
        };

        _repoSelector = new ComboBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 5, 0, 5)
        };
        _repoSelector.SelectedIndexChanged += OnRepoSelected;
        panel.Controls.Add(_repoSelector);

        var title = new Label
        {
            Text = "Репозиторий",
            Font = new Font("Arial", 12, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(title);

        Controls.Add(panel);
    }


    private void UpdateRepoSelector()
    {
        _repoSelector.Items.Clear();
        foreach (var repo in _repoManager.Repositories)
        {
            _repoSelector.Items.Add(repo);
        }
        if (_repoSelector.Items.Count > 0)
        {
            var savedId = _repoManager.SelectedRepositoryId;
            var match = _repoManager.Repositories.FirstOrDefault(r => r.Id == savedId);
            _repoSelector.SelectedItem = match ?? _repoManager.Repositories[0];
        }
        else
        {
            _protoList.Items.Clear();
            _protoList.Items.Add("(нет репозиториев)");
            _searchBox.Enabled = false;
            _filterCombo.Enabled = false;
            _btnRemoveRepo.Enabled = false;
            _btnIndexRepo.Enabled = false;
        }
    }


    private void OnRepoSelected(object? sender, EventArgs e)
    {
        ClearProtoIconCache();

        var repo = _repoSelector.SelectedItem as Repository;
        bool hasRepo = repo != null;

        _btnRemoveRepo.Enabled = hasRepo;
        _btnIndexRepo.Enabled = hasRepo;
        _searchBox.Enabled = hasRepo;
        _filterCombo.Enabled = hasRepo;

        if (hasRepo)
        {
            _repoManager.SetSelectedRepository(repo!.Id);
        }

        if (hasRepo && repo!.IsIndexed)
        {
            _indexer.IndexRepository(repo);
            // Репозиторий сменился — текстуры на карте (ткани, стены, сущности)
            // кэшируются в Renderer. Сбрасываем кэш рендера, иначе спрайты
            // продолжают показывать текстуры старого репозитория.
            _renderer?.ClearCache();
            Render();
        }
        else if (hasRepo)
        {
            _protoList.Items.Clear();
            _protoList.Items.Add("⚠️ Репозиторий не \nпроиндексирован");
            _protoList.Items.Add("Нажмите 'Обновить' \nдля загрузки");
        }
    }


    private void AddRepository()
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = @"Выберите репозиторий, например D:\_Goob-Station";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _repoManager.AddRepository(dialog.SelectedPath);
        }
    }


    private void RemoveRepository()
    {
        if (_repoSelector.SelectedItem is Repository repo)
        {
            if (MessageBox.Show($"Удалить репозиторий '{repo.Name}'?", "Подтверждение",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _repoManager.RemoveRepository(repo.Id);
            }
        }
    }


    private void IndexSelectedRepository()
    {
        if (_repoSelector.SelectedItem is Repository repo)
        {
            // Подтверждение спрашиваем только у ранее проиндексированного репозитория:
            // обновление пересобирает индекс с нуля и занимает время, а первый проход
            // ничего не перезаписывает — там подтверждение лишнее
            if (repo.IsIndexed &&
                MessageBox.Show(
                    $"Обновить индекс репозитория '{repo.Name}'?\n" +
                    $"Текущий индекс ({repo.PrototypeCount} прототипов) будет пересобран заново.",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _indexer.ReindexFromDisk(repo);

            // Переиндексация того же репозитория тоже меняет прототипы/спрайты, но в отличие
            // от смены репозитория раньше кэш рендера здесь не сбрасывался — запечённые
            // текстуры оставались от старого индекса, и спрайты показывали устаревший вид
            // до ручного трогания карты/переключения репозитория.
            _renderer?.ClearCache();

            int count = _indexer.GetPrototypeIds().Count;
            _repoManager.MarkAsIndexed(repo.Id, count);
            UpdateRepoSelector();

            MessageBox.Show($"Проиндексировано {count} прототипов");
        }
    }


    private void UpdatePrototypeList(string filter = "")
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _protoList.Items.Clear();
        _protoList.Items.Add("⏳ Поиск...");

        // Измеряем ширину панели на основе самой длинной строки
        void AdjustPanelWidth(IEnumerable<string> items)
        {
            if (_protoList == null || _protoList.IsDisposed) return;
            using var g = _protoList.CreateGraphics();
            var font = _protoList.Font;
            float maxW = 0;
            foreach (var item in items)
            {
                var size = TextRenderer.MeasureText(g, item, font);
                if (size.Width > maxW) maxW = size.Width;
            }
            // Минимальная ширина 200, максимум 500, запас 50px на иконки и отступы
            var newWidth = Math.Max(200, Math.Min(500, (int)maxW + 50));
            if (_protoList.Parent != null && _protoList.Parent.Width != newWidth)
            {
                _protoList.Parent.Width = newWidth;
                _toolPanel.Parent?.PerformLayout();
            }
        }

        // Invoke требует уже созданного дескриптора окна. При старте, если
        // индексация из кэша завершается очень быстро (как сейчас), фоновая
        // задача ниже успевает вызвать Invoke раньше, чем форма создаёт хэндл —
        // обычный Invoke в этот момент кидает InvalidOperationException.
        // Вместо падения ждём HandleCreated, если хэндла ещё нет
        void SafeInvoke(Action action)
        {
            if (_protoList.IsDisposed) return;
            if (_protoList.IsHandleCreated)
                _protoList.Invoke(action);
            else
                _protoList.HandleCreated += (s, e) => { if (!_protoList.IsDisposed) _protoList.Invoke(action); };
        }

Task.Run(() =>
        {
            try
            {
                // Кэш фильтрации: дорогие GetPrototypeIds()/SearchPrototypes() и
                // FindPrototype() в switch не пересчитываем, если для данного
                // (запрос, фильтр) результат уже получен. Оборачиваем в try, чтобы
                // при гонке потоков (два запроса с одинаковым ключом) не упасть.
                List<string>? filteredIds;
                var cacheKey = (filter, _currentFilter);
                lock (_searchFilterCache)
                    _searchFilterCache.TryGetValue(cacheKey, out filteredIds);

                if (filteredIds == null)
                {
                    var allIds = string.IsNullOrEmpty(filter) || filter == "Поиск прототипов..."
                        ? _indexer.GetPrototypeIds()
                        : _indexer.SearchPrototypes(filter);

                    if (token.IsCancellationRequested) return;

                    filteredIds = allIds;

                    switch (_currentFilter)
                    {
                        case "all":
                        case "все":
                            // Декали по умолчанию скрыты из общего списка — слишком много "мусора"
                            // (сотни BrickTile*, RoadLine* и т.п.), видны только через отдельный фильтр
                            filteredIds = allIds.Where(id =>
                                _indexer.FindPrototype(id)?.Type != "decal"
                            ).ToList();
                            break;
                        case "тайлы":
                        case "tiles":
                            // Тайлы ищем по реальному Type из YAML (type: tile), а не по подстроке
                            // в id — имена настоящих тайлов (FloorSteel, Plating, FloorCarpet и т.п.)
                            // лишь отчасти содержат "tile"/"floor"/"plating", и часть тайлов из-за
                            // этого терялась из фильтра
                            filteredIds = allIds.Where(id =>
                                _indexer.FindPrototype(id)?.Type == "tile"
                            ).ToList();
                            break;
                        case "структура":
                        case "structures":
                            filteredIds = allIds.Where(id =>
                                id.Contains("wall", StringComparison.OrdinalIgnoreCase) ||
                                id.Contains("door", StringComparison.OrdinalIgnoreCase) ||
                                id.Contains("window", StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                            break;
                        case "спавнер":
                        case "spawner":
                            filteredIds = allIds.Where(id =>
                                id.Contains("spawn", StringComparison.OrdinalIgnoreCase) ||
                                id.Contains("spawner", StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                            break;
                        case "декали":
                        case "decals":
                            // Декали ищем по реальному Type из YAML (type: decal), а не по подстроке в id —
                            // имена декалей (BrickTileDarkBox и т.п.) никак не намекают на то, что это декаль
                            filteredIds = allIds.Where(id =>
                                _indexer.FindPrototype(id)?.Type == "decal"
                            ).ToList();
                            break;
                    }

                    filteredIds = filteredIds
                        .Where(id => !id.StartsWith("*"))           // исключаем начинающиеся с *
                        .Where(id => !id.Contains("Action"))        // исключаем содержащие "Action"
                        .Where(id => _indexer.FindPrototype(id)?.IsAbstract != true) // исключаем шаблоны с "abstract: true"
                        .ToList();

                    lock (_searchFilterCache)
                        _searchFilterCache[cacheKey] = filteredIds;

                    if (token.IsCancellationRequested) return;
                }

                // Десятки тысяч прототипов в ListBox всё равно не прокрутишь без
                // зависания (авто-подбор ширины O(N) + добавление элементов), поэтому
                // ограничиваем вывод разумным числом самых релевантных совпадений.
                var result = filteredIds.Take(2000).ToList();

if (token.IsCancellationRequested) return;

                SafeInvoke(() =>
                {
                    _protoList.Items.Clear();
                    if (result.Count == 0)
                        _protoList.Items.Add("(нет прототипов)");
                    else
                        foreach (var id in result)
                            _protoList.Items.Add(id);
                    // Пересчитываем ширину панели по содержимому
                    AdjustPanelWidth(result);
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() =>
                {
                    _protoList.Items.Clear();
                    _protoList.Items.Add($"Ошибка: {ex.Message}");
                });
            }
        }, token);
    }


private void ShowPrototypeInfo(string id)
{
    var proto = _indexer.FindPrototype(id);
    var path = _indexer.GetFullTexturePath(id);

    bool fileExists = path != null && File.Exists(path);

    string message = $"ID: {id}\n";
    message += $"SpritePath: {proto?.SpritePath ?? "(нет)"}\n";
    message += $"FilePath: {proto?.FilePath ?? "(нет)"}\n";
    message += $"\n--- АВТОМАТИЧЕСКИЙ ПУТЬ ---\n{path ?? "НЕ НАЙДЕН"}\n";
    message += $"Файл существует: {(fileExists ? "✅ ДА" : "❌ НЕТ")}";

    MessageBox.Show(message, "Информация о прототипе");
}

private void OnPrototypeDoubleClick(object? sender, EventArgs e)
{
    if (_protoList.SelectedItem == null) return;
    string? id = _protoList.SelectedItem.ToString();
    if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

    ShowPrototypeInfo(id);
}

private void ProtoList_MouseDown(object? sender, MouseEventArgs e)
{
    if (e.Button != MouseButtons.Left) return;

    int index = _protoList.IndexFromPoint(e.Location);
    if (index < 0 || index >= _protoList.Items.Count) return;

    string? id = _protoList.Items[index]?.ToString();
    bool isRealProto = !string.IsNullOrEmpty(id) &&
                        !id.StartsWith("(") && !id.StartsWith("⚠") &&
                        !id.StartsWith("⏳") && !id.StartsWith("Ошибка") &&
                        !id.StartsWith("Нажмите");
    if (!isRealProto) return;

    // Двойной клик — показываем информацию о прототипе и НЕ запускаем
    // Drag'n'Drop (иначе DoDragDrop на первом клике съедает события мыши
    // и обычный DoubleClick листбокса до нас не долетает).
    if (e.Clicks >= 2)
    {
        ShowPrototypeInfo(id!);
        return;
    }

    // Курсор во время OLE drag'n'drop ...    // него всё равно рисует своё "запрещено"/стрелку) — поэтому вместо этого ведём
    // за курсором отдельное floating-окошко на всё время перетаскивания. Берём ту же
    // иконку, что уже нарисована в списке (кэш ProtoList_DrawItem), либо грузим её
    // синхронно на месте, если фоновая загрузка ещё не успела её закэшировать.
    var dragIcon = GetCachedProtoIcon(id!) ?? GetPrototypeIcon(id!);
    _dragGhost = new DragGhostForm(dragIcon, id!);
    _dragGhost.MoveTo(Cursor.Position);
    _dragGhost.Show();
    _protoList.GiveFeedback += ProtoList_GiveFeedback;

    // Timer для плавного следования "призрака" за курсором.
    // GiveFeedback срабатывает только при смене состояния OLE DnD,
    // поэтому отдельно отслеживаем движение мыши через таймер.
    _moveTimer = new System.Windows.Forms.Timer { Interval = 16 };
    _moveTimer.Tick += (s, e) => _dragGhost?.MoveTo(Cursor.Position);
    _moveTimer.Start();

    try
    {
        _protoList.DoDragDrop(id!, DragDropEffects.Copy);
    }
    finally
    {
        _protoList.GiveFeedback -= ProtoList_GiveFeedback;
        _moveTimer?.Stop();
        _moveTimer?.Dispose();
        _moveTimer = null;
        _dragGhost?.Close();
        _dragGhost?.Dispose();
        _dragGhost = null;
    }
}

private void ProtoList_GiveFeedback(object? sender, GiveFeedbackEventArgs e)
{
    // e.Effect — это то, что выставил DragOver текущей цели под курсором.
    // Copy -> над валидной зоной (канвас и т.п.): оставляем системный курсор,
    // чтобы был виден нормальный значок "вставки".
    // None -> над невалидной зоной: системный курсор дал бы "запрет",
    // поэтому подменяем на обычную стрелку.
    e.UseDefaultCursors = e.Effect == DragDropEffects.Copy;
    if (e.Effect != DragDropEffects.Copy)
        Cursor.Current = Cursors.Default;

    _dragGhost?.MoveTo(Cursor.Position);
}

private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Size> _rsiFrameSizeCache = new();

private Size GetRsiFrameSize(string texturePath, Image fallbackImage)
{
    string dir = Path.GetDirectoryName(texturePath) ?? "";
    if (_rsiFrameSizeCache.TryGetValue(dir, out var cached)) return cached;

    Size result = new Size(Math.Min(32, fallbackImage.Width), Math.Min(32, fallbackImage.Height));
    try
    {
        string metaPath = Path.Combine(dir, "meta.json");
        if (File.Exists(metaPath))
        {
            var json = File.ReadAllText(metaPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("size", out var sizeElem) &&
                sizeElem.TryGetProperty("x", out var xEl) &&
                sizeElem.TryGetProperty("y", out var yEl))
            {
                result = new Size(xEl.GetInt32(), yEl.GetInt32());
            }
        }
    }
    catch { }

    // ConcurrentDictionary — потокобезопасен по определению, но индексатор-присвоение
    // здесь тоже безопасно (просто последний писатель победит при гонке на один и тот
    // же dir, без исключений и без повреждения структуры словаря, в отличие от Dictionary)
    _rsiFrameSizeCache[dir] = result;
    return result;
}    private Image? GetPrototypeIcon(string protoId)
    => GetPrototypeIcon(protoId, stateOverride: null);

    /// <summary>
    /// Загружает иконку прототипа. Если stateOverride != null — использует
    /// это состояние вместо того, что в YAML / дефолтного ("closed").
    /// </summary>
    private Image? GetPrototypeIcon(string protoId, string? stateOverride)
    {
        try
        {
            var layers = _indexer.GetLayers(protoId);

            // EntityStorageVisuals: шкафчики/локеры переопределяют state слоёв спрайта
            // (Base → stateBaseClosed, Door → stateDoorClosed). Без подстановки все шкафы
            // рисовались бы generic/generic_door, хотя у каждого свой набор текстур в RSI.
            var storageViz = _indexer.GetEntityStorageVisuals(protoId);
            string? ResolveLayerState(Models.SpriteLayer layer, int layerIndex)
            {
                if (storageViz == null) return layer.State;

                bool isBase;
                bool isDoor;
                if (!string.IsNullOrEmpty(layer.MapKey))
                {
                    // По map-ключу: enum.StorageVisualLayers.Base / .Door
                    isBase = layer.MapKey.Contains("Base");
                    isDoor = layer.MapKey.Contains("Door");
                }
                else
                {
                    // Слои без map: первый слой = база (корпус), второй со state-дверью.
                    // (SuitStorageBase: "base" без map первым, потом "door" без map)
                    bool first = layerIndex == 0;
                    bool second = layerIndex == 1;
                    isBase = first && !second;
                    isDoor = !first && second;
                }

                string? ov = isBase ? storageViz.Value.baseClosed : isDoor ? storageViz.Value.doorClosed : null;
                return !string.IsNullOrEmpty(ov) ? ov : layer.State;
            }

            // Многослойные прототипы (Sprite.layers) показываем КОМПОЗИЦИЕЙ всех видимых
            // слоёв друг поверх друга — так же, как они рисуются на канвасе в Renderer
            // (см. DrawLayeredTexturedRect), чтобы превью в панели совпадало с рендером.
            // Раньше брался только первый видимый слой, из-за чего часть во «меню спавна»
            // выглядела иначе и «пропадали» слои/state.
            if (layers.Count > 0)
            {
                var visibleLayers = layers.Where(l => l.Visible).ToList();
                if (visibleLayers.Count == 0) return null;

                // Загружаем текстуры всех видимых слоёв (state-слои), как в рендере
                var loadedLayers = new List<(Image Tex, Size Frame)>();
                try
                {
                    foreach (var layer in visibleLayers)
                    {
                        var lp = _indexer.GetLayerTexturePath(protoId, layer.SpritePath, layer.RsiPath, ResolveLayerState(layer, visibleLayers.IndexOf(layer)), stateOverride);
                        if (lp == null || !File.Exists(lp)) continue;
                        var tex = Image.FromFile(lp);
                        loadedLayers.Add((tex, GetRsiFrameSize(lp, tex)));
                    }

                    if (loadedLayers.Count == 0) return null;

                    // Холст строится по пропорции ПЕРВОГО успешно загруженного слоя
                    // (кадр сущности у всех слоёв почти всегда совпадает по размеру)
                    var firstFrame = loadedLayers[0].Frame;
                    float ratio = (float)firstFrame.Width / Math.Max(1, firstFrame.Height);
                    int iconW, iconH;
                    if (ratio >= 1.0f)
                    {
                        iconW = 32;
                        iconH = Math.Max(1, (int)(32f / ratio));
                    }
                    else
                    {
                        iconH = 32;
                        iconW = Math.Max(1, (int)(32f * ratio));
                    }

                    var icon = new Bitmap(iconW, iconH);
                    using (var g = Graphics.FromImage(icon))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        // Слои рисуются поверх друг друга в порядке YAML (нижний первым)
                        foreach (var (tex, frame) in loadedLayers)
                        {
                            var srcRect = new Rectangle(0, 0, frame.Width, frame.Height);
                            g.DrawImage(tex, new Rectangle(0, 0, iconW, iconH), srcRect, GraphicsUnit.Pixel);
                        }
                    }
                    return icon;
                }
                finally
                {
                    foreach (var (tex, _) in loadedLayers)
                        tex.Dispose();
                }
            }

            // Однослойный прототип
            var path = _indexer.GetFullTexturePath(protoId, stateOverride);
            if (path != null && File.Exists(path))
            {
                using var original = Image.FromFile(path);
                var frameSize = GetRsiFrameSize(path, original);

                // Scale to fit within 32x32 while preserving aspect ratio
                float ratio = (float)frameSize.Width / frameSize.Height;
                int iconW, iconH;
                if (ratio >= 1.0f)
                {
                    iconW = 32;
                    iconH = Math.Max(1, (int)(32f / ratio));
                }
                else
                {
                    iconH = 32;
                    iconW = Math.Max(1, (int)(32f * ratio));
                }

                var icon = new Bitmap(iconW, iconH);
                using (var g = Graphics.FromImage(icon))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    var srcRect = new Rectangle(0, 0, frameSize.Width, frameSize.Height);
                    g.DrawImage(original, new Rectangle(0, 0, iconW, iconH), srcRect, GraphicsUnit.Pixel);
                }
                return icon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки иконки для {protoId}: {ex.Message}");
        }

        return null;
    }

    private void LoadDoorComboIcons()
    {
        _doorIconAirlock?.Dispose();
        _doorIconAirlockGlass?.Dispose();
        _doorIconAirlock = GetPrototypeIcon("Airlock");
        _doorIconAirlockGlass = GetPrototypeIcon("AirlockGlass");
        if (_doorToolCombo != null && !_doorToolCombo.IsDisposed) _doorToolCombo.Invalidate();
        if (_btnPlaceHolopad != null && !_btnPlaceHolopad.IsDisposed)
        {
            _holopadIcon?.Dispose();
            _holopadIcon = GetPrototypeIcon("Holopad");
            _btnPlaceHolopad.Image = _holopadIcon;
            _btnPlaceHolopad.Invalidate();
        }
        if (_btnPlaceLocker != null && !_btnPlaceLocker.IsDisposed)
        {
            _lockerIcon?.Dispose();
            // Иконка кнопки — шкаф комнаты General (дефолтный прототип шкафчика,
            // прописанный в свойствах типа General), чтобы кнопка показывала ту же
            // текстуру, что ставит инструмент в General-комнаты.
            var generalLocker = _roomTypeManager?.GetRoomType("General")?.LockerProto;
            _lockerIcon = GetPrototypeIcon(!string.IsNullOrEmpty(generalLocker) ? generalLocker : "Closet");
            _btnPlaceLocker.Image = _lockerIcon;
            _btnPlaceLocker.Invalidate();
        }
        if (_btnPlaceSuitStorage != null && !_btnPlaceSuitStorage.IsDisposed)
        {
            _suitStorageIcon?.Dispose();
            // Иконка кнопки — базовое хранилище скафандра
            var generalSuit = _roomTypeManager?.GetRoomType("General")?.SuitStorageProto;
            _suitStorageIcon = GetPrototypeIcon(!string.IsNullOrEmpty(generalSuit) ? generalSuit : "SuitStorageBase");
            _btnPlaceSuitStorage.Image = _suitStorageIcon;
            _btnPlaceSuitStorage.Invalidate();
        }
        if (_btnPlaceBed != null && !_btnPlaceBed.IsDisposed)
        {
            _bedIcon?.Dispose();
            var generalBed = _roomTypeManager?.GetRoomType("General")?.BedProto;
            _bedIcon = GetPrototypeIcon(!string.IsNullOrEmpty(generalBed) ? generalBed : "Bed");
            _btnPlaceBed.Image = _bedIcon;
            _btnPlaceBed.Invalidate();
        }
        if (_btnPlaceCamera != null && !_btnPlaceCamera.IsDisposed)
        {
            _cameraIcon?.Dispose();
            // Иконка — камера того отдела, что привязана к комнате General (общая сеть).
            var generalCamera = _roomTypeManager?.GetRoomType("General")?.CameraProto;
            _cameraIcon = GetPrototypeIcon(!string.IsNullOrEmpty(generalCamera) ? generalCamera : "SurveillanceCameraGeneral");
            _btnPlaceCamera.Image = _cameraIcon;
            _btnPlaceCamera.Invalidate();
        }
        if (_btnPlaceDresser != null && !_btnPlaceDresser.IsDisposed)
        {
            _dresserIcon?.Dispose();
            // Комоды привязаны только к комнатам глав, поэтому иконка — комод одного
            // из них (капитан), а не «общий»: у General своего комода нет.
            var captainDresser = _roomTypeManager?.GetRoomType("Captain")?.DresserProto;
            _dresserIcon = GetPrototypeIcon(!string.IsNullOrEmpty(captainDresser) ? captainDresser : "DresserFilled");
            _btnPlaceDresser.Image = _dresserIcon;
            _btnPlaceDresser.Invalidate();
        }
        if (_btnPlaceIntercom != null && !_btnPlaceIntercom.IsDisposed)
        {
            _intercomIcon?.Dispose();
            // Интерком подбирается по отделу комнаты, поэтому на иконке — общий
            // (Common) вариант: он же достаётся комнатам без отдела.
            var generalIntercom = _roomTypeManager?.GetRoomType("General")?.IntercomProto;
            _intercomIcon = GetPrototypeIcon(!string.IsNullOrEmpty(generalIntercom) ? generalIntercom : "IntercomCommon");
            _btnPlaceIntercom.Image = _intercomIcon;
            _btnPlaceIntercom.Invalidate();
        }
        if (_btnPlaceFax != null && !_btnPlaceFax.IsDisposed)
        {
            _faxIcon?.Dispose();
            // Факс по умолчанию — обычный FaxMachineBase, поэтому на иконке он же
            // (отдельный прототип есть только у рубки капитана).
            var bridgeFax = _roomTypeManager?.GetRoomType("Bridge")?.FaxProto;
            _faxIcon = GetPrototypeIcon(!string.IsNullOrEmpty(bridgeFax) ? bridgeFax : "FaxMachineBase");
            _btnPlaceFax.Image = _faxIcon;
            _btnPlaceFax.Invalidate();
        }
    }
    
    private Image? GetCachedProtoIcon(string protoId)
    {
        if (_protoIconCache.TryGetValue(protoId, out var cached))
            return cached; // уже загружена (или уже точно известно, что иконки нет — null тоже кэшируется)

        // Ещё не грузили и сейчас не грузится — запускаем фоновую загрузку и возвращаем
        // null на этот кадр отрисовки (DrawItem нарисует заглушку вместо иконки)
        if (_iconLoadInFlight.TryAdd(protoId, 0))
        {
            _ = Task.Run(async () =>
            {
                await _iconLoadSemaphore.WaitAsync();
                try
                {
                    var icon = GetPrototypeIcon(protoId);
                    _protoIconCache[protoId] = icon;
                }
                finally
                {
                    _iconLoadSemaphore.Release();
                    _iconLoadInFlight.TryRemove(protoId, out _);
                    _pendingIconRedraw = true;
                }
            });
        }

        return null;
    }

    private void ClearProtoIconCache()
    {
        foreach (var kvp in _protoIconCache)
            kvp.Value?.Dispose();
        _protoIconCache.Clear();
        _iconLoadInFlight.Clear();
        _rsiFrameSizeCache.Clear();
        // После переиндексации репозитория старые результаты фильтрации недействительны
        _searchFilterCache.Clear();
    }
    private void ProtoList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        AppTheme.DrawItemBackground(e.Graphics, e.Bounds,
            (e.State & DrawItemState.Selected) == DrawItemState.Selected);

        string? id = _protoList.Items[e.Index]?.ToString();
        const int iconSize = 32;
        const int padding = 3;
        int textX = e.Bounds.Left + padding + iconSize + padding;

        bool isRealProto = !string.IsNullOrEmpty(id) &&
                            !id.StartsWith("(") && !id.StartsWith("⚠") &&
                            !id.StartsWith("⏳") && !id.StartsWith("Ошибка") &&
                            !id.StartsWith("Нажмите");

        // В ListBox.Items по-прежнему лежит настоящий id (drag&drop, выбор,
        // ArmPrototypePlacement и т.п. завязаны именно на него) — здесь подменяем
        // ТОЛЬКО отображаемый текст на русское название, если оно нашлось и содержит
        // кириллицу. При этом под локализованным именем второй строкой рисуется сам id.
        bool hasLocalizedName = false;
        string? displayName = null;
        if (isRealProto)
        {
            var proto = _indexer.FindPrototype(id!);
            var localizedName = proto?.LocalizedName;
            hasLocalizedName = !string.IsNullOrWhiteSpace(localizedName) &&
                               localizedName.Any(c => c >= '\u0400' && c <= '\u04FF');

            // Суффикс показываем в квадратных скобках, как редактор самой игры
            // ("Name [Suffix]", см. EntityPrototype.EditorSuffix): иначе варианты
            // одного прототипа (GlassBoxLaser / GlassBoxLaserOpen / ...FilledOpen)
            // в списке выглядели одинаково — «стеклянный короб».
            displayName = hasLocalizedName && !string.IsNullOrWhiteSpace(proto!.LocalizedSuffix)
                ? $"{localizedName} [{proto.LocalizedSuffix}]"
                : localizedName;
        }

        if (isRealProto)
        {
            var icon = GetCachedProtoIcon(id!);
            if (icon != null)
            {
                // Icon already has correct aspect ratio from loading, just draw it centered
                int drawX = e.Bounds.Left + padding + (iconSize - icon.Width) / 2;
                int drawY = e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2;
                e.Graphics.DrawImage(icon, drawX, drawY, icon.Width, icon.Height);
            }
            else
            {
                using var placeholderBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
                int iconY = e.Bounds.Top + (e.Bounds.Height - iconSize) / 2;
                e.Graphics.FillRectangle(placeholderBrush, e.Bounds.Left + padding, iconY, iconSize, iconSize);
            }
        }
        else
        {
            textX = e.Bounds.Left + padding;
        }

        // Двухстрочный вариант: сверху локализованное имя, под ним — настоящий id.
        // Для прототипов без локализации (или служебных пунктов) — одна строка по центру.
        if (hasLocalizedName)
        {
            var nameFont = new Font(e.Font.FontFamily, 12, e.Font.Style);
            var idFont = new Font(e.Font.FontFamily, 9, e.Font.Style);

            int line1H = 22;
            int line2H = 18;
            var r1 = new Rectangle(textX, e.Bounds.Top + 3, e.Bounds.Right - textX, line1H);
            var r2 = new Rectangle(textX, e.Bounds.Top + 3 + line1H, e.Bounds.Right - textX, line2H);

            TextRenderer.DrawText(e.Graphics, displayName!, nameFont, r1, e.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, id!, idFont, r2, Color.Gray,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
        else
        {
            string? displayText = id;
            var textFont = new Font(e.Font.FontFamily, 12, e.Font.Style);
            TextRenderer.DrawText(e.Graphics, displayText, textFont,
                new Rectangle(textX, e.Bounds.Top, e.Bounds.Right - textX, e.Bounds.Height),
                e.ForeColor, TextFormatFlags.VerticalCenter);
        }

        e.DrawFocusRectangle();
    }
    private void ArmPrototypePlacement()
    {
        if (_protoList.SelectedItem == null) return;
        string? id = _protoList.SelectedItem.ToString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

        _protoToPlace = id;
        _toolManager.ForceSetTool(ToolManager.Tool.PlacePrototype);
    }
}
