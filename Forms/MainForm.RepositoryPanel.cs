using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

    // === ПАНЕЛЬ РЕПОЗИТОРИЕВ ===
    private void CreateRepositoryPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None
        };

        var rightLine = new Panel
        {
            Location = new Point(panel.Width - 1, 0),
            Width = 1,
            Height = panel.Height,
            BackColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
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
            IntegralHeight = false
        };
        _protoList.DoubleClick += OnPrototypeDoubleClick;
        listContainer.Controls.Add(_protoList);
        panel.Controls.Add(listContainer);

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
        _searchBox.KeyUp += (s, e) => UpdatePrototypeList(_searchBox.Text);
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
                _btnPlaceProto.BackColor = Color.FromArgb(255, 245, 200);
                _btnPlaceProto.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }
            else
            {
                _btnPlaceProto.Text = "🔒";
                _btnPlaceProto.BackColor = Color.FromArgb(220, 220, 220);
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
            BackColor = _snapEntityToCenter ? Color.LightGreen : Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        _btnSnapEntityCenter.Click += (s, e) =>
        {
            _snapEntityToCenter = !_snapEntityToCenter;
            _btnSnapEntityCenter.BackColor = _snapEntityToCenter ? Color.LightGreen : Color.White;
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
            BackColor = _snapEntityRotation ? Color.LightGreen : Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnEntityRotationSnap.Click += (s, e) =>
        {
            _snapEntityRotation = !_snapEntityRotation;
            _btnEntityRotationSnap.BackColor = _snapEntityRotation ? Color.LightGreen : Color.White;

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
                _typeLabel.Text = $"Размещение: {_protoToPlace}  (клик — поставить)";
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
            Text = "Репозитории",
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
            _indexer.ReindexFromDisk(repo);
            int count = _indexer.GetPrototypeIds().Count;
            _repoManager.MarkAsIndexed(repo.Id, count);
            UpdateRepoSelector();

            UpdateDoorIcons();

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

        Task.Run(() =>
        {
            try
            {
                var allIds = string.IsNullOrEmpty(filter) || filter == "Поиск прототипов..."
                    ? _indexer.GetPrototypeIds()
                    : _indexer.SearchPrototypes(filter);

                if (token.IsCancellationRequested) return;

                var filteredIds = allIds;

                switch (_currentFilter)
                {
                    case "all":
                        // Декали по умолчанию скрыты из общего списка — слишком много "мусора"
                        // (сотни BrickTile*, RoadLine* и т.п.), видны только через отдельный фильтр
                        filteredIds = allIds.Where(id =>
                            _indexer.FindPrototype(id)?.Type != "decal"
                        ).ToList();
                        break;
                    case "тайл":
                    case "tiles":
                        filteredIds = allIds.Where(id =>
                            id.Contains("tile", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("floor", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("plating", StringComparison.OrdinalIgnoreCase)
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
                    .ToList();

                var result = filteredIds.Take(100000).ToList();

                if (token.IsCancellationRequested) return;

                _protoList.Invoke(() =>
                {
                    _protoList.Items.Clear();
                    if (result.Count == 0)
                        _protoList.Items.Add("(нет прототипов)");
                    else
                        foreach (var id in result)
                            _protoList.Items.Add(id);
                });
            }
            catch (Exception ex)
            {
                _protoList.Invoke(() =>
                {
                    _protoList.Items.Clear();
                    _protoList.Items.Add($"Ошибка: {ex.Message}");
                });
            }
        }, token);
    }


    private void OnPrototypeDoubleClick(object? sender, EventArgs e)
    {
        if (_protoList.SelectedItem == null) return;
        string? id = _protoList.SelectedItem.ToString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("(")) return;

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


    private void LoadDoorIcons()
    {
        if (_btnAirlock != null)
        {
            var icon = GetPrototypeIcon("Airlock");
            if (icon != null)
            {
                _btnAirlock.Image = icon;
                _btnAirlock.Text = "";
            }
            else
            {
                _btnAirlock.Text = "🚪";
                _btnAirlock.TextAlign = ContentAlignment.MiddleCenter;
                _btnAirlock.Font = new Font("Segoe UI", 16);
            }
        }

        if (_btnAirlockGlass != null)
        {
            var icon = GetPrototypeIcon("AirlockGlass");
            if (icon != null)
            {
                _btnAirlockGlass.Image = icon;
                _btnAirlockGlass.Text = "";
            }
            else
            {
                _btnAirlockGlass.Text = "🔲";
                _btnAirlockGlass.TextAlign = ContentAlignment.MiddleCenter;
                _btnAirlockGlass.Font = new Font("Segoe UI", 16);
            }
        }
    }


    private Image? GetPrototypeIcon(string protoId)
    {
        try
        {
            var path = _indexer.GetFullTexturePath(protoId);
            if (path != null && File.Exists(path))
            {
                using var original = Image.FromFile(path);
                var icon = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(icon))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, new Rectangle(0, 0, 32, 32));
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


    private void UpdateDoorIcons()
    {
        if (_btnAirlock != null)
        {
            var icon = GetPrototypeIcon("Airlock");
            if (icon != null)
            {
                _btnAirlock.Image = icon;
                _btnAirlock.Text = "";
            }
        }

        if (_btnAirlockGlass != null)
        {
            var icon = GetPrototypeIcon("AirlockGlass");
            if (icon != null)
            {
                _btnAirlockGlass.Image = icon;
                _btnAirlockGlass.Text = "";
            }
        }
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
