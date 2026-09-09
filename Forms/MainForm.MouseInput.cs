// Forms/MainForm.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        // Перехватываем только ЛКМ (перетаскивание углов/области). ПКМ (панорамирование)
        // и СКМ должны обрабатываться обычной логикой ниже даже в режиме редактирования области.
        if (_editingDecalArea != null && e.Button == MouseButtons.Left)
        {
            HandleDecalAreaEditMouseDown(e);
            return;
        }

        if (_canvas.Width == 0 || _canvas.Height == 0) return;
        if (_map.ActiveGrid == null) return;

        if (e.Button == MouseButtons.Right)
        {
            if (_pipeBuilder.IsDrawing)
            {
                _pipeBuilder.ResetDrawing();
                Render();
                return;
            }

            // ПКМ по синему квадрату — открыть диалог ввода строки
            var grid = _map.ActiveGrid;
            if (grid != null)
            {
                var tilePos = GetTilePosition(e.Location);
                var markedPipe = grid.Entities.OfType<PipeEntity>()
                    .FirstOrDefault(p => (int)p.X == tilePos.x && (int)p.Y == tilePos.y && p.HasFilterMarker);
                if (markedPipe != null)
                {
                    ShowFilterLabelDialog(markedPipe);
                    return;
                }
            }

            _isPanning = true;
            _panStart = new PointF(e.Location.X, e.Location.Y);
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button == MouseButtons.Middle)
        {
            if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm ||
                _toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
            {
                _currentAlarmRotation += (float)(Math.PI / 2);
                if (_currentAlarmRotation >= (float)(Math.PI * 2))
                    _currentAlarmRotation -= (float)(Math.PI * 2);

                var alarmTilePos = GetTilePosition(_lastMousePosition);  // Переименовано
                string type = _toolManager.CurrentTool == ToolManager.Tool.AirAlarm ? "AirAlarm" : "FireAlarm";
                _renderer.SetAlarmPreview(alarmTilePos.x, alarmTilePos.y, _currentAlarmRotation, type);

                Render();
                return;
            }
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            var tilePos = GetTilePosition(e.Location);
            int tileX = tilePos.x;
            int tileY = tilePos.y;

            if (_toolManager.CurrentTool == ToolManager.Tool.Delete)
            {
                var grid = _map.ActiveGrid;

                // Те же флаги _deleteSettings, что и в DeleteArea: при DeleteAll фильтр
                // не мешает, иначе конкретный чекбокс должен быть включён
                bool canDeletePipes = _deleteSettings.DeleteAll || _deleteSettings.DeletePipes;
                bool canDeleteAlarms = _deleteSettings.DeleteAll || _deleteSettings.DeleteAlarms;
                bool canDeleteRooms = _deleteSettings.DeleteAll || _deleteSettings.DeleteRooms;
                bool canDeleteEntities = _deleteSettings.DeleteAll || _deleteSettings.DeleteEntities;
                bool canDeleteOther = _deleteSettings.DeleteAll || _deleteSettings.DeleteOther;
                bool canDeleteDecals = _deleteSettings.DeleteAll || _deleteSettings.DeleteDecals;

                var alarm = grid.Entities.OfType<AirAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (alarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(alarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                var fireAlarm = grid.Entities.OfType<FireAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (fireAlarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(fireAlarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                var pipe = grid.Entities.OfType<PipeEntity>().FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY);
                if (pipe != null) { if (canDeletePipes) { grid.Entities.Remove(pipe); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Двери и вручную поставленные тайлы всегда удаляемы точечно — как и в области,
                // для них нет отдельных чекбоксов в DeleteSettings
                if (_doorUpdater.TryRemoveDoor(grid, tileX, tileY)) { RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render(); return; }
                var anyEntity = grid.Entities.FirstOrDefault(e => (int)e.X == tileX && (int)e.Y == tileY);
                if (anyEntity != null)
                {
                    bool isGenericEntity = anyEntity.GetType() == typeof(MapEntity);
                    bool allowed = isGenericEntity ? canDeleteEntities : canDeleteOther;
                    if (allowed) { grid.Entities.Remove(anyEntity); SaveState(); UpdateTileGrid(); Render(); }
                    return;
                }

                var decal = grid.Decals.FirstOrDefault(d => FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY);
                if (decal != null) { if (canDeleteDecals) { grid.Decals.Remove(decal); SaveState(); UpdateTileGrid(); Render(); } return; }

                var placedTile = grid.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
                if (placedTile != null) { grid.Tiles.Remove(placedTile); SaveState(); UpdateTileGrid(); Render(); return; }

                var room = grid.Rooms.FirstOrDefault(r => tileX >= r.X && tileX < r.X + r.Width && tileY >= r.Y && tileY < r.Y + r.Height);
                if (room != null && canDeleteRooms)
                {
                    // Удаляем декали, принадлежащие удаляемой комнате
                    var roomDecals = grid.Decals
                        .Where(d => d.X >= room.X && d.X < room.X + room.Width &&
                                    d.Y >= room.Y && d.Y < room.Y + room.Height)
                        .ToList();
                    foreach (var rd in roomDecals)
                        grid.Decals.Remove(rd);

                    grid.Rooms.Remove(room);
                    _doorUpdater.RecalculateAllDoors(grid);
                    SaveState(); UpdateTileGrid(); Render(); return;
                }



            }



            else if (_toolManager.CurrentTool == ToolManager.Tool.DeleteArea)
            {
                _isDeletingArea = true;
                _deleteStartPoint = new Point(tileX, tileY);
                _deleteEndPoint = new Point(tileX, tileY);
                Render();
                return;
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.CreateRoom ||
                     _toolManager.CurrentTool == ToolManager.Tool.SubtractRoom)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _currentRoom = new Room { X = tileX, Y = tileY, Width = 1, Height = 1 };
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.RestoreRoom)
            {
                var targetRoom = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                if (targetRoom != null && targetRoom.RemovedCells.Count > 0)
                {
                    _isDrawing = true;
                    _startPoint = e.Location;
                    _currentRoom = new Room { X = tileX, Y = tileY, Width = 1, Height = 1 };
                    _restoreTargetRoom = targetRoom;
                }
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.Door)
            {
                var targetRoom = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                string doorProto = targetRoom?.DoorProto ?? "Airlock";

                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, doorProto, out _, _snapToGrid))
                { RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render(); }
            }


            else if (_toolManager.CurrentTool == ToolManager.Tool.DoorGlass)
            {
                var targetRoom = _map.ActiveGrid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);
                string glassDoorProto = targetRoom?.GlassDoorProto ?? "AirlockGlass";

                if (_doorUpdater.TryCreateDoor(_map.ActiveGrid, tileX, tileY, glassDoorProto, out _, _snapToGrid))
                { RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render(); }
            }

                        else if (_toolManager.CurrentTool == ToolManager.Tool.Passage)
            {
                var grid = _map.ActiveGrid;
                // Ищем любую комнату, на периметре которой находится стена
                var room = grid.Rooms.FirstOrDefault(r =>
                {
                    bool isOnEdge = tileX == r.X || tileX == r.X + r.Width - 1 ||
                                    tileY == r.Y || tileY == r.Y + r.Height - 1;
                    return isOnEdge && _tileGrid.IsWall(tileX, tileY);
                });

                if (room != null && !room.Passages.Contains((tileX, tileY)))
                {
                    // Помечаем клетку как проход — TileBuilder не будет строить тут
                    // стену при следующей пересборке TileGrid, а тайл пола под ней
                    // уже есть (стадия 1 в BuildFromRooms кладёт пол под все клетки
                    // комнаты, включая границу)
                    room.Passages.Add((tileX, tileY));
                    RecalculateDecalPatterns();
                    UpdateTileGrid();
                    SaveState();
                    Render();
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.RestoreWall)
            {
                var grid = _map.ActiveGrid;
                var room = grid.Rooms.FirstOrDefault(r =>
                    tileX >= r.X && tileX < r.X + r.Width &&
                    tileY >= r.Y && tileY < r.Y + r.Height);

                if (room != null)
                {
                    bool isOnEdge = tileX == room.X || tileX == room.X + room.Width - 1 ||
                                    tileY == room.Y || tileY == room.Y + room.Height - 1;

                    if (isOnEdge && room.Passages.Contains((tileX, tileY)))
                    {
                        // Снимаем пометку прохода — TileBuilder сам восстановит
                        // стену на этой границе при следующей пересборке
                        room.Passages.Remove((tileX, tileY));
                        RecalculateDecalPatterns();
                        UpdateTileGrid();
                        SaveState();
                        Render();
                    }
                    else if (isOnEdge && !_tileGrid.HasTile(tileX, tileY))
                    {
                        // Старый случай — клетка на границе вообще без тайла
                        _tileGrid.SetTile(tileX, tileY, TileContent.Wall, room.WallProto, room.RoomType, room.GetHashCode());
                        UpdateTileGrid();
                        SaveState();
                        Render();
                    }
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.PipeDistra ||
                     _toolManager.CurrentTool == ToolManager.Tool.PipeWaste ||
                     _toolManager.CurrentTool == ToolManager.Tool.PipeNormal ||
                     _toolManager.CurrentTool == ToolManager.Tool.PipeUtil)
            {
                if (!_pipeBuilder.IsDrawing)
                {
                    _pipeBuilder.StartDrawing(tileX, tileY);
                    Render();
                }
                else
                {
                    string pipeType = _toolManager.CurrentTool switch
                    {
                        ToolManager.Tool.PipeDistra => "Distra",
                        ToolManager.Tool.PipeWaste => "Waste",
                        ToolManager.Tool.PipeUtil => "Util",
                        _ => "Normal"
                    };
                    _pipeBuilder.FinishDrawing(_map.ActiveGrid, pipeType);
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.PipeUtilSettings)
            {
                // Клик по трубе утилизации — меняем направление стрелки
                var grid = _map.ActiveGrid;
                if (grid == null) return;

                var utilPipe = grid.Entities.OfType<PipeEntity>()
                    .FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY && p.PipeType == "Util");

                if (utilPipe != null)
                {
                    // Считаем соседей по каждому конкретному направлению — нужны для
                    // построения списка допустимых направлений стрелки индивидуально
                    // для зелёного и синего узла (см. ниже)
                    var utilPipes = grid.Entities.OfType<PipeEntity>().Where(p => p.PipeType == "Util").ToList();
                    bool hasTop = utilPipes.Any(p => p.X == utilPipe.X && p.Y == utilPipe.Y - 1);
                    bool hasBottom = utilPipes.Any(p => p.X == utilPipe.X && p.Y == utilPipe.Y + 1);
                    bool hasLeft = utilPipes.Any(p => p.X == utilPipe.X - 1 && p.Y == utilPipe.Y);
                    bool hasRight = utilPipes.Any(p => p.X == utilPipe.X + 1 && p.Y == utilPipe.Y);

                    // Кодировка UtilArrowRotation (сверено с формулой поворота в
                    // Renderer.DrawPipeFlowArrows): 0 = север (вверх), 1 = восток (вправо),
                    // 2 = юг (вниз), 3 = запад (влево)
                    var validRots = new List<int>();

                    if (utilPipe.CustomColor.HasValue)
                    {
                        // Синий узел (перекрашен кнопкой "🔧🔧") — стрелка может указывать
                        // ТОЛЬКО "напролёт": вдоль оси, где сосед есть одновременно и
                        // спереди, и сзади. В сторону одиночного ответвления стрелка
                        // указывать не может.
                        if (hasTop && hasBottom) { validRots.Add(0); validRots.Add(2); }
                        if (hasLeft && hasRight) { validRots.Add(1); validRots.Add(3); }
                    }
                    else
                    {
                        // Зелёный узел (обычный, без перекраски) — стрелка может смотреть
                        // в сторону ЛЮБОГО реально существующего соседа, без ограничения
                        // на "сквозную" ось.
                        if (hasTop) validRots.Add(0);
                        if (hasRight) validRots.Add(1);
                        if (hasBottom) validRots.Add(2);
                        if (hasLeft) validRots.Add(3);
                    }

                    if (validRots.Count > 0)
                    {
                        // Если текущее направление недопустимо (например, после изменения сети),
                        // сбрасываем на первое допустимое
                        if (!validRots.Contains(utilPipe.UtilArrowRotation))
                        {
                            utilPipe.UtilArrowRotation = validRots[0];
                        }
                        else
                        {
                            // Циклически переключаем на следующее допустимое направление
                            int idx = validRots.IndexOf(utilPipe.UtilArrowRotation);
                            utilPipe.UtilArrowRotation = validRots[(idx + 1) % validRots.Count];
                        }

                        SaveState();
                        UpdateTileGrid();
                        Render();
                    }
                }
            
            
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.UtilWrenches)
            {
                // Клик по развилке утилизации — переключить охровый цвет
                var grid = _map.ActiveGrid;
                if (grid == null) return;

                var utilPipe = grid.Entities.OfType<PipeEntity>()
                    .FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY && p.PipeType == "Util");

                if (utilPipe != null)
                {
                    // Используем GetPipes как в Renderer — строим словарь позиций
                    var allPipes = _pipeBuilder.GetPipes(grid);
                    var utilDict = new Dictionary<(int x, int y), PipeEntity>();
                    foreach (var p in allPipes.Where(p => p.PipeType == "Util"))
                        utilDict[((int)p.X, (int)p.Y)] = p;

                    int neighbors = 0;
                    foreach (var (dx, dy) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
                    {
                        if (utilDict.ContainsKey(((int)utilPipe.X + dx, (int)utilPipe.Y + dy)))
                            neighbors++;
                    }

                    if (neighbors == 3)
                    {
                        if (utilPipe.CustomColor.HasValue)
                        {
                            utilPipe.CustomColor = null;
                        }
                        else
                        {
                            utilPipe.CustomColor = Color.FromArgb(255, 200, 200, 100); // охровый
                        }
                        SaveState();
                        UpdateTileGrid();
                        Render();
                    }
                    else if (neighbors == 1)
                    {
                        // Конец трубы — переключить тип: DisposalUnit ↔ MailingUnit
                        utilPipe.EndpointType = utilPipe.EndpointType == EndpointType.MailingUnit
                            ? EndpointType.DisposalUnit
                            : EndpointType.MailingUnit;
                        SaveState();
                        UpdateTileGrid();
                        Render();
                    }
                    else if (neighbors == 2)
                    {
                        // Проверяем, что соседи по противоположным сторонам
                        var oppPairs = new[]
                        {
                            ((0, -1), (0, 1)),  // север-юг
                            ((-1, 0), (1, 0))   // запад-восток
                        };
                        bool isStraight = false;
                        foreach (var (d1, d2) in oppPairs)
                        {
                            bool has1 = utilDict.ContainsKey(((int)utilPipe.X + d1.Item1, (int)utilPipe.Y + d1.Item2));
                            bool has2 = utilDict.ContainsKey(((int)utilPipe.X + d2.Item1, (int)utilPipe.Y + d2.Item2));
                            if (has1 && has2)
                            {
                                isStraight = true;
                                break;
                            }
                        }

                        if (isStraight)
                        {
                            utilPipe.HasFilterMarker = !utilPipe.HasFilterMarker;
                            SaveState();
                            UpdateTileGrid();
                            Render();
                        }
                    }
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.UtilNodeLabel)
            {
                var grid = _map.ActiveGrid;
                if (grid == null) return;

                var utilPipe = grid.Entities.OfType<PipeEntity>()
                    .FirstOrDefault(p => (int)p.X == tileX && (int)p.Y == tileY &&
                                         p.PipeType == "Util");

                if (utilPipe != null)
                {
                    ShowFilterLabelDialog(utilPipe);
                }
            }

            
            else if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                if (!string.IsNullOrEmpty(_protoToPlace))
                {
                    var grid = _map.ActiveGrid;

                    var proto = _indexer.FindPrototype(_protoToPlace);
                    bool isTile = proto != null && proto.Type == "tile";
                    bool isDecal = proto != null && proto.Type == "decal";

                    if (isTile)
                    {
                        var placeTilePos = GetTilePosition(e.Location);  // Переименовано

                        var existing = grid.Tiles.FirstOrDefault(t => t.X == placeTilePos.x && t.Y == placeTilePos.y);
                        if (existing != null)
                            grid.Tiles.Remove(existing);

                        grid.Tiles.Add(new PlacedTile { X = placeTilePos.x, Y = placeTilePos.y, Proto = _protoToPlace });
                    }



                    else if (isDecal)
                    {
                        // Декали кладём только там, где есть пол — так же, как трубы
                        // (PipeBuilder.HasFloorAt) и сигнализации (AddAirAlarm/AddFireAlarm)
                        // не ставятся в пустоте. Проверяем по тайлу под курсором, а не по
                        // точной дробной координате, иначе декаль у самого края комнаты
                        // могла бы формально попасть "мимо" пола из-за округления
                        var floorCheckTile = GetTilePosition(e.Location);
                        if (!HasFloorAt(grid, floorCheckTile.x, floorCheckTile.y))
                        {
                            return;
                        }

                        // Декали — не ECS-сущности в игре, поэтому кладём их в отдельный
                        // список, а не в grid.Entities. Иначе при экспорте они попадут в
                        // entities: как обычный прототип и вызовут "Missing prototype",
                        // так как decal-id не зарегистрирован как id сущности
                        float decalX, decalY;
                        if (_snapEntityToCenter)
                        {
                            var centerTile = GetTilePosition(e.Location);
                            decalX = centerTile.x + _centerOffset.X;
                            decalY = centerTile.y + _centerOffset.Y;
                        }
                        else
                        {
                            var precise = GetPrecisePosition(e.Location);
                            decalX = precise.x;
                            decalY = precise.y;
                        }

                        grid.Decals.Add(new PlacedDecal { X = decalX, Y = decalY, Proto = _protoToPlace, Rotation = _currentEntityRotation, Color = _decalColor, Cleanable = _decalCleanable });
                    }


                    else
                    {
                        // Firelock — специальный тип, создаём FirelockEntity, а не MapEntity
                        bool isFirelock = _protoToPlace == "Firelock" || _protoToPlace == "FirelockGlass";

                        float finalX, finalY;
                        if (isFirelock)
                        {
                            var firelockTilePos = GetTilePosition(e.Location);
                            finalX = firelockTilePos.x;
                            finalY = firelockTilePos.y;
                        }
                        else if (_snapEntityToCenter)
                        {
                            var centerTile = GetTilePosition(e.Location);
                            finalX = centerTile.x + _centerOffset.X;
                            finalY = centerTile.y + _centerOffset.Y;
                        }
                        else
                        {
                            var precise = GetPrecisePosition(e.Location);
                            finalX = precise.x;
                            finalY = precise.y;
                        }

                        if (isFirelock)
                        {
                            grid.Entities.Add(new FirelockEntity
                            {
                                X = finalX,
                                Y = finalY,
                                Proto = _protoToPlace,
                                IsGlass = _protoToPlace == "FirelockGlass"
                            });
                        }
                        else
                        {
                            grid.Entities.Add(new MapEntity { X = finalX, Y = finalY, Proto = _protoToPlace, Rotation = _currentEntityRotation });
                        }
                    }

                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }




            else if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm)
            {
                AddAirAlarm(_map.ActiveGrid, tileX, tileY);
                SaveState();
                UpdateTileGrid();
                Render();
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
            {
                AddFireAlarm(_map.ActiveGrid, tileX, tileY);
                SaveState();
                UpdateTileGrid();
                Render();
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.DecalRule)
            {
                var grid = _map.ActiveGrid;
                var room = grid.Rooms.FirstOrDefault(r => r.Contains(tileX, tileY));
                if (room != null)
                {
                    ShowDecalRuleDialog(room);
                }
            }
            else if (_toolManager.CurrentTool == ToolManager.Tool.Move)
            {
                var grid = _map.ActiveGrid;
                if (grid == null) return;

                bool shiftHeld = ModifierKeys.HasFlag(Keys.Shift);
                bool ctrlHeld = ModifierKeys.HasFlag(Keys.Control);

                var hit = HitTestAt(tileX, tileY);

                if (shiftHeld && _lastClickTile.HasValue)
                {
                    int minX = Math.Min(_lastClickTile.Value.x, tileX);
                    int maxX = Math.Max(_lastClickTile.Value.x, tileX);
                    int minY = Math.Min(_lastClickTile.Value.y, tileY);
                    int maxY = Math.Max(_lastClickTile.Value.y, tileY);

                _selectedObjects = GatherObjectsInRect(minX, minY, maxX, maxY);
                _renderer.SetSelection(_selectedObjects);
                // Статус удалён — тип комнаты теперь в ComboBox

                    BeginMoveDrag(e.Location);
                    Render();
                    return;
                }

                // Клик по пустому месту — начинаем протягивание рамки (заменяющий режим)
                _isBoxSelecting = true;
                _boxSelectAdditive = false;
                _boxStartScreen = e.Location;
                _boxEndScreen = e.Location;
                _lastClickTile = (tileX, tileY);
                Render();
            }












        }
    }









    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        bool isPlacingPrototype = _toolManager.CurrentTool == ToolManager.Tool.PlacePrototype;
        bool ctrlHeld = ModifierKeys.HasFlag(Keys.Control);

        if (isPlacingPrototype && !ctrlHeld)
        {
            if (_snapEntityRotation)
            {
                float step = (float)(Math.PI / 2);
                _currentEntityRotation += e.Delta > 0 ? step : -step;
                _currentEntityRotation = (float)(Math.Round(_currentEntityRotation / step) * step);
            }
            else
            {
                float step = (float)(Math.PI / 36); // 5° за "щелчок" колеса
                _currentEntityRotation += e.Delta > 0 ? step : -step;
            }

            float fullCircle = (float)(Math.PI * 2);
            _currentEntityRotation %= fullCircle;
            if (_currentEntityRotation < 0)
                _currentEntityRotation += fullCircle;

            // Обновляем превью немедленно, не дожидаясь движения мыши
            if (!string.IsNullOrEmpty(_protoToPlace))
            {
                float previewX, previewY;
                bool isFirelock = _protoToPlace == "Firelock" || _protoToPlace == "FirelockGlass";
                if (isFirelock)
                {
                    var centerTile = GetTilePosition(_lastMousePosition);
                    previewX = centerTile.x;
                    previewY = centerTile.y;
                }
                else if (_snapEntityToCenter)
                {
                    var centerTile = GetTilePosition(_lastMousePosition);
                    previewX = centerTile.x + _centerOffset.X;
                    previewY = centerTile.y + _centerOffset.Y;
                }
                else
                {
                    var precise = GetPrecisePosition(_lastMousePosition);
                    previewX = precise.x;
                    previewY = precise.y;
                }

                var wheelProto = _indexer.FindPrototype(_protoToPlace);
                bool wheelIsDecal = wheelProto != null && wheelProto.Type == "decal";
                _renderer.SetEntityPreview(previewX, previewY, _currentEntityRotation, _protoToPlace,
                    wheelIsDecal ? _decalColor : null);
            }


            if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype)
            {
                float protoDegrees = _currentEntityRotation * 180 / (float)Math.PI;
                // Статус удалён — тип комнаты теперь в ComboBox
            }

            Render();
            return;
        }

        // Зум: либо инструмент неактивен (CTRL не важен), либо инструмент активен и CTRL зажат
        float zoomDelta = e.Delta > 0 ? 0.1f : -0.1f;
        _scale = Math.Clamp(_scale + zoomDelta, 0.2f, 3.0f);
        Render();
    }


    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        // Перехватываем только когда реально идёт перетаскивание угла/области (ЛКМ зажата).
        // Если сейчас панорамирование (_isPanning от ПКМ), пропускаем в обычную логику ниже.
        if (_editingDecalArea != null && (_draggingDecalCornerIndex >= 0 || _isDraggingDecalWholeArea))
        {
            HandleDecalAreaEditMouseMove(e);
            return;
        }

        _lastMousePosition = e.Location;

        if (_isBoxSelecting)
        {
            _boxEndScreen = e.Location;
            _renderer.SetSelectionBox(_boxStartScreen, _boxEndScreen);
            Render();
            return;
        }

        if (_isMovingSelection)
        {
            var current = GetPrecisePosition(e.Location);
            float rawDx = current.x - _moveDragStartWorld.x;
            float rawDy = current.y - _moveDragStartWorld.y;

            // Если в выделении есть комната или вручную поставленный тайл — вся группа
            // двигается целыми шагами (по умолчанию 1 тайл), а не плавно по пикселю
            bool forceSnap = _selectedObjects.Any(o => o is Room || o is PlacedTile);

            float dx, dy;
            if (forceSnap)
            {
                float step = _moveSettings.Step <= 0 ? 1f : _moveSettings.Step;
                dx = (float)(Math.Round(rawDx / step) * step);
                dy = (float)(Math.Round(rawDy / step) * step);
            }
            else
            {
                dx = rawDx;
                dy = rawDy;
            }

            if (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f)
                _moveDidMove = true;

            foreach (var item in _moveSnapshot)
            {
                MoveTarget(item.Target, item.OrigX + dx, item.OrigY + dy);
            }

            UpdateTileGrid();
            Render();
            return;
        }

        if (_isPanning)
        {
            _viewOffset.X -= e.Location.X - _panStart.X;
            _viewOffset.Y -= e.Location.Y - _panStart.Y;
            _panStart = new PointF(e.Location.X, e.Location.Y);
            Render();
            return;
        }

        if (_map.ActiveGrid == null) return;

        if (_toolManager.CurrentTool == ToolManager.Tool.AirAlarm ||
            _toolManager.CurrentTool == ToolManager.Tool.FireAlarm)
        {
            var tilePos = GetTilePosition(e.Location);
            string type = _toolManager.CurrentTool == ToolManager.Tool.AirAlarm ? "AirAlarm" : "FireAlarm";
            _renderer.SetAlarmPreview(tilePos.x, tilePos.y, _currentAlarmRotation, type);
            Render();
            return;
        }
        else
        {
            _renderer.ClearAlarmPreview();
        }

        if (_toolManager.CurrentTool == ToolManager.Tool.PlacePrototype && !string.IsNullOrEmpty(_protoToPlace))
        {
            float previewX, previewY;
            bool isFirelock = _protoToPlace == "Firelock" || _protoToPlace == "FirelockGlass";
            if (isFirelock)
            {
                var tilePos = GetTilePosition(e.Location);
                previewX = tilePos.x;
                previewY = tilePos.y;
            }
            else if (_snapEntityToCenter)
            {
                var centerTile = GetTilePosition(e.Location);
                previewX = centerTile.x + _centerOffset.X;
                previewY = centerTile.y + _centerOffset.Y;
            }
            else
            {
                var precise = GetPrecisePosition(e.Location);
                previewX = precise.x;
                previewY = precise.y;
            }

            var moveProto = _indexer.FindPrototype(_protoToPlace);
            bool moveIsDecal = moveProto != null && moveProto.Type == "decal";
            _renderer.SetEntityPreview(previewX, previewY, _currentEntityRotation, _protoToPlace,
                moveIsDecal ? _decalColor : null);
            Render();
            return;
        }



        else
        {
            _renderer.ClearEntityPreview();
        }

        if (_isDeletingArea)
        {
            var tilePos = GetTilePosition(e.Location);
            _deleteEndPoint = new Point(tilePos.x, tilePos.y);
            Render();
            return;
        }

        if (_pipeBuilder.IsDrawing)
        {
            var tilePos = GetTilePosition(e.Location);
            _pipeBuilder.UpdateEndPoint(tilePos.x, tilePos.y);
            Render();
            return;
        }

        if (!_isDrawing || _currentRoom == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * _scale);
        int activeIndex = _map.Grids.IndexOf(_map.ActiveGrid);
        float layerOffsetY = Grid.GetLayerOffsetY(activeIndex);
        float gridOffsetX = _map.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = (_map.ActiveGrid.Position.Y + layerOffsetY) * tileSize;

        float endWorldX = (e.Location.X + _viewOffset.X - gridOffsetX) / tileSize;
        float endWorldY = (e.Location.Y + _viewOffset.Y - gridOffsetY) / tileSize;
        float startWorldX = (_startPoint.X + _viewOffset.X - gridOffsetX) / tileSize;
        float startWorldY = (_startPoint.Y + _viewOffset.Y - gridOffsetY) / tileSize;

        int endX = (int)Math.Floor(endWorldX);
        int endY = (int)Math.Floor(endWorldY);
        int startX = (int)Math.Floor(startWorldX);
        int startY = (int)Math.Floor(startWorldY);

        _currentRoom.X = Math.Min(startX, endX);
        _currentRoom.Y = Math.Min(startY, endY);
        _currentRoom.Width = Math.Abs(endX - startX) + 1;
        _currentRoom.Height = Math.Abs(endY - startY) + 1;

        Render();
    }


    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        // Аналогично MouseMove — перехватываем отпускание только если реально
        // тащили угол/область; иначе (например, отпускание ПКМ после панорамирования)
        // отдаём обработку обычной логике ниже.
        if (_editingDecalArea != null && (_draggingDecalCornerIndex >= 0 || _isDraggingDecalWholeArea))
        {
            HandleDecalAreaEditMouseUp(e);
            return;
        }

        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            _renderer.ClearSelectionBox();

            int dxPix = Math.Abs(_boxEndScreen.X - _boxStartScreen.X);
            int dyPix = Math.Abs(_boxEndScreen.Y - _boxStartScreen.Y);

            if (dxPix < 3 && dyPix < 3)
            {
                // Слишком маленькое перемещение — это был обычный клик по пустому месту, а не протягивание
                if (!_boxSelectAdditive)
                {
                    _selectedObjects.Clear();
                    _renderer.SetSelection(_selectedObjects);
                    // Статус удалён — тип комнаты теперь в ComboBox
                }
                Render();
                return;
            }

            var startTile = GetTilePosition(_boxStartScreen);
            var endTile = GetTilePosition(_boxEndScreen);

            int minX = Math.Min(startTile.x, endTile.x);
            int maxX = Math.Max(startTile.x, endTile.x);
            int minY = Math.Min(startTile.y, endTile.y);
            int maxY = Math.Max(startTile.y, endTile.y);

            var found = GatherObjectsInRect(minX, minY, maxX, maxY);

            if (_boxSelectAdditive)
            {
                foreach (var obj in found)
                {
                    if (!_selectedObjects.Contains(obj))
                        _selectedObjects.Add(obj);
                }
            }
            else
            {
                _selectedObjects = found;
            }

            _renderer.SetSelection(_selectedObjects);
            // Статус удалён — тип комнаты теперь в ComboBox
            Render();
            return;
        }

        if (_isMovingSelection)
        {
            _isMovingSelection = false;

            if (_moveDidMove)
            {
                if (_map.ActiveGrid != null)
                {
                    _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);
                    RecalculateDecalPatterns();
                }
                UpdateTileGrid();
                SaveState(); // ← логирование в undo/redo
            }

            _moveSnapshot.Clear();
            _moveDidMove = false;
            Render();
            return;
        }

        if (e.Button == MouseButtons.Right && _isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
            return;
        }

        if (e.Button == MouseButtons.Left && _isDeletingArea && _map.ActiveGrid != null)
        {
            var start = _deleteStartPoint;
            var end = _deleteEndPoint ?? start;

            int minX = Math.Min(start.X, end.X);
            int maxX = Math.Max(start.X, end.X);
            int minY = Math.Min(start.Y, end.Y);
            int maxY = Math.Max(start.Y, end.Y);

            var grid = _map.ActiveGrid;
            var toRemove = new List<MapEntity>();
            var decalsToRemove = new List<PlacedDecal>();

            if (_deleteSettings.DeleteAll)
            {
                toRemove.AddRange(grid.Entities.Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY));
                decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        _doorUpdater.TryRemoveDoor(grid, x, y);

                var rooms = grid.Rooms.Where(r => !(r.X + r.Width <= minX || r.X > maxX || r.Y + r.Height <= minY || r.Y > maxY)).ToList();
                foreach (var room in rooms) grid.Rooms.Remove(room);

                if (rooms.Count > 0)
                    _doorUpdater.RecalculateAllDoors(grid);




            }
            else
            {
                if (_deleteSettings.DeletePipes)
                    toRemove.AddRange(grid.Entities.OfType<PipeEntity>().Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY));

                if (_deleteSettings.DeleteRooms)
                {
                    var roomsToRemove = grid.Rooms
                        .Where(r => !(r.X + r.Width <= minX || r.X > maxX || r.Y + r.Height <= minY || r.Y > maxY))
                        .ToList();
                    foreach (var room in roomsToRemove)
                    {
                        // Удаляем декали, принадлежащие удаляемой комнате
                        var roomDecals = grid.Decals
                            .Where(d => d.X >= room.X && d.X < room.X + room.Width &&
                                        d.Y >= room.Y && d.Y < room.Y + room.Height)
                            .ToList();
                        foreach (var rd in roomDecals)
                            grid.Decals.Remove(rd);

                        grid.Rooms.Remove(room);
                    }

                    if (roomsToRemove.Count > 0)
                        _doorUpdater.RecalculateAllDoors(grid);
                }

                if (_deleteSettings.DeleteAlarms)
                {
                    toRemove.AddRange(grid.Entities.OfType<AirAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                    toRemove.AddRange(grid.Entities.OfType<FireAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                }

                if (_deleteSettings.DeleteEntities)
                {
                    var repoEntities = grid.Entities
                        .Where(e => e.GetType() == typeof(MapEntity))
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .ToList();
                    toRemove.AddRange(repoEntities);
                }

                if (_deleteSettings.DeleteOther)
                {
                    var knownTypes = new HashSet<Type>
                    {
                        typeof(PipeEntity), typeof(AirAlarmEntity), typeof(FireAlarmEntity),
                        typeof(FirelockEntity), typeof(MapEntity)
                    };
                    var otherEntities = grid.Entities
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .Where(e => !knownTypes.Contains(e.GetType()))
                        .ToList();
                    toRemove.AddRange(otherEntities);
                }

                if (_deleteSettings.DeleteDecals)
                {
                    decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));
                }
            }

            foreach (var entity in toRemove) grid.Entities.Remove(entity);
            foreach (var decal in decalsToRemove) grid.Decals.Remove(decal);
            SaveState();
            UpdateTileGrid();
            Render();
            _isDeletingArea = false;
            _deleteEndPoint = null;
            return;
        }

        if (e.Button == MouseButtons.Left && _isDrawing && _currentRoom != null && _map.ActiveGrid != null)
        {
            if (_toolManager.CurrentTool == ToolManager.Tool.SubtractRoom)
            {
                if (_currentRoom.Width >= 1 && _currentRoom.Height >= 1)
                {
                    bool changed = RoomSubtractor.ApplyToGrid(
                        _map.ActiveGrid, _currentRoom.X, _currentRoom.Y, _currentRoom.Width, _currentRoom.Height);

                    if (changed)
                    {
                        _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);
                        RecalculateDecalPatterns();
                        UpdateTileGrid();
                        SaveState();
                    }
                }

                _currentRoom = null;
                _isDrawing = false;
                Render();
                return;
            }

            if (_toolManager.CurrentTool == ToolManager.Tool.RestoreRoom && _restoreTargetRoom != null)
            {
                if (_currentRoom.Width >= 1 && _currentRoom.Height >= 1)
                {
                    bool changed = RoomSubtractor.RestoreFromRoom(
                        _restoreTargetRoom, _currentRoom.X, _currentRoom.Y, _currentRoom.Width, _currentRoom.Height);

                    if (changed)
                    {
                        _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);
                        RecalculateDecalPatterns();
                        UpdateTileGrid();
                        SaveState();
                    }
                }

                _restoreTargetRoom = null;
                _currentRoom = null;
                _isDrawing = false;
                Render();
                return;
            }

            if (_currentRoom.Width > 1 || _currentRoom.Height > 1)
            {
                _roomTypeManager.ApplyTypeToRoom(_currentRoom);

                // Подхватываем унаследованное Decal Rule для выбранного типа комнаты
                // (по реальной C#-иерархии RoomType, см. DecalInheritanceManager) —
                // если явное правило есть хоть у одного предка в цепочке, новая комната
                // сразу получает его копию вместо пустого узора
                var roomTypeInstance = _roomTypeManager.GetRoomType(_roomTypeManager.SelectedType);
                var inheritedRule = _decalInheritanceManager.ResolveEffectiveRule(roomTypeInstance.GetType());
                if (inheritedRule != null)
                {
                    _currentRoom.AutoDecalRule = inheritedRule.Clone();
                    _currentRoom.DecalMode = DecalPatternMode.Auto;
                }

                // Обрабатывает и "впритык" (нахлёст создаётся), и "глубокое" наложение
                // (вырезается только внутренность, оставляя общее кольцо стены в 1 тайл) —
                // в обоих случаях граничный тайл стены оказывается один и тот же
                // физический тайл у старой и новой комнаты, без дублирования
                RoomSubtractor.ApplyForNewRoom(_map.ActiveGrid, _currentRoom);

                _map.ActiveGrid.Rooms.Add(_currentRoom);
                _doorUpdater.RecalculateAllDoors(_map.ActiveGrid);

                // Раньше узор для новой комнаты не пересчитывался тут вовсе — декали
                // появлялись только если пользователь вручную открывал диалог "Узор по
                // периметру" для этой конкретной комнаты. Теперь, когда правило может
                // прийти по наследству автоматически, пересчёт тоже должен быть
                // автоматическим сразу при создании
                RecalculateDecalPatterns();

                UpdateTileGrid();
                SaveState();
            }
            _currentRoom = null;
            _isDrawing = false;
            Render();
        }
    }


    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (_renderer != null)
        {
            _renderer.ClearAlarmPreview();
            _renderer.ClearEntityPreview();

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _renderer.ClearSelectionBox();
            }

            Render();
        }
    }
}
