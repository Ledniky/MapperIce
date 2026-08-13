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
        MessageBox.Show("Загрузка карт из YAML пока не реализована");
    }


    private void ExportToYAML()
    {
        if (_map.ActiveGrid == null || _map.ActiveGrid.Rooms.Count == 0)
        {
            MessageBox.Show("Нет комнат для экспорта");
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
        if (_map.ActiveGrid == null)
        {
            MessageBox.Show("Нет активного грида для сохранения");
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
                ActiveGridName = _map.ActiveGrid.Name
            };

            // Комнаты и двери
            foreach (var room in _map.ActiveGrid.Rooms)
            {
                var roomData = new RoomData
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
                    FillColor = $"{room.FillColor.A},{room.FillColor.R},{room.FillColor.G},{room.FillColor.B}",
                    LineColor = $"{room.LineColor.A},{room.LineColor.R},{room.LineColor.G},{room.LineColor.B}"
                };

                foreach (var door in room.Doors)
                {
                    roomData.Doors.Add(new DoorData
                    {
                        X = door.X,
                        Y = door.Y,
                        Proto = door.Proto
                    });
                }

                data.Rooms.Add(roomData);
            }

            // Все сущности грида (трубы, сигнализации, размещённые прототипы и любые будущие типы).
            // Пожарные шлюзы (Firelock) не сохраняем — они пересоздаются автоматически
            // из дверей через DoorUpdater.UpdateAllDoors при загрузке.
            foreach (var entity in _map.ActiveGrid.Entities.Where(e => e is not FirelockEntity))
            {
                data.Entities.Add(new GenericEntityData
                {
                    Type = entity.GetType().Name,
                    Data = System.Text.Json.JsonSerializer.SerializeToElement(entity, entity.GetType())
                });
            }

            data.Tiles = _map.ActiveGrid.Tiles
                .Select(t => new PlacedTile { X = t.X, Y = t.Y, Proto = t.Proto })
                .ToList();

            data.Decals = _map.ActiveGrid.Decals
                .Select(d => new PlacedDecal { X = d.X, Y = d.Y, Proto = d.Proto, Color = d.Color, Rotation = d.Rotation, Cleanable = d.Cleanable })
                .ToList();

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show($"Проект сохранён!\nКомнат: {data.Rooms.Count}\nДверей: {data.Rooms.Sum(r => r.Doors.Count)}\nСущностей: {data.Entities.Count}\nДекалей: {data.Decals.Count}");


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

            if (_map.ActiveGrid != null)
            {
                _map.ActiveGrid.Rooms.Clear();
                _map.ActiveGrid.Entities.Clear();
                _map.ActiveGrid.Tiles.Clear();
                _map.ActiveGrid.Decals.Clear();
                foreach (var roomData in data.Rooms)
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
                        FillColor = ParseColor(roomData.FillColor),
                        LineColor = ParseColor(roomData.LineColor)
                    };

                    foreach (var doorData in roomData.Doors)
                    {
                        room.Doors.Add(new Door
                        {
                            X = doorData.X,
                            Y = doorData.Y,
                            Proto = doorData.Proto
                        });
                    }

                    _map.ActiveGrid.Rooms.Add(room);
                }

                int restoredCount = 0;
                foreach (var entityData in data.Entities)
                {
                    if (!EntityTypeRegistry.TryGetType(entityData.Type, out var type))
                        continue; // неизвестный/удалённый тип — пропускаем, не роняем загрузку

                    try
                    {
                        // Исправление здесь:
                        var restored = JsonSerializer.Deserialize(entityData.Data.GetRawText(), type);
                        if (restored is MapEntity mapEntity)
                        {
                            _map.ActiveGrid.Entities.Add(mapEntity);
                            restoredCount++;
                        }
                    }
                    catch
                    {
                        // повреждённая запись — пропускаем
                    }
                }


                foreach (var tileData in data.Tiles)
                {
                    _map.ActiveGrid.Tiles.Add(new PlacedTile { X = tileData.X, Y = tileData.Y, Proto = tileData.Proto });
                }

                foreach (var decalData in data.Decals)
                {
                    _map.ActiveGrid.Decals.Add(new PlacedDecal { X = decalData.X, Y = decalData.Y, Proto = decalData.Proto, Color = decalData.Color, Rotation = decalData.Rotation, Cleanable = decalData.Cleanable });
                }

                _doorUpdater.UpdateAllDoors(_map.ActiveGrid); // пересоздаёт Firelock из дверей
                RecalculateDecalPatterns();
                UpdateTileGrid();
                SaveState();
                Render();

                int totalDoors = data.Rooms.Sum(r => r.Doors.Count);
                MessageBox.Show($"Проект загружен!\nКомнат: {data.Rooms.Count}\nДверей: {totalDoors}\nСущностей: {restoredCount}\nДекалей: {data.Decals.Count}");


            }
            else { MessageBox.Show("Уже что-то в рабочей области"); }

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
