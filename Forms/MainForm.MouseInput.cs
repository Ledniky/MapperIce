// Forms/MainForm.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        // Режим потайлёвой вырезки декалей (кнопка «✖» в окне «Ручные области»): клики
        // по клеткам области переключают их в CutCells — тайл теряет свою декаль, остальные
        // клетки строятся как обычно. Тянем мышью прямоугольник, как рамку выделения.
        if (_isCuttingDecalCells && e.Button == MouseButtons.Left)
        {
            HandleDecalCellCutMouseDown(e);
            return;
        }

        // Режим потайлового изменения ФОРМЫ области (кнопка «➖ Вычесть» в окне «Ручные области»):
        // клики по клеткам области удаляют их из формы (RemovedCells) — клетка перестаёт быть
        // частью области, контур/декали пересчитываются.
        if (_isSubtractingDecalCells && e.Button == MouseButtons.Left)
        {
            HandleDecalCellSubtractMouseDown(e);
            return;
        }

        // Растягивание НОВОЙ ручной области мышью (режим «Добавить область» в Decal Rule)
        if (_isDrawingDecalArea && e.Button == MouseButtons.Left)
        {
            HandleDecalAreaDrawMouseDown(e);
            return;
        }

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

            if (_wireBuilder.IsDrawing)
            {
                _wireBuilder.ResetDrawing();
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
            if (IsAlarmTool(_toolManager.CurrentTool))
            {
                // СКМ поворачивает сигнализацию так же, как колесо: состояние поворота
                // общее для всех ручных поворотов размещения (_placeRotation).
                RotatePlacementByQuarter(1);
                UpdateAlarmPreview();
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

            // Режим выбора объекта из конфигуратора сетей: клик выделяет сущность
            // в окне (передатчик или приёмник), в каком бы списке она ни была
            if (_networkPickMode)
            {
                PickEntityByClick(e.Location);
                Render();
                return;
            }

            if (_toolManager.CurrentTool == ToolManager.Tool.Magnifier)
            {
                _magnifierPinnedTile = (tileX, tileY);
                _magnifierPinned = true;
                UpdateMagnifierPanel();
                return;
            }

            if (_toolManager.CurrentTool == ToolManager.Tool.Delete)
            {
                var grid = _map.ActiveGrid;

                // Те же флаги _deleteSettings, что и в DeleteArea: при DeleteAll фильтр
                // не мешает, иначе конкретный чекбокс должен быть включён
                bool canDeletePipes = _deleteSettings.DeleteAll || _deleteSettings.DeletePipes;
                bool canDeleteWires = _deleteSettings.DeleteAll || _deleteSettings.DeleteWires;
                bool canDeleteAlarms = _deleteSettings.DeleteAll || _deleteSettings.DeleteAlarms;
                bool canDeleteRooms = _deleteSettings.DeleteAll || _deleteSettings.DeleteRooms;
                bool canDeleteDoors = _deleteSettings.DeleteAll || _deleteSettings.DeleteDoors;
                bool canDeletePrototypes = _deleteSettings.DeleteAll || _deleteSettings.DeletePrototypes;
                bool canDeleteOther = _deleteSettings.DeleteAll || _deleteSettings.DeleteOther;
                bool canDeleteDecals = _deleteSettings.DeleteAll || _deleteSettings.DeleteDecals;
                bool canDeleteTiles = _deleteSettings.DeleteAll || _deleteSettings.DeleteTiles;

                // Сигнализации
                var alarm = grid.Entities.OfType<AirAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (alarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(alarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                var fireAlarm = grid.Entities.OfType<FireAlarmEntity>().FirstOrDefault(a => (int)a.X == tileX && (int)a.Y == tileY);
                if (fireAlarm != null) { if (canDeleteAlarms) { grid.Entities.Remove(fireAlarm); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Трубы (газовые и утилизационные)
                var pipesHere = grid.Entities.OfType<PipeEntity>()
                    .Where(p => (int)p.X == tileX && (int)p.Y == tileY)
                    .ToList();
                if (pipesHere.Count > 0)
                {
                    PipeEntity pipe;
                    if (pipesHere.Count == 1)
                    {
                        pipe = pipesHere[0];
                    }
                    else
                    {
                        // Несколько труб разных типов на одном тайле — их узлы визуально
                        // смещены друг от друга (см. Renderer.GetPipeTypeOffset). Удаляем
                        // ту, чей узел физически ближе к точке клика, а не первую в списке
                        var (preciseX, preciseY) = GetPrecisePosition(e.Location);
                        float fracX = preciseX - tileX;
                        float fracY = preciseY - tileY;

                        pipe = pipesHere
                            .OrderBy(p =>
                            {
                                var (offX, offY) = Renderer.GetPipeTypeOffset(p.PipeType);
                                float dx = fracX - (0.5f + offX);
                                float dy = fracY - (0.5f + offY);
                                return dx * dx + dy * dy;
                            })
                            .First();
                    }

                    if (canDeletePipes) { grid.Entities.Remove(pipe); SaveState(); UpdateTileGrid(); Render(); }
                    return;
                }

                // Электропроводка
                var wire = grid.Entities.OfType<WireEntity>().FirstOrDefault(w => (int)w.X == tileX && (int)w.Y == tileY);
                if (wire != null) { if (canDeleteWires) { grid.Entities.Remove(wire); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Двери, проёмы и окна
                var anyRoom = grid.Rooms.FirstOrDefault(r => tileX >= r.X && tileX < r.X + r.Width && tileY >= r.Y && tileY < r.Y + r.Height);
                if (anyRoom != null)
                {
                    bool foundDoorFeature = false;
                    if (anyRoom.Passages.Contains((tileX, tileY)))
                    {
                        if (canDeleteDoors) { anyRoom.Passages.Remove((tileX, tileY)); RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render(); }
                        foundDoorFeature = true;
                    }
                    else if (anyRoom.Windows.Contains((tileX, tileY)))
                    {
                        if (canDeleteDoors)
                        {
                            // Удаляем окно: возвращаем стену на это место
                            anyRoom.Windows.Remove((tileX, tileY));
                            _tileGrid.SetTile(tileX, tileY, TileContent.Wall, anyRoom.WallProto, anyRoom.RoomType, anyRoom.GetHashCode());
                            // Сносим связанные сущности окна и решётки (generic MapEntity на этой клетке)
                            var winEntities = grid.Entities
                                .Where(e => e.GetType() == typeof(MapEntity) &&
                                            (int)e.X == tileX && (int)e.Y == tileY)
                                .ToList();
                            foreach (var we in winEntities)
                                grid.Entities.Remove(we);
                            RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render();
                        }
                        foundDoorFeature = true;
                    }
                    if (foundDoorFeature) return;
                }
                if (canDeleteDoors && _doorUpdater.TryRemoveDoor(grid, tileX, tileY))
                {
                    RecalculateDecalPatterns(); SaveState(); UpdateTileGrid(); Render();
                    return;
                }

                // Прототипы из репозитория (точный тип MapEntity) и прочее
                var anyEntity = grid.Entities.FirstOrDefault(e => (int)e.X == tileX && (int)e.Y == tileY);
                if (anyEntity != null)
                {
                    bool allowed;
                    if (anyEntity is PipeEntity || anyEntity is WireEntity ||
                        anyEntity is AirAlarmEntity || anyEntity is FireAlarmEntity)
                    {
                        allowed = false; // обработаны выше
                    }
                    else if (anyEntity.GetType() == typeof(MapEntity))
                    {
                        allowed = canDeletePrototypes;
                    }
                    else
                    {
                        allowed = canDeleteOther; // FirelockEntity и прочие подтипы
                    }
                    if (allowed) { grid.Entities.Remove(anyEntity); UpdateTileGrid(); Render(); }
                    SaveState();
                    return;
                }

                // Декали из репозитория
                var decal = grid.Decals.FirstOrDefault(d => FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY);
                if (decal != null) { if (canDeleteDecals) { grid.Decals.Remove(decal); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Тайлы из репозитория
                var placedTile = grid.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
                if (placedTile != null) { if (canDeleteTiles) { grid.Tiles.Remove(placedTile); SaveState(); UpdateTileGrid(); Render(); } return; }

                // Комнаты
                var room = grid.Rooms.FirstOrDefault(r => tileX >= r.X && tileX < r.X + r.Width && tileY >= r.Y && tileY < r.Y + r.Height);
                if (room != null && canDeleteRooms)
                {
                    // Снос комнаты идёт одним каскадом (RoomCleanup.Remove): вместе с
                    // ней уходят пометки проходов/окон на её клетках — в том числе
                    // те, что записаны у соседей по общей стене, — сущности окон и
                    // решёток и декали узора. Иначе проход в стене оставался навсегда
                    // (его пометку продолжал видеть TileBuilder), а окно с решёткой
                    // висели спрайтами там, где комнаты уже нет. Пометки соседей
                    // влияют на их узоры, поэтому пересчитываем декали.
                    RoomCleanup.Remove(grid, room, _doorUpdater);
                    RecalculateDecalPatterns();
                    SaveState(); UpdateTileGrid(); Render(); return;
                }
            }



            else if (_toolManager.CurrentTool == ToolManager.Tool.DeleteArea)
            {
                _isDeletingArea = true;
                _deleteStartPoint = new Point(tileX, tileY);
                _deleteEndPoint = new Point(tileX, tileY);
                UpdateDeleteAreaPreview();
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

                    if (isOnEdge && room.Windows.Contains((tileX, tileY)))
                    {
                        // Удаляем окно: снимаем пометку окна и сносим сущности
                        // окна и решётки на этой клетке. TileBuilder сам восстановит
                        // стену при следующей пересборке (окна у него в списке пропуска)
                        room.Windows.Remove((tileX, tileY));
                        var winEntities = grid.Entities
                            .Where(e => e.GetType() == typeof(MapEntity) &&
                                        (int)e.X == tileX && (int)e.Y == tileY)
                            .ToList();
                        foreach (var we in winEntities)
                            grid.Entities.Remove(we);
                        RecalculateDecalPatterns();
                        UpdateTileGrid();
                        SaveState();
                        Render();
                    }
                    else if (isOnEdge && room.Passages.Contains((tileX, tileY)))
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

            else if (_toolManager.CurrentTool == ToolManager.Tool.ReplaceWallWithWindow)
            {
                var grid = _map.ActiveGrid;

                // Клетка — настоящая стена: у неё есть сосед вне комнаты (внешний
                // периметр ИЛИ внутренняя стена, возникшая после вырезания). Вложенные
                // комнаты (например, командование с WallReinforced внутри большой
                // комнаты General) перекрывают те же координаты, поэтому комнату ищем
                // не по первому попаданию прямоугольником, а по той, для которой клетка
                // является реальной границей — иначе окно не ставится в укреплённую стену.
                var dirs = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
                if (!_tileGrid.IsWall(tileX, tileY)) return;

                var room = grid.Rooms.FirstOrDefault(r =>
                    r.Contains(tileX, tileY) &&
                    dirs.Any(d => r.HasWallOnSide(tileX, tileY, d.Item1, d.Item2)));

                if (room != null)
                {
                    // Удаляем тайл стены
                    _tileGrid.SetTile(tileX, tileY, TileContent.Empty, null, null, 0);
                    // Добавляем сущность-окно
                    string windowProto = _selectedWindowProto ?? "Window";
                    grid.Entities.Add(new MapEntity
                    {
                        X = tileX + 0.5f,
                        Y = tileY + 0.5f,
                        Proto = windowProto
                    });
                    // Добавляем сущность-решётку под окном (в том же тайле) —
                    // только если окно унаследовано от базового "Window" (проверка
                    // по цепочке parent:), иначе решётка не ставится
                    if (_indexer.IsWindow(windowProto))
                    {
                        grid.Entities.Add(new MapEntity
                        {
                            X = tileX + 0.5f,
                            Y = tileY + 0.5f,
                            Proto = "Grille"
                        });
                    }
                    // Запоминаем клетку как "окно" в самой комнате — иначе
                    // BuildFromRooms при следующем UpdateTileGrid() заново
                    // поставит сюда стену (он перестраивает стены с нуля
                    // из геометрии комнаты, а не из текущего _tileGrid)
                    room.Windows.Add((tileX, tileY));
                    RecalculateDecalPatterns();
                    UpdateTileGrid();
                    SaveState();
                    Render();
                }
            }


            else if (_toolManager.CurrentTool == ToolManager.Tool.ExpandRoom)
            {
                var grid = _map.ActiveGrid;
                var target = FindExpandRoomTarget(e.Location);
                if (target.HasValue)
                {
                    var room = target.Value.room;
                    int dx = target.Value.dx;
                    int dy = target.Value.dy;
                    var cellPos = GetTilePosition(e.Location);

                    if (RoomSubtractor.ExpandRoomBorder(grid, room, cellPos.x, cellPos.y, dx, dy))
                    {
                        _doorUpdater.RecalculateAllDoors(grid);
                        RecalculateDecalPatterns();
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

                    // Только газовые слои (L2/L1/L3 = Distra/Normal/Waste) получают
                    // текущий цвет и тип вентиляции из настроек слоя, зафиксированные
                    // на самой точке в момент простановки (PipeEntity.CustomColor /
                    // VentProto). Util не трогаем — у него свой отдельный механизм
                    // перекраски/разметки конкретных точек через ПКМ по маркеру
                    Color? pipeColor = pipeType != "Util" ? GetPipeLayerColor(pipeType) : null;
                    string? pipeVentProto = pipeType != "Util" ? GetPipeLayerVentProto(pipeType) : null;

                    _pipeBuilder.FinishDrawing(_map.ActiveGrid, pipeType, pipeColor, pipeVentProto); SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.WireHV ||
                     _toolManager.CurrentTool == ToolManager.Tool.WireMV ||
                     _toolManager.CurrentTool == ToolManager.Tool.WireLV)
            {
                if (!_wireBuilder.IsDrawing)
                {
                    _wireBuilder.StartDrawing(tileX, tileY);
                    Render();
                }
                else
                {
                    string wireType = _toolManager.CurrentTool switch
                    {
                        ToolManager.Tool.WireHV => "HV",
                        ToolManager.Tool.WireMV => "MV",
                        _ => "LV"
                    };
                    _wireBuilder.FinishDrawing(_map.ActiveGrid, wireType);
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.PlaceSubstation)
            {
                // Подстанция — переходник ВВ↔СВ. Ставится на клетку с любым из своих
                // кабелей (ВВ или СВ) — второй можно дотянуть потом. Валидность
                // и поворот считает ResolvePowerJunctionPlacement — тот же расчёт,
                // что и в превью.
                var grid = _map.ActiveGrid;
                var (proto, rotation, valid) = ResolvePowerJunctionPlacement(tileX, tileY);

                if (grid != null && valid)
                {
                    grid.Entities.Add(new MapEntity
                    {
                        X = tileX + 0.5f,
                        Y = tileY + 0.5f,
                        Proto = proto,
                        Rotation = rotation
                    });
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }

            else if (_toolManager.CurrentTool == ToolManager.Tool.PlaceApc)
            {
                // ЛКП — переходник СВ↔НВ, та же логика, что у подстанции. Поворот
                // задаётся колёсиком мыши (общий ручной поворот размещения).
                var grid = _map.ActiveGrid;
                var (proto, rotation, valid) = ResolvePowerJunctionPlacement(tileX, tileY);

                if (grid != null && valid)
                {
                    grid.Entities.Add(new MapEntity
                    {
                        X = tileX + 0.5f,
                        Y = tileY + 0.5f,
                        Proto = proto,
                        Rotation = rotation
                    });
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
            else if (IsLinkedEntityTool(_toolManager.CurrentTool))
            {
                PlaceLinkedEntity(_toolManager.CurrentTool, tileX, tileY);
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
            else if (_toolManager.CurrentTool == ToolManager.Tool.NetworkConfigurator)
            {
                var grid = _map.ActiveGrid;
                var room = grid.Rooms.FirstOrDefault(r => r.Contains(tileX, tileY));
                if (room != null)
                {
                    ShowNetworkConfigurator(room);
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









    // ===== ПРИВЯЗКИ: расчёт цели, превью и установка =====

    /// <summary>Инструмент привязки (голопад, шкафчик, скафандр, кровать, камера, комод, интерком).</summary>
    private static bool IsLinkedEntityTool(ToolManager.Tool tool) =>
        tool is ToolManager.Tool.PlaceHolopad or ToolManager.Tool.PlaceLocker
             or ToolManager.Tool.PlaceSuitStorage or ToolManager.Tool.PlaceBed
             or ToolManager.Tool.PlaceCamera or ToolManager.Tool.PlaceDresser
             or ToolManager.Tool.PlaceIntercom or ToolManager.Tool.PlaceFax;

    /// <summary>Инструмент установки сигнализации — воздуха или пожарной.</summary>
    private static bool IsAlarmTool(ToolManager.Tool tool) =>
        tool is ToolManager.Tool.AirAlarm or ToolManager.Tool.FireAlarm;

    /// <summary>
    /// У этой привязки направление можно задать вручную колёсиком мыши.
    /// Пока только интерком: у остальных направление либо не нужно, либо
    /// (как у камеры) считается по стене автоматически.
    /// </summary>
    private static bool LinkedEntityToolSupportsRotation(ToolManager.Tool tool) =>
        tool == ToolManager.Tool.PlaceIntercom;

    /// <summary>
    /// Инструмент, у которого направление размещаемого объекта задаётся колёсиком мыши:
    /// интерком-привязка, ЛКП и обе сигнализации.
    /// </summary>
    private static bool ToolSupportsManualRotation(ToolManager.Tool tool) =>
        LinkedEntityToolSupportsRotation(tool) || tool == ToolManager.Tool.PlaceApc || IsAlarmTool(tool);

    /// <summary>Инструмент установки узла сети — ЛКП или подстанции (ставит обычную сущность).</summary>
    private static bool IsPowerJunctionTool(ToolManager.Tool tool) =>
        tool is ToolManager.Tool.PlaceApc or ToolManager.Tool.PlaceSubstation;

    /// <summary>У этих инструментов под курсором показывается превью размещения.</summary>
    private static bool IsPlacementPreviewTool(ToolManager.Tool tool) =>
        IsLinkedEntityTool(tool) || IsPowerJunctionTool(tool) || IsAlarmTool(tool);

    /// <summary>В клетке уже стоит привязка этого типа.</summary>
    private static bool LinkedEntityCellBusy<T>(Grid grid, int tileX, int tileY) where T : MapEntity =>
        grid.Entities.OfType<T>().Any(e => (int)e.X == tileX && (int)e.Y == tileY);

    /// <summary>
    /// Грань, которой интерком смотрит в комнату, для клетки-стены (автоматический
    /// выбор — если направление задано колёсиком мыши, оно переопределяет этот расчёт).
    /// Сначала берутся стороны, где сосед — пол (внутренность комнаты); у угловых клеток
    /// пола рядом нет, поэтому затем рассматриваются стороны, где сосед принадлежит
    /// комнате. Если подходящих сторон несколько (стена между двумя комнатами),
    /// выбирается та, к грани которой ближе курсор внутри клетки, — направление
    /// (а с ним и комната-владелец) задаётся наведением.
    /// </summary>
    private WallMountOrientation.WallSide ResolveIntercomFacing(Grid grid, int cellX, int cellY, float fracX, float fracY)
    {
        var sides = new[]
        {
            (Side: WallMountOrientation.WallSide.South, Dx: 0, Dy: 1, Distance: 1f - fracY),
            (Side: WallMountOrientation.WallSide.North, Dx: 0, Dy: -1, Distance: fracY),
            (Side: WallMountOrientation.WallSide.East, Dx: 1, Dy: 0, Distance: 1f - fracX),
            (Side: WallMountOrientation.WallSide.West, Dx: -1, Dy: 0, Distance: fracX),
        };

        var floorSide = sides.Where(s => _tileGrid.IsFloor(cellX + s.Dx, cellY + s.Dy))
                             .OrderBy(s => s.Distance)
                             .FirstOrDefault();
        if (floorSide.Side != WallMountOrientation.WallSide.None) return floorSide.Side;

        var roomSide = sides.Where(s => grid.Rooms.Any(r => r.Contains(cellX + s.Dx, cellY + s.Dy)))
                            .OrderBy(s => s.Distance)
                            .FirstOrDefault();
        return roomSide.Side;
    }

    /// <summary>
    /// Комната по ту сторону грани клетки-стены — та, которой интерком принадлежит:
    /// «если интерком висит на стене, он принадлежит комнате, в которую направлен».
    /// </summary>
    private static Room? IntercomFacingRoom(Grid grid, int wallX, int wallY, WallMountOrientation.WallSide facing)
    {
        var (dx, dy) = facing switch
        {
            WallMountOrientation.WallSide.South => (0, 1),
            WallMountOrientation.WallSide.North => (0, -1),
            WallMountOrientation.WallSide.East => (1, 0),
            WallMountOrientation.WallSide.West => (-1, 0),
            _ => (0, 0)
        };

        if (dx == 0 && dy == 0) return null;
        return grid.Rooms.FirstOrDefault(r => r.Contains(wallX + dx, wallY + dy));
    }

    /// <summary>
    /// Что и куда поставит привязка в клетке (tileX, tileY): прототип, точная позиция
    /// в мировых координатах и поворот. Пустой Proto = ставить нельзя (клетка вне
    /// комнаты, у комнаты нет прототипа для этой привязки, стены рядом нет или клетка
    /// уже занята) — превью покажет на этом месте красную рамку.
    ///
    /// Один и тот же расчёт используют и превью под курсором, и установка, поэтому
    /// они не могут разойтись. Позиция интеркома — центр найденного тайла-стены,
    /// остальных привязок — центр клетки комнаты.
    /// </summary>
    private (string Proto, float X, float Y, float Rotation) ResolveLinkedEntityPlacement(ToolManager.Tool tool, int tileX, int tileY)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return ("", tileX + 0.5f, tileY + 0.5f, 0f);

        var room = grid.Rooms.FirstOrDefault(r => r.Contains(tileX, tileY));

        switch (tool)
        {
            // Интерком висит В тайле-стены, поэтому логика такая:
            //  * курсор на клетке комнаты — интерком идёт в тайл ближайшей стены ЭТОЙ
            //    комнаты и смотрит в неё (ей же и принадлежит);
            //  * курсор на самом тайле-стены — цель это сама клетка, а направление
            //    задаётся колёсиком мыши; от направления зависит, какой комнате интерком
            //    принадлежит (её ключ/прототип он и получит);
            //  * если в клетке уже стоит интерком — превью остаётся ровно на ней.
            case ToolManager.Tool.PlaceIntercom:
            {
                var existing = grid.Entities.OfType<IntercomEntity>()
                    .FirstOrDefault(e => (int)e.X == tileX && (int)e.Y == tileY);
                if (existing != null)
                    return ("", existing.X, existing.Y, existing.Rotation);

                int wallX, wallY;
                WallMountOrientation.WallSide facing;
                Room? owner;

                if (_tileGrid.IsWall(tileX, tileY))
                {
                    wallX = tileX;
                    wallY = tileY;

                    if (_placeRotationManual)
                    {
                        // Направление выбрано колёсиком — комната та, в которую он смотрит
                        facing = WallMountOrientation.SideForRotation(_placeRotation);
                    }
                    else
                    {
                        var precise = GetPrecisePosition(_lastMousePosition);
                        facing = ResolveIntercomFacing(grid, tileX, tileY, precise.x - tileX, precise.y - tileY);
                    }

                    owner = IntercomFacingRoom(grid, wallX, wallY, facing);
                }
                else
                {
                    var wallTile = WallMountOrientation.FindNearestWallTile(grid, _tileGrid, tileX, tileY);
                    if (wallTile == null) return ("", tileX + 0.5f, tileY + 0.5f, 0f);

                    // Грань — сторона, где лежит исходная клетка, то есть стена смотрит
                    // обратно в комнату, по которой кликнули.
                    (wallX, wallY, facing) = wallTile.Value;
                    owner = room;
                }

                if (facing == WallMountOrientation.WallSide.None || owner == null ||
                    string.IsNullOrEmpty(owner.IntercomProto) ||
                    LinkedEntityCellBusy<IntercomEntity>(grid, wallX, wallY))
                    return ("", wallX + 0.5f, wallY + 0.5f, 0f);

                return (owner.IntercomProto, wallX + 0.5f, wallY + 0.5f, WallMountOrientation.RotationForSide(facing));
            }

            // Камера ставится по центру клетки, но разворачивается в сторону
            // ближайшей стены (поворот 0 = стена с юга от клетки).
            case ToolManager.Tool.PlaceCamera:
            {
                if (room == null || string.IsNullOrEmpty(room.CameraProto))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                if (LinkedEntityCellBusy<CameraEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);

                var wallSide = WallMountOrientation.FindNearestWall(grid, _tileGrid, tileX + 0.5f, tileY + 0.5f);
                return (room.CameraProto, tileX + 0.5f, tileY + 0.5f, WallMountOrientation.RotationForSide(wallSide));
            }

            case ToolManager.Tool.PlaceHolopad:
                if (room == null || string.IsNullOrEmpty(room.HolopadProto) || LinkedEntityCellBusy<HolopadEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (room.HolopadProto, tileX + 0.5f, tileY + 0.5f, 0f);

            case ToolManager.Tool.PlaceLocker:
                if (room == null || string.IsNullOrEmpty(room.LockerProto) || LinkedEntityCellBusy<LockerEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (room.LockerProto, tileX + 0.5f, tileY + 0.5f, 0f);

            case ToolManager.Tool.PlaceSuitStorage:
                if (room == null || string.IsNullOrEmpty(room.SuitStorageProto) || LinkedEntityCellBusy<SuitStorageEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (room.SuitStorageProto, tileX + 0.5f, tileY + 0.5f, 0f);

            case ToolManager.Tool.PlaceBed:
                if (room == null || string.IsNullOrEmpty(room.BedProto) || LinkedEntityCellBusy<BedEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (room.BedProto, tileX + 0.5f, tileY + 0.5f, 0f);

            case ToolManager.Tool.PlaceDresser:
                if (room == null || string.IsNullOrEmpty(room.DresserProto) || LinkedEntityCellBusy<DresserEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (room.DresserProto, tileX + 0.5f, tileY + 0.5f, 0f);

            // Факс ставится по центру клетки комнаты, как комод. Прототип берётся из
            // свойств комнаты (FaxProto — у капитана свой), а адрес (FaxName) запекается
            // в привязку отдельным полем: он же уходит в компонент FaxMachine при экспорте.
            case ToolManager.Tool.PlaceFax:
                if (room == null || string.IsNullOrEmpty(room.FaxName) || LinkedEntityCellBusy<FaxMachineEntity>(grid, tileX, tileY))
                    return ("", tileX + 0.5f, tileY + 0.5f, 0f);
                return (string.IsNullOrEmpty(room.FaxProto) ? "FaxMachineBase" : room.FaxProto, tileX + 0.5f, tileY + 0.5f, 0f);

            default:
                return ("", tileX + 0.5f, tileY + 0.5f, 0f);
        }
    }

    /// <summary>
    /// Установка привязки по клетке. Цель считает ResolveLinkedEntityPlacement —
    /// тот же расчёт, что и превью под курсором, поэтому нарисованное и поставленное
    /// всегда совпадают.
    /// </summary>
    private void PlaceLinkedEntity(ToolManager.Tool tool, int tileX, int tileY)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return;

        var (proto, x, y, rotation) = ResolveLinkedEntityPlacement(tool, tileX, tileY);
        if (string.IsNullOrEmpty(proto)) return;

        var room = grid.Rooms.FirstOrDefault(r => r.Contains(tileX, tileY));

        var entity = tool switch
        {
            ToolManager.Tool.PlaceHolopad => new HolopadEntity { Proto = proto, HolopadProto = proto } as MapEntity,
            ToolManager.Tool.PlaceLocker => new LockerEntity { Proto = proto, LockerProto = proto },
            ToolManager.Tool.PlaceSuitStorage => new SuitStorageEntity { Proto = proto, SuitStorageProto = proto },
            // Кровать ставится вместе с одеялом комнаты (или без него, если не задано)
            ToolManager.Tool.PlaceBed => new BedEntity { Proto = proto, BedProto = proto, BedsheetProto = room?.BedsheetProto ?? "" },
            ToolManager.Tool.PlaceCamera => new CameraEntity { Proto = proto, CameraProto = proto },
            ToolManager.Tool.PlaceDresser => new DresserEntity { Proto = proto, DresserProto = proto },
            ToolManager.Tool.PlaceIntercom => new IntercomEntity { Proto = proto, IntercomProto = proto },
            // Адрес факса запекается при установке — из свойств комнаты, в которой стоит клетка
            ToolManager.Tool.PlaceFax => new FaxMachineEntity { Proto = proto, FaxProto = proto, FaxName = room?.FaxName ?? "" },
            _ => new MapEntity { Proto = proto }
        };

        entity.X = x;
        entity.Y = y;
        entity.Rotation = rotation;

        grid.Entities.Add(entity);
        SaveState();
        UpdateTileGrid();
        Render();
    }

    /// <summary>
    /// Превью привязки под курсором: пока инструмент привязки выбран, постоянно
    /// показывается тайл, в который она встанет (рамка), спрайт будущего объекта и
    /// его поворот. Обновляется на движение мыши, поворот колёсиком и смену
    /// инструмента.
    /// </summary>
    private void UpdateLinkedEntityPreview()
    {
        var tool = _toolManager.CurrentTool;
        if (!IsLinkedEntityTool(tool))
        {
            _renderer.ClearEntityPreview();
            return;
        }

        var tilePos = GetTilePosition(_lastMousePosition);
        var (proto, x, y, rotation) = ResolveLinkedEntityPlacement(tool, tilePos.x, tilePos.y);
        _renderer.SetEntityPreview(x, y, rotation, proto, null, valid: !string.IsNullOrEmpty(proto));
    }

    /// <summary>
    /// Можно ли поставить выбранный узел сети (ЛКП или подстанцию) в клетку и с каким
    /// поворотом. Узлу достаточно ОДНОГО из своих кабелей на клетке: ЛКП — СВ (MV)
    /// или НВ (LV), подстанция — ВВ (HV) или СВ (MV). Второй кабель можно дотянуть
    /// потом, поэтому требовать сразу оба не нужно. Поворот берётся из ручного
    /// (колёсико мыши); подстанция — неориентированная структура, у неё поворот 0.
    /// </summary>
    private (string Proto, float Rotation, bool Valid) ResolvePowerJunctionPlacement(int tileX, int tileY)
    {
        var grid = _map.ActiveGrid;
        if (grid == null || string.IsNullOrEmpty(_selectedPowerJunctionProto))
            return ("", 0f, false);

        bool wiresOk = _selectedPowerJunctionType switch
        {
            "APC" => _wireBuilder.HasWireOfType(grid, tileX, tileY, "MV") ||
                     _wireBuilder.HasWireOfType(grid, tileX, tileY, "LV"),
            "Substation" => _wireBuilder.HasWireOfType(grid, tileX, tileY, "HV") ||
                            _wireBuilder.HasWireOfType(grid, tileX, tileY, "MV"),
            _ => false
        };
        if (!wiresOk) return ("", 0f, false);

        // Клетка занята простой сущностью (в том числе уже поставленным узлом)
        bool alreadyThere = grid.Entities.Any(en =>
            en.GetType() == typeof(MapEntity) && (int)en.X == tileX && (int)en.Y == tileY);
        if (alreadyThere) return ("", 0f, false);

        float rotation = _selectedPowerJunctionType == "APC" ? _placeRotation : 0f;
        return (_selectedPowerJunctionProto, rotation, true);
    }

    /// <summary>
    /// Превью узла сети (ЛКП/подстанции) под курсором: рамка тайла показывает, можно ли
    /// тут ставить (нужные провода сходятся, клетка свободна), спрайт — сам узел
    /// с его поворотом.
    /// </summary>
    private void UpdatePowerJunctionPreview()
    {
        if (!IsPowerJunctionTool(_toolManager.CurrentTool))
        {
            _renderer.ClearEntityPreview();
            return;
        }

        var tilePos = GetTilePosition(_lastMousePosition);
        var (proto, rotation, valid) = ResolvePowerJunctionPlacement(tilePos.x, tilePos.y);
        _renderer.SetEntityPreview(tilePos.x + 0.5f, tilePos.y + 0.5f, rotation, proto, null, valid);
    }

    /// <summary>
    /// Поворот размещаемого объекта на четверть оборота (колёсико мыши или СКМ у
    /// сигнализаций). Направление считается заданным вручную — оно переопределяет
    /// автоопределение (у интеркома — «вовнутрь комнаты»).
    /// </summary>
    private void RotatePlacementByQuarter(int direction)
    {
        float step = (float)(Math.PI / 2);
        _placeRotation += direction > 0 ? step : -step;
        _placeRotation = (float)(Math.Round(_placeRotation / step) * step);
        _placeRotationManual = true;
    }

    /// <summary>
    /// Можно ли поставить сигнализацию (воздуха или пожарную) в клетку и с каким
    /// поворотом. Условия те же, что и при установке (см. AddAirAlarm/AddFireAlarm):
    /// с включённой привязкой к сетке нужна стена, и вторую такую же сигнализацию
    /// на клетку не поставить.
    /// </summary>
    private (string Proto, float Rotation, bool Valid) ResolveAlarmPlacement(ToolManager.Tool tool, int tileX, int tileY)
    {
        string proto = tool == ToolManager.Tool.AirAlarm ? "AirAlarm" : "FireAlarm";

        var grid = _map.ActiveGrid;
        if (grid == null) return (proto, _placeRotation, false);

        bool wallOk = !_snapToGrid || HasWallAt(grid, tileX, tileY);
        bool busy = tool == ToolManager.Tool.AirAlarm
            ? LinkedEntityCellBusy<AirAlarmEntity>(grid, tileX, tileY)
            : LinkedEntityCellBusy<FireAlarmEntity>(grid, tileX, tileY);

        return (proto, _placeRotation, wallOk && !busy);
    }

    /// <summary>
    /// Превью сигнализации под курсором: тот же спрайт, что появится на карте
    /// (у спрайтов сигнализаций directions: 1, поэтому поворот у них аффинный —
    /// так же, как при отрисовке уже поставленных сигнализаций), плюс красная
    /// рамка, если в этой клетке ставить нельзя.
    /// </summary>
    private void UpdateAlarmPreview()
    {
        if (!IsAlarmTool(_toolManager.CurrentTool))
        {
            _renderer.ClearEntityPreview();
            return;
        }

        var tilePos = GetTilePosition(_lastMousePosition);
        var (proto, rotation, valid) = ResolveAlarmPlacement(_toolManager.CurrentTool, tilePos.x, tilePos.y);
        _renderer.SetEntityPreview(tilePos.x + 0.5f, tilePos.y + 0.5f, rotation, proto, null, valid);
    }

    /// <summary>Превью размещения для текущего инструмента (привязка, узел сети, сигнализация).</summary>
    private void UpdatePlacementPreview()
    {
        if (IsLinkedEntityTool(_toolManager.CurrentTool))
            UpdateLinkedEntityPreview();
        else if (IsAlarmTool(_toolManager.CurrentTool))
            UpdateAlarmPreview();
        else
            UpdatePowerJunctionPreview();
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        // Поворот размещаемого объекта колёсиком — как поворот прототипа при спавне
        // из левого меню: шаг 90°. Работает у интеркома (переопределяет автонаправление
        // «вовнутрь комнаты»), у ЛКП и у сигнализаций; результат сразу видно в превью.
        if (ToolSupportsManualRotation(_toolManager.CurrentTool) && !ModifierKeys.HasFlag(Keys.Control))
        {
            RotatePlacementByQuarter(e.Delta > 0 ? 1 : -1);

            UpdatePlacementPreview();
            Render();
            return;
        }

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

        // Зум к курсору: сначала считаем новый масштаб, затем сдвигаем _viewOffset
        // так, чтобы мировая точка под курсором осталась на том же экранном месте
        // (та же идея, что и в GetTilePosition/ToRect: screen = world*tileSize - viewOffset,
        // поэтому при изменении tileSize нужно домножить (курсор+offset) на отношение
        // масштабов и вычесть обратно координату курсора)
        float oldScale = _scale;
        float zoomDelta = e.Delta > 0 ? 0.1f : -0.1f;
        float newScale = Math.Clamp(_scale + zoomDelta, 0.2f, 3.0f);

        if (newScale != oldScale)
        {
            float ratio = newScale / oldScale;
            _viewOffset.X = (e.Location.X + _viewOffset.X) * ratio - e.Location.X;
            _viewOffset.Y = (e.Location.Y + _viewOffset.Y) * ratio - e.Location.Y;
            _scale = newScale;
        }

        Render();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        // Перетаскивание прямоугольника вырезки декалей (кнопка «✖»)
        if (_isCuttingDecalCells && _cuttingDecalMouseDown)
        {
            HandleDecalCellCutMouseMove(e);
            return;
        }

        // Перетаскивание прямоугольника вырезки КЛЕТОК ФОРМЫ области (кнопка «➖ Вычесть»)
        if (_isSubtractingDecalCells && _subtractingDecalMouseDown)
        {
            HandleDecalCellSubtractMouseMove(e);
            return;
        }

        // Растягивание новой ручной области — живое обновление рамки и декалей
        if (_isDrawingDecalArea && _decalAreaDrawMouseDown)
        {
            HandleDecalAreaDrawMouseMove(e);
            return;
        }

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



        if (_toolManager.CurrentTool == ToolManager.Tool.Magnifier)
        {
            if (!_magnifierPinned)
            {
                // Окошко следует за курсором, пока тайл не закреплён через ЛКМ
                RepositionMagnifierPanel();

                var tilePos = GetTilePosition(e.Location);
                if (tilePos != _lastMagnifierTile)
                {
                    _lastMagnifierTile = tilePos;
                    UpdateMagnifierPanel();
                }
            }
            return;
        }


        // Привязка, узел сети или сигнализация: пока инструмент выбран, под курсором
        // постоянно видно, в какую клетку объект встанет и как будет повёрнут.
        if (IsPlacementPreviewTool(_toolManager.CurrentTool))
        {
            UpdatePlacementPreview();
            Render();
            return;
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


        if (_toolManager.CurrentTool == ToolManager.Tool.ExpandRoom)
        {
            var target = FindExpandRoomTarget(e.Location);
            if (target.HasValue)
            {
                var cellPos = GetTilePosition(e.Location);
                _renderer.SetExpandRoomPreview(target.Value.room, cellPos.x, cellPos.y, target.Value.dx, target.Value.dy);
            }
            else
            {
                _renderer.ClearExpandRoomPreview();
            }
            Render();
            return;
        }
        else
        {
            _renderer.ClearExpandRoomPreview();
        }


        if (_isDeletingArea)
        {
            var tilePos = GetTilePosition(e.Location);
            _deleteEndPoint = new Point(tilePos.x, tilePos.y);
            UpdateDeleteAreaPreview();
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

        if (_wireBuilder.IsDrawing)
        {
            var tilePos = GetTilePosition(e.Location);
            _wireBuilder.UpdateEndPoint(tilePos.x, tilePos.y);
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
        // Завершение перетаскивания прямоугольника вырезки декалей (кнопка «✖»)
        if (_isCuttingDecalCells && _cuttingDecalMouseDown && e.Button == MouseButtons.Left)
        {
            HandleDecalCellCutMouseUp(e);
            return;
        }

        // Завершение перетаскивания прямоугольника вырезки КЛЕТОК ФОРМЫ области (кнопка «➖ Вычесть»)
        if (_isSubtractingDecalCells && _subtractingDecalMouseDown && e.Button == MouseButtons.Left)
        {
            HandleDecalCellSubtractMouseUp(e);
            return;
        }

        // Завершение растягивания новой ручной области
        if (_isDrawingDecalArea && _decalAreaDrawMouseDown && e.Button == MouseButtons.Left)
        {
            HandleDecalAreaDrawMouseUp(e);
            return;
        }

        // Аналогично MouseMove — перехватываем отпускание только если реально
        // тащили угол/область; иначе (например, отпускание ПКМ после панорамирования)
        // отдаём обработку обычной логике ниже.
        if (_editingDecalArea != null && (_draggingDecalCornerIndex >= 0 || _isDraggingDecalWholeArea))
        {
            HandleDecalAreaEditMouseUp(e);
            return;
        }

        // В режиме выбора объекта из конфигуратора сетей отпускание ЛКМ ничего не делает
        if (_networkPickMode && e.Button == MouseButtons.Left)
            return;

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
            bool hasRoomsInArea = grid.Rooms.Any(r => RoomAreaOverlapsBox(r, minX, minY, maxX, maxY));

            if (_deleteSettings.DeleteAll)
            {
                // При DeleteAll удаляем всё, что попало в область
                toRemove.AddRange(grid.Entities.Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY));
                decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        _doorUpdater.TryRemoveDoor(grid, x, y);

                var rooms = grid.Rooms.Where(r => RoomAreaOverlapsBox(r, minX, minY, maxX, maxY)).ToList();
                foreach (var room in rooms)
                {
                    // Тот же каскад, что и в Delete по одной комнате (декали узора,
                    // пометки проходов/окон на её клетках — включая пометки соседей,
                    // сущности окон и решёток). Двери и шлюзы пересчитаем один раз
                    // после цикла, поэтому DoorUpdater сюда не передаём.
                    // Декали узора снимаются по PatternOwnerId = room.Id: их
                    // координаты могут выходить за выделенную область (периметр,
                    // смещённые offset-декали), поэтому фильтр по рамке их не ловит.
                    RoomCleanup.Remove(grid, room);
                }

                if (rooms.Count > 0)
                    _doorUpdater.RecalculateAllDoors(grid);
            }
            else
            {
                // --- Трубы ---
                if (_deleteSettings.DeletePipes)
                    toRemove.AddRange(grid.Entities.OfType<PipeEntity>().Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY));

                // --- Провода ---
                if (_deleteSettings.DeleteWires)
                    toRemove.AddRange(grid.Entities.OfType<WireEntity>().Where(w => w.X >= minX && w.X <= maxX && w.Y >= minY && w.Y <= maxY));

                // --- Сигнализации ---
                if (_deleteSettings.DeleteAlarms)
                {
                    toRemove.AddRange(grid.Entities.OfType<AirAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                    toRemove.AddRange(grid.Entities.OfType<FireAlarmEntity>().Where(a => a.X >= minX && a.X <= maxX && a.Y >= minY && a.Y <= maxY));
                }

                // --- Прототипы (сущности из репозитория, точный тип MapEntity) ---
                if (_deleteSettings.DeletePrototypes)
                {
                    var repoEntities = grid.Entities
                        .Where(e => e.GetType() == typeof(MapEntity))
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .ToList();
                    toRemove.AddRange(repoEntities);
                }

                // --- Другое (FirelockEntity и любые прочие типы, у которых нет своей категории) ---
                if (_deleteSettings.DeleteOther)
                {
                    var knownTypes = new HashSet<Type>
                    {
                        typeof(PipeEntity), typeof(WireEntity),
                        typeof(AirAlarmEntity), typeof(FireAlarmEntity),
                        typeof(MapEntity)
                    };
                    var otherEntities = grid.Entities
                        .Where(e => e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                        .Where(e => !knownTypes.Contains(e.GetType()))
                        .ToList();
                    toRemove.AddRange(otherEntities);
                }

                // --- Декали из репозитория ---
                if (_deleteSettings.DeleteDecals)
                {
                    decalsToRemove.AddRange(grid.Decals.Where(d => d.X >= minX && d.X <= maxX && d.Y >= minY && d.Y <= maxY));
                }

                // --- Тайлы из репозитория ---
                if (_deleteSettings.DeleteTiles)
                {
                    var tilesToRemove = grid.Tiles
                        .Where(t => t.X >= minX && t.X <= maxX && t.Y >= minY && t.Y <= maxY)
                        .ToList();
                    foreach (var t in tilesToRemove)
                        grid.Tiles.Remove(t);
                }

                // --- Двери, проёмы и окна ---
                if (_deleteSettings.DeleteDoors)
                {
                    for (int x = minX; x <= maxX; x++)
                        for (int y = minY; y <= maxY; y++)
                        {
                            _doorUpdater.TryRemoveDoor(grid, x, y);
                            foreach (var rr in grid.Rooms)
                            {
                                if (rr.Contains(x, y))
                                {
                                    rr.Passages.Remove((x, y));
                                    if (rr.Windows.Remove((x, y)))
                                    {
                                        _tileGrid.SetTile(x, y, TileContent.Wall, rr.WallProto, rr.RoomType, rr.GetHashCode());
                                        // Удаляем сущности окна и решётки на этой клетке
                                        var winEntities = grid.Entities
                                            .Where(e => e.GetType() == typeof(MapEntity) &&
                                                        (int)e.X == x && (int)e.Y == y)
                                            .ToList();
                                        foreach (var we in winEntities)
                                            grid.Entities.Remove(we);
                                    }
                                }
                            }
                        }

                    if (hasRoomsInArea)
                        _doorUpdater.RecalculateAllDoors(grid);
                }

                // --- Комнаты ---
                if (_deleteSettings.DeleteRooms)
                {
                    var roomsToRemove = grid.Rooms
                        .Where(r => RoomAreaOverlapsBox(r, minX, minY, maxX, maxY))
                        .ToList();
                    foreach (var room in roomsToRemove)
                    {
                        // Каскадный снос (см. RoomCleanup.Remove): комната, пометки
                        // проходов/окон на её клетках у всех комнат (включая соседей,
                        // если пометка досталась им), сущности окон/решёток и декали
                        // узора (по PatternOwnerId = room.Id). Двери и шлюзы
                        // пересчитываются один раз после цикла.
                        RoomCleanup.Remove(grid, room);
                    }

                    if (roomsToRemove.Count > 0)
                        _doorUpdater.RecalculateAllDoors(grid);
                }
            }

            foreach (var entity in toRemove) grid.Entities.Remove(entity);
            foreach (var decal in decalsToRemove) grid.Decals.Remove(decal);
            SaveState();
            UpdateTileGrid();
            _renderer.ClearDeleteAreaRect();
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
            _renderer.ClearEntityPreview();

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _renderer.ClearSelectionBox();
            }

            Render();
        }
    }


        /// <summary>
    /// Обновляет рамку визуализации инструмента "Удалить область": пересчитывает
    /// прямоугольник (в тайловых координатах, инклюзивно по обоим углам) между
    /// начальной и текущей конечной клеткой и передаёт его рендереру.
    /// </summary>
    private void UpdateDeleteAreaPreview()
    {
        var start = _deleteStartPoint;
        var end = _deleteEndPoint ?? start;
        int minX = Math.Min(start.X, end.X);
        int maxX = Math.Max(start.X, end.X);
        int minY = Math.Min(start.Y, end.Y);
        int maxY = Math.Max(start.Y, end.Y);
        _renderer.SetDeleteAreaRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }


    /// <summary>
    /// Определяет, какую комнату и в какую сторону (dx, dy) можно расширить от
    /// точки под курсором — используется и для подсветки при наведении
    /// (OnMouseMove), и для самого клика (OnMouseDown), чтобы поведение
    /// совпадало один в один. Если клетка под курсором — граница сразу с
    /// нескольких открытых сторон (угол/выступ), направление выбирается по тому,
    /// к какому краю клетки курсор физически ближе.
    /// </summary>
    private (Room room, int dx, int dy)? FindExpandRoomTarget(Point mouseLocation)
    {
        var grid = _map.ActiveGrid;
        if (grid == null) return null;

        var tilePos = GetTilePosition(mouseLocation);
        int tileX = tilePos.x;
        int tileY = tilePos.y;

        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

        var room = grid.Rooms.FirstOrDefault(r =>
            r.Contains(tileX, tileY) &&
            directions.Any(d => r.HasWallOnSide(tileX, tileY, d.Item1, d.Item2)));

        if (room == null) return null;

        var exposed = directions.Where(d => room.HasWallOnSide(tileX, tileY, d.Item1, d.Item2)).ToList();
        if (exposed.Count == 1) return (room, exposed[0].Item1, exposed[0].Item2);

        var precise = GetPrecisePosition(mouseLocation);
        float fracX = precise.x - tileX;
        float fracY = precise.y - tileY;

        (int dx, int dy) best = exposed[0];
        float bestDist = float.MaxValue;
        foreach (var (dx, dy) in exposed)
        {
            float dist = (dx, dy) switch
            {
                (0, -1) => fracY,
                (0, 1) => 1f - fracY,
                (-1, 0) => fracX,
                (1, 0) => 1f - fracX,
                _ => float.MaxValue
            };
            if (dist < bestDist) { bestDist = dist; best = (dx, dy); }
        }

        return (room, best.dx, best.dy);
    }
}
