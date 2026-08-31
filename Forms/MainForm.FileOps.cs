// Forms/MainForm.FileOps.cs

using MapperIce.Models;
using MapperIce.Services;
using System.Text.Json;

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
                var yaml = YAMLGenerator.Generate(_map.ActiveGrid, _tileBuilder, _pipeLayers, _alarmSettings);
                File.WriteAllText(dialog.FileName, yaml);
                MessageBox.Show($"Карта экспортирована в {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
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
            FileName = "project.ice"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var data = new ProjectData
            {
                LastSaved = DateTime.Now,
                ActiveGridName = _map.ActiveGrid?.Name
            };

            foreach (var grid in _map.Grids)
            {
                var gridData = new GridData
                {
                    Uid = grid.Uid,
                    Name = grid.Name,
                    PositionX = grid.Position.X,
                    PositionY = grid.Position.Y,
                    IsVisible = grid.IsVisible,
                    Color = $"{grid.Color.A},{grid.Color.R},{grid.Color.G},{grid.Color.B}"
                };

                // Комнаты и двери
                foreach (var room in grid.Rooms)
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
                        FillColor = $"{room.FillColor.A},{room.FillColor.R},{room.FillColor.G},{room.FillColor.B}",
                        LineColor = $"{room.LineColor.A},{room.LineColor.R},{room.LineColor.G},{room.LineColor.B}",
                        DecalMode = room.DecalMode,
                        HasCustomDecalRule = room.HasCustomDecalRule,
                        Priority = room.Priority,
                    };

                    // RemovedCells как список строк "x,y"
                    foreach (var cell in room.RemovedCells)
                    {
                        roomData.RemovedCells.Add($"{cell.X},{cell.Y}");
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
                                    X = a.X, Y = a.Y, Width = a.Width, Height = a.Height
                                }).ToList()
                            }).ToList()
                        };
                    }

                    // ManualDecalAreas
                    foreach (var area in room.ManualDecalAreas)
                    {
                        roomData.ManualDecalAreas.Add(new ManualDecalAreaData
                        {
                            X = area.X, Y = area.Y, Width = area.Width, Height = area.Height
                        });
                    }

                    gridData.Rooms.Add(roomData);
                }

                // Сущности (исключая Firelock)
                foreach (var entity in grid.Entities.Where(e => e is not FirelockEntity))
                {
                    gridData.Entities.Add(new GenericEntityData
                    {
                        Type = entity.GetType().Name,
                        Data = System.Text.Json.JsonSerializer.SerializeToElement(entity, entity.GetType())
                    });
                }

                gridData.Tiles = grid.Tiles
                    .Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto })
                    .ToList();

                gridData.Decals = grid.Decals
                    .Select(d => new PlacedDecal { X = d.X, Y = d.Y, Proto = d.Proto, Color = d.Color, Rotation = d.Rotation, Cleanable = d.Cleanable })
                    .ToList();

                gridData.LooseDoors = grid.LooseDoors
                    .Select(d => new DoorData { X = d.X, Y = d.Y, Proto = d.Proto })
                    .ToList();

                data.Grids.Add(gridData);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(dialog.FileName, json);

            int totalRooms = data.Grids.Sum(g => g.Rooms.Count);
            int totalDoors = data.Grids.Sum(g => g.Rooms.Sum(r => r.Doors.Count));
            int totalEntities = data.Grids.Sum(g => g.Entities.Count);
            int totalDecals = data.Grids.Sum(g => g.Decals.Count);
            MessageBox.Show($"Проект сохранён!\nСлоёв: {data.Grids.Count}\nКомнат: {totalRooms}\nДверей: {totalDoors}\nСущностей: {totalEntities}\nДекалей: {totalDecals}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
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
            var data = System.Text.Json.JsonSerializer.Deserialize<ProjectData>(json);

            if (data == null)
            {
                MessageBox.Show("Ошибка чтения файла");
                return;
            }

            // Очищаем все текущие данные
            _map.Grids.Clear();
            _map.ActiveGridUid = null;

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
                    Color = ParseColor(gridData.Color)
                };

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
                        FillColor = ParseColor(roomData.FillColor),
                        LineColor = ParseColor(roomData.LineColor),
                        DecalMode = roomData.DecalMode,
                        HasCustomDecalRule = roomData.HasCustomDecalRule,
                        Priority = roomData.Priority,
                        RemovedCells = new HashSet<(int X, int Y)>()
                    };

                    // Восстанавливаем RemovedCells
                    foreach (var cellStr in roomData.RemovedCells)
                    {
                        var parts = cellStr.Split(',');
                        if (parts.Length == 2)
                        {
                            room.RemovedCells.Add((int.Parse(parts[0]), int.Parse(parts[1])));
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
                                    X = a.X, Y = a.Y, Width = a.Width, Height = a.Height
                                }).ToList()
                            }).ToList()
                        };
                    }

                    // Восстанавливаем ManualDecalAreas
                    foreach (var areaData in roomData.ManualDecalAreas)
                    {
                        room.ManualDecalAreas.Add(new ManualDecalArea
                        {
                            X = areaData.X, Y = areaData.Y, Width = areaData.Width, Height = areaData.Height
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
                        var restored = JsonSerializer.Deserialize(entityData.Data.GetRawText(), type);
                        if (restored is MapEntity mapEntity)
                        {
                            grid.Entities.Add(mapEntity);
                            totalEntities++;
                        }
                    }
                    catch { /* повреждённая запись — пропускаем */ }
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
                        Cleanable = decalData.Cleanable
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
                _doorUpdater.UpdateAllDoors(_map.ActiveGrid);
                RecalculateDecalPatterns();
                UpdateTileGrid();
                SaveState();
                Render();
            }

            InitGridTabs();

            MessageBox.Show($"Проект загружен!\nСлоёв: {data.Grids.Count}\nКомнат: {totalRooms}\nДверей: {totalDoors}\nСущностей: {totalEntities}\nДекалей: {totalDecals}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
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
}
