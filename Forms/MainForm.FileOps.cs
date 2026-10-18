// Forms/MainForm.FileOps.cs

using MapperIce.Models;
using MapperIce.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MapperIce.Forms;

public partial class MainForm
{

    // === ФАЙЛОВЫЕ ОПЕРАЦИИ ===
    private void LoadMapFromYAML()
    {
        using var dialog = new OpenFileDialog();
        dialog.Filter = "YAML files (*.yml;*.yaml)|*.yml;*.yaml";
        
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var loader = new YAMLLoader(_indexer, _drawDepthManager);
            var loadedMap = loader.LoadFromFile(dialog.FileName);

            // Запоминаем путь открытого YAML — Ctrl+S будет экспортировать в него же
            _currentYamlPath = dialog.FileName;
            
            // Определяем смещение UID — все загруженные слои получат новые UID
            // чтобы не конфликтовать с существующими
            int existingMaxUid = _map.Grids.Any() ? _map.Grids.Max(g => g.Uid) : 0;
            int uidOffset = existingMaxUid + 1;

            for (int i = 0; i < loadedMap.Grids.Count; i++)
            {
                var grid = loadedMap.Grids[i].Clone();
                grid.Uid = uidOffset + i;
                grid.Name = $"Слой {i + 1}";
                // Смещение слоя рассчитывается автоматически через Grid.GetLayerOffsetY
                grid.Position = PointF.Empty;
                _map.Grids.Add(grid);
            }

            // Активируем первый загруженный слой
            if (loadedMap.Grids.Any())
            {
                _map.ActiveGridUid = loadedMap.Grids.First().Uid;
            }
            
            // Обновляем UI
            InitGridTabs();
            UpdateTileGrid();
            
            // Если есть активный грид, пересчитываем паттерны
            if (_map.ActiveGrid != null)
            {
                RecalculateDecalPatterns();
                SaveState();
            }
            
            Render();
            MessageBox.Show($"Карта загружена!\nСлоёв: {_map.Grids.Count}\nТайлов: {_map.ActiveGrid?.Tiles.Count ?? 0}\nСущностей: {_map.ActiveGrid?.Entities.Count ?? 0}\nДекалей: {_map.ActiveGrid?.Decals.Count ?? 0}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки карты: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.ToString());
        }
    }


    private void ExportToYAML()
    {
        if (_map.ActiveGrid == null)
        {
            MessageBox.Show("Нет активного слоя");
            return;
        }

        var g = _map.ActiveGrid;
        if (g.Rooms.Count == 0 && g.Entities.Count == 0 && g.Tiles.Count == 0 && g.Decals.Count == 0)
        {
            MessageBox.Show("Слой пуст — нечего экспортировать");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "YAML files (*.yml)|*.yml",
            DefaultExt = "yml",
            FileName = "map.yml"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExportToYamlPath(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Экспортирует активный слой в указанный YAML-файл и запоминает путь, чтобы
    /// Ctrl+S сохранял в него же. Общая логика для «Экспорт в YAML» и быстрого
    /// сохранения в открытый .yml.
    /// </summary>
    private void ExportToYamlPath(string filePath)
    {
        // Одно-гридовый экспорт — только если слоёв один И нет областей разделения
        // (иначе область при экспорте должна уйти в отдельный грид).
        bool hasDividerAreas = _map.Grids.Any(g =>
            g.DividerAreas != null && g.DividerAreas.Count > 0 && g.DividerAreas.Any(a => !a.IsEmpty));

        var yaml = _map.Grids.Count <= 1 && !hasDividerAreas
            ? YAMLGenerator.Generate(_map.ActiveGrid, _tileBuilder, _pipeLayers, _alarmSettings)
            : YAMLGenerator.GenerateAll(_map.Grids, _tileBuilder, _pipeLayers, _alarmSettings);
        File.WriteAllText(filePath, yaml);
        _currentYamlPath = filePath;

        // Экспорт в YAML-источник — это сохранение работы, а не создание нового
        // проекта: сбрасываем привязку к .ice, чтобы Ctrl+S не уводило обратно туда
        ToastNotification.Show(this, $"Сохранено в YAML: {Path.GetFileName(filePath)}", "MapperIce", 2500);
    }

    /// <summary>
    /// Рендерит всю карту (все видимые слои) в PNG-«скриншот» без интерфейса:
    /// пол, стены, двери, декали и сущности, трубы/провода как реальные спрайты.
    /// Аналитические оверлеи (цвет комнат, связи, обзор камер) в картинку не попадают.
    /// </summary>
    private void RenderToPng()
    {
        if (_map.Grids.Count == 0)
        {
            MessageBox.Show("Нет слоёв для рендера");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = "png",
            FileName = "map.png"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                using var bitmap = _renderer.RenderFullMapPng(_map);
                if (bitmap == null)
                {
                    MessageBox.Show("На карте нет содержимого для рендера");
                    return;
                }
                bitmap.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            ToastNotification.Show(this, $"Рендер сохранён: {Path.GetFileName(dialog.FileName)}", "MapperIce", 2500);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка рендера: {ex.Message}");
        }
    }


    private void SaveProject()
    {
        if (_map.Grids.Count == 0)
        {
            MessageBox.Show("Нет слоёв для сохранения");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Project files (*.ice)|*.ice",
            DefaultExt = "ice",
            FileName = _currentProjectPath != null ? Path.GetFileName(_currentProjectPath) : "project.ice"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            WriteProjectToFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }

    /// <summary>
    /// Быстрое сохранение по Ctrl+S. Если карта открыта из YAML — пишет в тот же
    /// .yml/.yaml. Если открыт .ice — пишет файл рядом с исходным, заменяя префикс
    /// даты «день_месяц_год_» на сегодняшний. Если файла ещё нет — диалог «Сохранить как».
    /// </summary>
    private void QuickSaveProject()
    {
        if (_map.Grids.Count == 0)
        {
            MessageBox.Show("Нет слоёв для сохранения");
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(_currentYamlPath))
            {
                ExportToYamlPath(_currentYamlPath);
                return;
            }

            if (_currentProjectPath == null)
            {
                SaveProject();
                return;
            }

            WriteProjectToFile(GetDatePrefixedPath(_currentProjectPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает путь к файлу рядом с исходным, где префикс даты «день_месяц_год_»
    /// заменён на сегодняшний: C:\...\16_10_2026_map.ice → C:\...\18_10_2026_map.ice.
    /// Старый префикс снимается, поэтому дата не накапливается в имени. Если дата
    /// уже сегодняшняя — путь не меняется и Ctrl+S перезаписывает тот же файл.
    /// </summary>
    private static readonly Regex DatePrefixRegex = new(@"^\d{2}_\d{2}_\d{4}_", RegexOptions.Compiled);

    private static string GetDatePrefixedPath(string path)
    {
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        var bareName = DatePrefixRegex.Replace(name, "");
        if (bareName.Length == 0) bareName = name;
        var newName = $"{DateTime.Now:dd_MM_yyyy}_{bareName}";
        return string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);
    }

    /// <summary>
    /// Настройки JSON для .ice: одни и те же при записи и чтении. Конвертер цвета
    /// обязателен — System.Text.Json не умеет читать System.Drawing.Color сам
    /// (получался Color.Empty, из-за чего терялся цвет труб).
    /// </summary>
    private static readonly JsonSerializerOptions ProjectJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ArgbColorJsonConverter() }
    };

    /// <summary>
    /// Сериализует проект и записывает его в указанный путь. Отдельно от UI-части
    /// (диалоги/сообщения), чтобы и «Сохранить как», и Ctrl+S переиспользовали одну
    /// и ту же логику. Обновляет _currentProjectPath.
    /// </summary>
    private void WriteProjectToFile(string filePath)
    {
        var grid = _map.ActiveGrid;

        var data = new ProjectData
        {
            LastSaved = DateTime.Now,
            ActiveGridName = grid?.Name
        };

        foreach (var g in _map.Grids)
        {
            var gridData = new GridData
            {
                Uid = g.Uid,
                Name = g.Name,
                PositionX = g.Position.X,
                PositionY = g.Position.Y,
                IsVisible = g.IsVisible,
                Color = $"{g.Color.A},{g.Color.R},{g.Color.G},{g.Color.B}",
                IsStaticGrid = g.IsStaticGrid,
                ExteriorWallProto = g.ExteriorWallProto,
                ReplaceExteriorWalls = g.ReplaceExteriorWalls
            };

            // Области разделения («Разделитель гридов») — потайлово
            foreach (var area in g.DividerAreas)
            {
                var areaData = new GridDividerAreaData
                {
                    Cells = area.Cells
                        .OrderBy(c => c.Y).ThenBy(c => c.X)
                        .Select(c => new GridDividerCellData { X = c.X, Y = c.Y })
                        .ToList()
                };
                gridData.DividerAreas.Add(areaData);
            }

            // Комнаты и двери
            foreach (var room in g.Rooms)
            {
                var roomData = new RoomGridData
                {
                    X = room.X,
                    Y = room.Y,
                    Width = room.Width,
                    Height = room.Height,
                    RoomType = room.RoomType,
                    WallProto = room.WallProto,
                    FloorProto = room.FloorProto,
                    DoorProto = room.DoorProto,
                    GlassDoorProto = room.GlassDoorProto,
                    AirAlarmProto = room.AirAlarmProto,
                    FireAlarmProto = room.FireAlarmProto,
                    HolopadProto = room.HolopadProto,
                    LockerProto = room.LockerProto,
                    SuitStorageProto = room.SuitStorageProto,
                    BedProto = room.BedProto,
                    BedsheetProto = room.BedsheetProto,
                    CameraProto = room.CameraProto,
                    DresserProto = room.DresserProto,
                    IntercomProto = room.IntercomProto,
                    FaxName = room.FaxName,
                    FaxProto = room.FaxProto,
                    FillColor = $"{room.FillColor.A},{room.FillColor.R},{room.FillColor.G},{room.FillColor.B}",
                    LineColor = $"{room.LineColor.A},{room.LineColor.R},{room.LineColor.G},{room.LineColor.B}",
                    DecalMode = room.DecalMode,
                    HasCustomDecalRule = room.HasCustomDecalRule,
                    Priority = room.Priority,
                    Id = room.Id,
                };

                // RemovedCells как список строк "x,y"
                foreach (var cell in room.RemovedCells)
                {
                    roomData.RemovedCells.Add($"{cell.X},{cell.Y}");
                }

                // Проходы (Passages) и окна (Windows) как список строк "x,y"
                foreach (var p in room.Passages)
                {
                    roomData.Passages.Add($"{p.X},{p.Y}");
                }
                foreach (var w in room.Windows)
                {
                    roomData.Windows.Add($"{w.X},{w.Y}");
                }

                foreach (var door in room.Doors)
                {
                    roomData.Doors.Add(new DoorData
                    {
                        X = door.X,
                        Y = door.Y,
                        Proto = door.Proto
                    });
                }

                // AutoDecalRule (DecalRuleSet → DecalRuleData)
                if (room.AutoDecalRule != null && room.AutoDecalRule.Layers.Count > 0)
                {
                    roomData.AutoDecalRule = new DecalRuleData
                    {
                        Layers = room.AutoDecalRule.Layers.Select(l => new DecalLayerData
                        {
                            Name = l.Name,
                            SourcePackId = l.SourcePackId,
                            Enabled = l.Enabled,
                            Color = l.Color,
                            Mode = l.Mode,
                            ManualAreas = l.ManualAreas.Select(a => new ManualDecalAreaData
                            {
                                X = a.X, Y = a.Y, Width = a.Width, Height = a.Height, Mode = a.Mode,
                                Subtractions = a.Subtractions.Select(s => new DecalAreaRectData
                                {
                                    X = s.X, Y = s.Y, Width = s.Width, Height = s.Height
                                }).ToList(),
                                CutCells = a.CutCells.Select(c => $"{c.X},{c.Y}").ToList(),
                                RemovedCells = a.RemovedCells.Select(c => $"{c.X},{c.Y}").ToList()
                            }).ToList()
                        }).ToList()
                    };
                }

                // ManualDecalAreas
                foreach (var area in room.ManualDecalAreas)
                {
                    roomData.ManualDecalAreas.Add(new ManualDecalAreaData
                    {
                        X = area.X, Y = area.Y, Width = area.Width, Height = area.Height, Mode = area.Mode,
                        Subtractions = area.Subtractions.Select(s => new DecalAreaRectData
                        {
                            X = s.X, Y = s.Y, Width = s.Width, Height = s.Height
                        }).ToList(),
                        CutCells = area.CutCells.Select(c => $"{c.X},{c.Y}").ToList(),
                        RemovedCells = area.RemovedCells.Select(c => $"{c.X},{c.Y}").ToList()
                    });
                }

                gridData.Rooms.Add(roomData);
            }

            // Сущности (исключая Firelock)
            foreach (var entity in g.Entities.Where(e => e is not FirelockEntity))
            {
                gridData.Entities.Add(new GenericEntityData
                {
                    Type = entity.GetType().Name,
                    Data = System.Text.Json.JsonSerializer.SerializeToElement(entity, entity.GetType(), ProjectJsonOptions)
                });
            }

            gridData.Tiles = g.Tiles
                .Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto })
                .ToList();

            gridData.Decals = g.Decals
                .Select(d => new PlacedDecal { X = d.X, Y = d.Y, Proto = d.Proto, Color = d.Color, Rotation = d.Rotation, Cleanable = d.Cleanable, PatternOwnerId = d.PatternOwnerId, PatternLayerIndex = d.PatternLayerIndex })
                .ToList();

            gridData.LooseDoors = g.LooseDoors
                .Select(d => new DoorData { X = d.X, Y = d.Y, Proto = d.Proto })
                .ToList();

            data.Grids.Add(gridData);
        }

        // Настройки слоёв труб (цвет/вентиляция) — проектные, сохраняем вместе с
        // картой, иначе после загрузки .ice они вернутся к дефолтным
        data.PipeLayers = _pipeLayers.Values
            .Select(s => new PipeLayerData
            {
                Layer = s.Layer,
                Color = $"{s.Color.A},{s.Color.R},{s.Color.G},{s.Color.B}",
                HasColor = s.HasColor,
                VentProto = s.VentProto
            })
            .ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(data, ProjectJsonOptions);
        File.WriteAllText(filePath, json);

        _currentProjectPath = filePath;
        // Сохранение в .ice делает его источником работы — YAML-привязку снимаем
        _currentYamlPath = null;

        // Ненавязчивое уведомление об успешном сохранении
        ToastNotification.Show(this, $"Проект сохранён: {Path.GetFileName(filePath)}", "MapperIce", 2500);
    }


    private void LoadProject()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Project files (*.ice)|*.ice"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var data = System.Text.Json.JsonSerializer.Deserialize<ProjectData>(json, ProjectJsonOptions);

            if (data == null)
            {
                MessageBox.Show("Ошибка чтения файла");
                return;
            }

            // Запоминаем путь открытого файла — Ctrl+S будет сохранять в него же
            _currentProjectPath = dialog.FileName;
            // Источник теперь .ice, а не YAML
            _currentYamlPath = null;

            // Очищаем все текущие данные
            _map.Grids.Clear();
            _map.ActiveGridUid = null;

            // Слои труб восстанавливаем ДО отрисовки карты в конце метода, чтобы
            // трубы сразу рисовались сохранёнными цветами
            ApplyPipeLayers(data.PipeLayers);

            int totalRooms = 0;
            int totalDoors = 0;
            int totalEntities = 0;
            int totalDecals = 0;

            foreach (var gridData in data.Grids)
            {
                var grid = new Grid
                {
                    Uid = gridData.Uid,
                    Name = gridData.Name,
                    Position = new PointF(gridData.PositionX, gridData.PositionY),
                    IsVisible = gridData.IsVisible,
                    Color = ParseColor(gridData.Color),
                    IsStaticGrid = gridData.IsStaticGrid,
                    ExteriorWallProto = gridData.ExteriorWallProto,
                    ReplaceExteriorWalls = gridData.ReplaceExteriorWalls
                };

                // Области разделения («Разделитель гридов»): новый потайловый
                // формат Cells; старые проекты хранили AddRects/SubRects —
                // разворачиваем их в клетки (union(Add) минус union(Sub)).
                foreach (var areaData in gridData.DividerAreas)
                {
                    var area = new GridDividerArea();

                    if (areaData.Cells != null && areaData.Cells.Count > 0)
                    {
                        foreach (var cell in areaData.Cells)
                            area.AddCell(cell.X, cell.Y);
                    }
                    else
                    {
                        if (areaData.AddRects != null)
                            foreach (var r in areaData.AddRects)
                                area.AddRect(r.X, r.Y, r.Width, r.Height);
                        if (areaData.SubRects != null)
                            foreach (var r in areaData.SubRects)
                                area.RemoveRect(r.X, r.Y, r.Width, r.Height);
                    }

                    if (!area.IsEmpty)
                        grid.DividerAreas.Add(area);
                }

                // Комнаты
                foreach (var roomData in gridData.Rooms)
                {
                    var room = new Room
                    {
                        X = roomData.X,
                        Y = roomData.Y,
                        Width = roomData.Width,
                        Height = roomData.Height,
                        RoomType = roomData.RoomType,
                        WallProto = roomData.WallProto,
                        FloorProto = roomData.FloorProto,
                        DoorProto = roomData.DoorProto,
                        GlassDoorProto = roomData.GlassDoorProto ?? "AirlockGlass",
                        AirAlarmProto = roomData.AirAlarmProto,
                        FireAlarmProto = roomData.FireAlarmProto,
                        HolopadProto = roomData.HolopadProto,
                        LockerProto = roomData.LockerProto,
                        SuitStorageProto = roomData.SuitStorageProto,
                        BedProto = roomData.BedProto,
                        BedsheetProto = roomData.BedsheetProto,
                        CameraProto = roomData.CameraProto,
                        DresserProto = roomData.DresserProto,
                        IntercomProto = roomData.IntercomProto,
                        FaxName = roomData.FaxName,
                        FaxProto = roomData.FaxProto,
                        FillColor = ParseColor(roomData.FillColor),
                        LineColor = ParseColor(roomData.LineColor),
                        DecalMode = roomData.DecalMode,
                        HasCustomDecalRule = roomData.HasCustomDecalRule,
                        Priority = roomData.Priority,
                        RemovedCells = new HashSet<(int X, int Y)>()
                    };

                    // Восстанавливаем стабильный Id из проекта, чтобы декали узора
                    // (PlacedDecal.PatternOwnerId) снова совпали по ключу с комнатой.
                    room.RestoreId(roomData.Id);

                    // БЭКФИЛЛ привязок: проекты, сохранённые ДО появления фичи привязок
                    // (голопад/шкафчик/скафандр), могли записать эти поля пустыми, хотя
                    // у типа комнаты они заданы. Заполняем недостающие из типа комнаты,
                    // НЕ перезаписывая явные значения (например, кастомно убранный голопад).
                    // Применяем только для реально существующего типа: GetRoomType для
                    // неизвестного имени молча вернёт General, и не желаем прибивать его
                    // привязки к комнатам с удалённым кастомным типом.
                    if (_roomTypeManager.TypeExists(room.RoomType))
                    {
                        var rt = _roomTypeManager.GetRoomType(room.RoomType);
                        if (rt != null)
                        {
                            if (string.IsNullOrEmpty(room.HolopadProto) && !string.IsNullOrEmpty(rt.HolopadProto))
                                room.HolopadProto = rt.HolopadProto;
                            if (string.IsNullOrEmpty(room.LockerProto) && !string.IsNullOrEmpty(rt.LockerProto))
                                room.LockerProto = rt.LockerProto;
                            if (string.IsNullOrEmpty(room.SuitStorageProto) && !string.IsNullOrEmpty(rt.SuitStorageProto))
                                room.SuitStorageProto = rt.SuitStorageProto;
                            if (string.IsNullOrEmpty(room.BedProto) && !string.IsNullOrEmpty(rt.BedProto))
                                room.BedProto = rt.BedProto;
                            if (string.IsNullOrEmpty(room.BedsheetProto) && !string.IsNullOrEmpty(rt.BedsheetProto))
                                room.BedsheetProto = rt.BedsheetProto;
                            if (string.IsNullOrEmpty(room.CameraProto) && !string.IsNullOrEmpty(rt.CameraProto))
                                room.CameraProto = rt.CameraProto;
                            if (string.IsNullOrEmpty(room.DresserProto) && !string.IsNullOrEmpty(rt.DresserProto))
                                room.DresserProto = rt.DresserProto;
                            if (string.IsNullOrEmpty(room.IntercomProto) && !string.IsNullOrEmpty(rt.IntercomProto))
                                room.IntercomProto = rt.IntercomProto;
                            if (string.IsNullOrEmpty(room.FaxName) && !string.IsNullOrEmpty(rt.FaxName))
                                room.FaxName = rt.FaxName;
                            if (string.IsNullOrEmpty(room.FaxProto) && !string.IsNullOrEmpty(rt.FaxProto))
                                room.FaxProto = rt.FaxProto;
                        }
                    }

                    // Восстанавливаем RemovedCells
                    foreach (var cellStr in roomData.RemovedCells)
                    {
                        var parts = cellStr.Split(',');
                        if (parts.Length == 2)
                        {
                            room.RemovedCells.Add((int.Parse(parts[0]), int.Parse(parts[1])));
                        }
                    }

                    // Восстанавливаем проходы (Passages) и окна (Windows)
                    foreach (var cellStr in roomData.Passages)
                    {
                        var parts = cellStr.Split(',');
                        if (parts.Length == 2)
                        {
                            room.Passages.Add((int.Parse(parts[0]), int.Parse(parts[1])));
                        }
                    }
                    foreach (var cellStr in roomData.Windows)
                    {
                        var parts = cellStr.Split(',');
                        if (parts.Length == 2)
                        {
                            room.Windows.Add((int.Parse(parts[0]), int.Parse(parts[1])));
                        }
                    }

                    foreach (var doorData in roomData.Doors)
                    {
                        room.Doors.Add(new Door
                        {
                            X = doorData.X,
                            Y = doorData.Y,
                            Proto = doorData.Proto
                        });
                    }

                    // Восстанавливаем AutoDecalRule
                    if (roomData.AutoDecalRule != null)
                    {
                        room.AutoDecalRule = new DecalRuleSet
                        {
                            Layers = roomData.AutoDecalRule.Layers.Select(l => new DecalLayer
                            {
                                Name = l.Name,
                                SourcePackId = l.SourcePackId,
                                Enabled = l.Enabled,
                                Color = l.Color,
                                Mode = l.Mode,
                                ManualAreas = l.ManualAreas.Select(a => new ManualDecalArea
                                {
                                    X = a.X, Y = a.Y, Width = a.Width, Height = a.Height, Mode = a.Mode,
                                    Subtractions = a.Subtractions.Select(s => new DecalAreaRect
                                    {
                                        X = s.X, Y = s.Y, Width = s.Width, Height = s.Height
                                    }).ToList(),
                                    CutCells = ParseCutCells(a.CutCells),
                                    RemovedCells = ParseCutCells(a.RemovedCells)
                                }).ToList()
                            }).ToList()
                        };
                    }

                    // Восстанавливаем ManualDecalAreas
                    foreach (var areaData in roomData.ManualDecalAreas)
                    {
                        room.ManualDecalAreas.Add(new ManualDecalArea
                        {
                            X = areaData.X, Y = areaData.Y, Width = areaData.Width, Height = areaData.Height, Mode = areaData.Mode,
                            Subtractions = areaData.Subtractions.Select(s => new DecalAreaRect
                            {
                                X = s.X, Y = s.Y, Width = s.Width, Height = s.Height
                            }).ToList(),
                            CutCells = ParseCutCells(areaData.CutCells),
                            RemovedCells = ParseCutCells(areaData.RemovedCells)
                        });
                    }

                    grid.Rooms.Add(room);
                    totalRooms++;
                    totalDoors += roomData.Doors.Count;
                }

                // Сущности
                foreach (var entityData in gridData.Entities)
                {
                    if (!EntityTypeRegistry.TryGetType(entityData.Type, out var type))
                        continue;

                    try
                    {
                        var restored = JsonSerializer.Deserialize(entityData.Data.GetRawText(), type, ProjectJsonOptions);
                        if (restored is MapEntity mapEntity)
                        {
                            grid.Entities.Add(mapEntity);
                            totalEntities++;
                        }
                    }
                    catch { /* повреждённая запись — пропускаем */ }
                }

                // БЭКФИЛЛ цвета труб: проекты, сохранённые до появления
                // ArgbColorJsonConverter, содержат цвет трубы как Color.Empty (A=0) —
                // System.Text.Json не умел читать System.Drawing.Color и отдавал нули.
                // Это потерянный цвет: возвращаем трубе цвет её слоя — ровно то, что
                // запекает простановка (PipeBuilder.AddPipe). У Util пустой цвет
                // означает «узел не помечен» (охра ставится только конкретному узлу),
                // поэтому утилизация просто остаётся без цвета.
                foreach (var pipe in grid.Entities.OfType<PipeEntity>())
                {
                    if (!pipe.CustomColor.HasValue || pipe.CustomColor.Value.A != 0) continue;
                    pipe.CustomColor = pipe.PipeType == "Util" ? null : GetPipeLayerColor(pipe.PipeType);
                }

                // Тайлы
                foreach (var tileData in gridData.Tiles)
                {
                    grid.Tiles.Add(new PlacedTile { X = tileData.X, Y = tileData.Y, Proto = tileData.Proto });
                }

                // Декали
                foreach (var decalData in gridData.Decals)
                {
                    grid.Decals.Add(new PlacedDecal
                    {
                        X = decalData.X, Y = decalData.Y, Proto = decalData.Proto,
                        Color = decalData.Color, Rotation = decalData.Rotation,
                        Cleanable = decalData.Cleanable,
                        PatternOwnerId = decalData.PatternOwnerId,
                        PatternLayerIndex = decalData.PatternLayerIndex
                    });
                }

                // LooseDoors
                foreach (var doorData in gridData.LooseDoors)
                {
                    grid.LooseDoors.Add(new Door
                    {
                        X = doorData.X, Y = doorData.Y, Proto = doorData.Proto
                    });
                }

                _map.Grids.Add(grid);
                totalDecals += gridData.Decals.Count;
            }

            // Активируем нужный грид
            if (data.Grids.Count > 0)
            {
                if (!string.IsNullOrEmpty(data.ActiveGridName))
                {
                    var targetGrid = _map.Grids.FirstOrDefault(g => g.Name == data.ActiveGridName);
                    if (targetGrid != null)
                    {
                        _map.ActiveGridUid = targetGrid.Uid;
                    }
                    else
                    {
                        _map.ActiveGridUid = _map.Grids.First().Uid;
                    }
                }
                else
                {
                    _map.ActiveGridUid = _map.Grids.First().Uid;
                }
            }

            if (_map.Grids.Count > 0 && _map.ActiveGrid != null)
            {
                // Пересчитываем владельцев и прототипы дверей по актуальным
                // приоритетам типов: в файле могли сохраниться двери, посчитанные
                // ещё до изменения приоритета, и без этого они оставались бы
                // «чужими» до первого редактирования комнаты
                _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);
                RecalculateDecalPatterns();
                UpdateTileGrid();
                SaveState();
                Render();
            }

            InitGridTabs();

            // Без модального окна: загрузка проекта не должна прерывать работу,
            // итог показываем всплывающим уведомлением (как при сохранении)
            ToastNotification.Show(this,
                $"Проект загружен: {Path.GetFileName(dialog.FileName)} — слоёв {data.Grids.Count}, комнат {totalRooms}, " +
                $"дверей {totalDoors}, сущностей {totalEntities}, декалей {totalDecals}",
                "MapperIce", 4000);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }


    /// <summary>
    /// Применяет сохранённые в проекте настройки слоёв труб к _pipeLayers: цвет и
    /// прототип вентиляции. Слои, которых нет в файле (новые в текущей версии),
    /// остаются с дефолтными значениями; битый цвет слоя не затирает текущий.
    /// Вызывается только при загрузке проекта.
    /// </summary>
    private void ApplyPipeLayers(List<PipeLayerData>? savedLayers)
    {
        if (savedLayers == null || savedLayers.Count == 0) return;

        foreach (var layerData in savedLayers)
        {
            if (string.IsNullOrEmpty(layerData.Layer)) continue;

            if (!_pipeLayers.TryGetValue(layerData.Layer, out var settings))
            {
                settings = new PipeSettings { Layer = layerData.Layer };
                _pipeLayers[layerData.Layer] = settings;
            }

            if (TryParseColor(layerData.Color, out var color))
                settings.Color = color;
            // Подпись слоя (L1/L2/L3) — фиксированная метка из DefaultLayers, а не
            // пользовательская настройка: держим её из кода, чтобы проект, сохранённый
            // до переименования слоёв, не вернул прежнее название
            if (PipeSettings.DefaultLayers.TryGetValue(layerData.Layer, out var def))
                settings.DisplayName = def.DisplayName;
            settings.HasColor = layerData.HasColor;
            if (!string.IsNullOrEmpty(layerData.VentProto))
                settings.VentProto = layerData.VentProto;
        }

        UpdatePipeButtonColors();
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split(',');
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[0], out var a) || !int.TryParse(parts[1], out var r) ||
            !int.TryParse(parts[2], out var g) || !int.TryParse(parts[3], out var b))
            return false;

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    private Color ParseColor(string value)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 4)
                return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }
        catch { }
        return Color.FromArgb(200, 230, 230, 230);
    }

    private static HashSet<(int X, int Y)> ParseCutCells(List<string> cells)
    {
        var result = new HashSet<(int X, int Y)>();
        if (cells == null) return result;
        foreach (var cellStr in cells)
        {
            var parts = cellStr.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var cx) && int.TryParse(parts[1], out var cy))
                result.Add((cx, cy));
        }
        return result;
    }
}
