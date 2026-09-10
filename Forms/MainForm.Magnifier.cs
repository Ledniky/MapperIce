// Forms/MainForm.Magnifier.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    // ===== Инструмент "Лупа" — окошко со списком объектов под курсором =====

    private void CreateMagnifierPanel()
    {
        _magnifierPanel = new Panel
        {
            Width = 260,
            Height = 190,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(255, 250, 250, 235),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.FromArgb(255, 225, 225, 190)
        };

        _magnifierHeaderLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🔍 Лупа",
            Font = new Font("Arial", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        headerPanel.Controls.Add(_magnifierHeaderLabel);

        _btnMagnifierUnpin = new Button
        {
            Dock = DockStyle.Right,
            Width = 82,
            Text = "🔓 Открепить",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Arial", 7),
            Visible = false
        };
        _btnMagnifierUnpin.Click += (s, e) =>
        {
            _magnifierPinned = false;
            _magnifierPinnedTile = null;
            UpdateMagnifierPanel();
        };
        headerPanel.Controls.Add(_btnMagnifierUnpin);

        _magnifierPanel.Controls.Add(headerPanel);

        // Список строк — не TextBox, т.к. у каждой строки своя кнопка удаления.
        // AutoScroll + WrapContents=false + FlowDirection=TopDown даёт вертикальный
        // список с прокруткой, растущий по одной строке за раз.
        _magnifierListPanel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(255, 255, 255, 245),
            Top = headerPanel.Height,
            Width = _magnifierPanel.Width,
            Height = _magnifierPanel.Height - headerPanel.Height
        };
        _magnifierPanel.Controls.Add(_magnifierListPanel);

        _magnifierPanel.Resize += (s, e) =>
        {
            if (_magnifierListPanel != null)
            {
                _magnifierListPanel.Width = _magnifierPanel.Width;
                _magnifierListPanel.Height = _magnifierPanel.Height - headerPanel.Height;
            }
        };

        headerPanel.BringToFront();

        _canvas.Controls.Add(_magnifierPanel);
        _magnifierPanel.BringToFront();

        // На момент вызова этого метода (конструктор MainForm) реальный докинг ещё
        // не выполнен — форма ещё не показана, WindowState=Maximized ещё не применён,
        // поэтому _canvas.Width тут может быть равен начальному Size(1024,768) формы,
        // а не финальному размеру экрана. Пересчёт всё равно происходит (на случай,
        // если размеры уже корректны), но окончательно верное положение гарантируется
        // повторным вызовом из OnResize (см. MainForm.cs) и из OnToolChanged при
        // каждом включении инструмента — оба момента наступают уже после реального
        // докинга.
        RepositionMagnifierPanel();
    }

    /// <summary>
    /// Пересчитывает экранное положение окошка "Лупа" в правом нижнем углу канвы.
    /// Отступ справа равен ширине панели инструментов (200px), отступ снизу — 10px.
    /// </summary>
    private void RepositionMagnifierPanel()
    {
        if (_magnifierPanel == null || _canvas == null) return;
        if (_canvas.Width <= 0 || _canvas.Height <= 0) return;

        const int rightMargin = 200; // ширина панели инструментов
        _magnifierPanel.Location = new Point(
            Math.Max(0, _canvas.Width - _magnifierPanel.Width - rightMargin),
            Math.Max(0, _canvas.Height - _magnifierPanel.Height - 10));
    }

    /// <summary>
    /// Добавляет одну строку в список: текст слева (обрезается многоточием, если
    /// не помещается) и маленькая кнопка "✕" справа. deleteAction — null, если для
    /// этого типа объекта нет прямого удаления (см. пол/стена ниже — они выводятся
    /// из границ комнаты, а не хранятся как отдельный объект, поэтому кнопки нет).
    /// </summary>
    private void AddMagnifierRow(string text, Action? deleteAction)
    {
        if (_magnifierListPanel == null) return;

        int rowWidth = Math.Max(50, _magnifierListPanel.ClientSize.Width - 4);

        var row = new Panel
        {
            Width = rowWidth,
            Height = 20,
            Margin = new Padding(1)
        };

        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5f)
        };
        row.Controls.Add(label);

        if (deleteAction != null)
        {
            var btnDelete = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 20,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 7),
                ForeColor = Color.DarkRed
            };
            btnDelete.Click += (s, e) => deleteAction();
            row.Controls.Add(btnDelete);
            // Добавлять кнопку нужно ДО Label с Dock=Fill по Z-порядку контролов,
            // иначе Fill-контрол перекрывает Dock=Right — переставляем на передний план
            btnDelete.BringToFront();
        }

        _magnifierListPanel.Controls.Add(row);
    }

    /// <summary>
    /// Выполняет удаление объекта из-под конкретной строки и обновляет список —
    /// после удаления содержимое клетки меняется (например, был последний объект
    /// на тайле), поэтому список нужно построить заново.
    /// </summary>
    private void DeleteMagnifierObject(Action deleteAction)
    {
        deleteAction();
        UpdateMagnifierPanel();
    }

    /// <summary>
    /// Пересчитывает содержимое окошка "Лупа". В обычном режиме — по клетке под
    /// текущим положением курсора (_lastMousePosition), обновляется на каждое
    /// движение мыши. После ЛКМ по тайлу (см. OnMouseDown в MainForm.MouseInput.cs)
    /// содержимое "фиксируется" (_magnifierPinned=true) на зафиксированной клетке
    /// (_magnifierPinnedTile) и больше не следует за курсором, пока пользователь
    /// не нажмёт "Открепить" или не переключит инструмент (см. OnToolChanged).
    /// </summary>
    private void UpdateMagnifierPanel()
    {
        if (_magnifierPanel == null || _magnifierListPanel == null || _magnifierHeaderLabel == null)
            return;

        _magnifierListPanel.Controls.Clear();

        var grid = _map.ActiveGrid;
        if (grid == null)
        {
            _magnifierHeaderLabel.Text = "🔍 Лупа — нет активного грида";
            if (_btnMagnifierUnpin != null) _btnMagnifierUnpin.Visible = false;
            return;
        }

        int tileX, tileY;
        if (_magnifierPinned && _magnifierPinnedTile.HasValue)
        {
            tileX = _magnifierPinnedTile.Value.x;
            tileY = _magnifierPinnedTile.Value.y;
        }
        else
        {
            var tilePos = GetTilePosition(_lastMousePosition);
            tileX = tilePos.x;
            tileY = tilePos.y;
        }

        if (_btnMagnifierUnpin != null)
            _btnMagnifierUnpin.Visible = _magnifierPinned;

        string pinMark = _magnifierPinned ? " [закреплено]" : "";
        _magnifierHeaderLabel.Text = $"🔍 Тайл ({tileX}, {tileY}){pinMark}";

        // Пол и стена выводятся из границ комнаты (TileBuilder.BuildFromRooms),
        // а не хранятся как самостоятельный объект — прямого способа "удалить"
        // именно этот тайл пола/стены нет (см. правило "геометрия комнаты
        // производная, не хранимая" в принципах проекта), поэтому у этих двух
        // строк кнопки удаления не будет.
        var floorTile = _tileGrid.GetTilesByContent(TileContent.Floor)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (floorTile != null)
            AddMagnifierRow($"Пол: {floorTile.ProtoId ?? "Plating"}", null);

        var wallTile = _tileGrid.GetTilesByContent(TileContent.Wall)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (wallTile != null)
            AddMagnifierRow($"Стена: {wallTile.ProtoId ?? "WallSolid"}", null);

        // Дверь — удаляется через DoorUpdater, как и в инструменте "Удалить"
        var doorTile = _tileGrid.GetTilesByContent(TileContent.Door)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (doorTile != null)
        {
            string doorLine = $"Дверь: {doorTile.ProtoId ?? "Airlock"}";
            if (doorTile.HasFloorUnder && !string.IsNullOrEmpty(doorTile.FloorProtoUnder))
                doorLine += $" (пол под: {doorTile.FloorProtoUnder})";

            int dtx = tileX, dty = tileY;
            AddMagnifierRow(doorLine, () => DeleteMagnifierObject(() =>
            {
                if (_doorUpdater.TryRemoveDoor(grid, dtx, dty))
                {
                    RecalculateDecalPatterns();
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }
            }));
        }

        // Вручную размещённые тайлы (инструмент "Разместить прототип" → tile)
        foreach (var pt in grid.Tiles.Where(t => t.X == tileX && t.Y == tileY).ToList())
        {
            var ptRef = pt;
            AddMagnifierRow($"Ручной тайл: {ptRef.Proto}", () => DeleteMagnifierObject(() =>
            {
                grid.Tiles.Remove(ptRef);
                SaveState();
                UpdateTileGrid();
                Render();
            }));
        }

        // Комнаты, реально владеющие этой клеткой (учитывает вырезы через Room.Contains)
        foreach (var room in grid.Rooms.Where(r => r.Contains(tileX, tileY)).ToList())
        {
            var roomRef = room;
            AddMagnifierRow(
                $"Комната: {roomRef.RoomType} [{roomRef.Width}×{roomRef.Height} @ ({roomRef.X},{roomRef.Y})]",
                () => DeleteMagnifierObject(() =>
                {
                    // Та же последовательность, что и в Delete-инструменте по комнате:
                    // сначала снести декали внутри её границ, потом саму комнату,
                    // потом пересчитать двери всей карты
                    var roomDecals = grid.Decals
                        .Where(d => d.X >= roomRef.X && d.X < roomRef.X + roomRef.Width &&
                                    d.Y >= roomRef.Y && d.Y < roomRef.Y + roomRef.Height)
                        .ToList();
                    foreach (var rd in roomDecals)
                        grid.Decals.Remove(rd);

                    grid.Rooms.Remove(roomRef);
                    _doorUpdater.RecalculateAllDoors(grid);
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }));
        }

        // Сущности — попадание проверяем через floor(X)/floor(Y), как и весь
        // остальной код (HitTestAt/Delete), чтобы декали/сущности с дробными
        // координатами у края клетки тоже засчитывались как "в этом тайле"
        foreach (var entity in grid.Entities.Where(e => FloorToInt(e.X) == tileX && FloorToInt(e.Y) == tileY).ToList())
        {
            var entityRef = entity;
            void DeleteEntity() => DeleteMagnifierObject(() =>
            {
                grid.Entities.Remove(entityRef);
                SaveState();
                UpdateTileGrid();
                Render();
            });

            switch (entity)
            {
                case PipeEntity pipe:
                    {
                        string pipeLine = $"Труба [{pipe.PipeType}]";
                        if (pipe.CustomColor.HasValue)
                        {
                            var c = pipe.CustomColor.Value;
                            pipeLine += $" цвет:#{c.R:X2}{c.G:X2}{c.B:X2}";
                        }
                        if (pipe.HasFilterMarker && !string.IsNullOrEmpty(pipe.FilterLabel))
                            pipeLine += $" метка:\"{pipe.FilterLabel}\"";
                        pipeLine += pipe.EndpointType switch
                        {
                            EndpointType.MailingUnit => " [MailingUnit]",
                            EndpointType.DisposalUnit => " [DisposalUnit]",
                            _ => ""
                        };
                        AddMagnifierRow(pipeLine, DeleteEntity);
                        break;
                    }
                case FirelockEntity firelock:
                    AddMagnifierRow($"Огнешлюз: {firelock.Proto}{(firelock.IsGlass ? " (стекло)" : "")}", DeleteEntity);
                    break;
                case AirAlarmEntity air:
                    AddMagnifierRow($"Сигнализация воздуха (поворот {air.Rotation * 180 / Math.PI:F0}°)", DeleteEntity);
                    break;
                case FireAlarmEntity fire:
                    AddMagnifierRow($"Сигнализация пожара (поворот {fire.Rotation * 180 / Math.PI:F0}°)", DeleteEntity);
                    break;
                default:
                    AddMagnifierRow($"Сущность: {entity.Proto}", DeleteEntity);
                    break;
            }
        }

        // Декали
        foreach (var decal in grid.Decals.Where(d => FloorToInt(d.X) == tileX && FloorToInt(d.Y) == tileY).ToList())
        {
            var decalRef = decal;
            AddMagnifierRow($"Декаль: {decalRef.Proto} [{decalRef.Color}, поворот {decalRef.Rotation * 180 / Math.PI:F0}°]",
                () => DeleteMagnifierObject(() =>
                {
                    grid.Decals.Remove(decalRef);
                    SaveState();
                    UpdateTileGrid();
                    Render();
                }));
        }

        if (_magnifierListPanel.Controls.Count == 0)
            AddMagnifierRow("(пусто)", null);
    }
}