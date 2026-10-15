// Forms/MainForm.Magnifier.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{
    // ===== Инструмент "Лупа" — окошко со списком объектов под курсором =====


    // Лупа живёт в собственном безрамочном окне без активации: окно двигается силами ОС
    // и не вызывает перерисовку холста с картой (дочерний контрол вызывал бы)
    private MagnifierHostForm? _magnifierForm;
    private System.Windows.Forms.Timer? _magnifierFollowTimer;

    private sealed class MagnifierHostForm : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public MagnifierHostForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }


    private void CreateMagnifierPanel()
    {
        _magnifierPanel = new Panel
        {
            Width = 260,
            Height = 190,
            Dock = DockStyle.Fill,
            // Фон нейтральный — при смене темы окно перекрашивает AppTheme.ApplyToForm
            // (напрямую палитру темы здесь не задаём, т.к. idle-проход всё равно
            // приведёт любой «нейтральный» фон к BackPanel/BackField текущей темы).
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.White
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
            _lastMagnifierTile = (-1, -1);
            RepositionMagnifierPanel();
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
            AutoSize = false,
            BackColor = Color.White,
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

        _magnifierForm = new MagnifierHostForm
        {
            ClientSize = _magnifierPanel.Size
        };
        _magnifierForm.Controls.Add(_magnifierPanel);

        // Таймер подтягивает окошко к курсору, даже если мышь на миг оказалась над самим
        // окошком (тогда холст не получает MouseMove)
        _magnifierFollowTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _magnifierFollowTimer.Tick += (s, e) => RepositionMagnifierPanel();

    }

    /// <summary>
    /// Пересчитывает экранное положение окошка "Лупа" в правом нижнем углу канвы.
    /// Отступ справа равен ширине панели инструментов (200px), отступ снизу — 10px.
    /// </summary>
    private void RepositionMagnifierPanel()
    {
        if (_magnifierForm == null || _canvas == null) return;
        if (!_canvas.IsHandleCreated) return;

        // Закреплённое окошко стоит на месте — за мышью не ходит
        if (_magnifierPinned) return;

        const int cursorGap = 16; // смещение от курсора, чтобы окошко не перекрывало тайл под мышью

        var area = _canvas.RectangleToScreen(_canvas.ClientRectangle);
        if (area.Width <= 0 || area.Height <= 0) return;

        var cursor = Cursor.Position;
        cursor.X = Math.Max(area.Left, Math.Min(cursor.X, area.Right - 1));
        cursor.Y = Math.Max(area.Top, Math.Min(cursor.Y, area.Bottom - 1));

        int w = _magnifierForm.Width;
        int h = _magnifierForm.Height;

        int x = cursor.X + cursorGap;
        int y = cursor.Y + cursorGap;

        // У правого/нижнего края холста переворачиваем смещение
        if (x + w > area.Right)
            x = cursor.X - cursorGap - w;
        if (y + h > area.Bottom)
            y = cursor.Y - cursorGap - h;

        x = Math.Max(area.Left, Math.Min(x, area.Right - w));
        y = Math.Max(area.Top, Math.Min(y, area.Bottom - h));

        var newLocation = new Point(x, y);
        if (_magnifierForm.Location != newLocation)
            _magnifierForm.Location = newLocation;
    }
    /// <summary>
    /// Добавляет одну строку в список: текст слева (обрезается многоточием, если
    /// не помещается) и маленькая кнопка "✕" справа. deleteAction — null, если для
    /// этого типа объекта нет прямого удаления (см. пол/стена ниже — они выводятся
    /// из границ комнаты, а не хранятся как отдельный объект, поэтому кнопки нет).
    ///
    /// placeProtoId — если задан, клик по строке "забирает" прототип из панели
    /// репозитория: включает инструмент "Разместить прототип" с этим ID, как если
    /// бы пользователь выбрал его в списке прототипов (см. ArmPrototypePlacement).
    /// Ряд с кнопкой удаления тоже кликабелен — нажатие на саму строку берет
    /// предмет, нажатие на "✕" удаляет.
    /// </summary>
    private void AddMagnifierRow(string text, Action? deleteAction, string? placeProtoId = null)
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
            Font = new Font("Consolas", 8.5f),
            Cursor = placeProtoId != null ? Cursors.Hand : Cursors.Default
        };
        row.Controls.Add(label);

        if (!string.IsNullOrEmpty(placeProtoId))
        {
            string protoId = placeProtoId;
            label.Click += (s, e) => ArmMagnifierPrototypePlacement(protoId);
            row.Click += (s, e) => ArmMagnifierPrototypePlacement(protoId);
        }

        if (deleteAction != null)
        {
            var btnDelete = new Button
            {
                Text = "🗑",
                Dock = DockStyle.Right,
                Width = 20,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9)
            };
            btnDelete.Click += (s, e) => deleteAction();
            row.Controls.Add(btnDelete);
            // Добавлять кнопку нужно ДО Label с Dock=Fill по Z-порядку контролов,
            // иначе Fill-контрол перекрывает Dock=Right — переставляем на передний план
            btnDelete.BringToFront();
            AppTheme.ApplyToControl(btnDelete);
        }

        _magnifierListPanel.Controls.Add(row);

        // Строки создаются уже после того, как окно один раз прошло темизацию
        // (idle-проход повторно окно не перекрашивает), поэтому новую строку
        // оформляем текущей темой сразу — иначе в тёмной теме она осталась бы светлой.
        AppTheme.ApplyToControl(row);
        AppTheme.ApplyToControl(label);
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

        // Controls.Clear() не освобождает контролы — на каждой смене тайла копились
        // Panel/Label/Button с живыми дескрипторами. Удаляем и освобождаем явно,
        // а раскладку откладываем до конца пересборки списка.
        _magnifierListPanel.SuspendLayout();
        for (int i = _magnifierListPanel.Controls.Count - 1; i >= 0; i--)
        {
            var oldRow = _magnifierListPanel.Controls[i];
            _magnifierListPanel.Controls.RemoveAt(i);
            oldRow.Dispose();
        }

        var grid = _map.ActiveGrid;
        if (grid == null)
        {
            _magnifierHeaderLabel.Text = "🔍 Лупа — нет активного грида";
            if (_btnMagnifierUnpin != null) _btnMagnifierUnpin.Visible = false;
            _magnifierListPanel.ResumeLayout();
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
            AddMagnifierRow($"Пол: {floorTile.ProtoId ?? "Plating"}", null,
                placeProtoId: floorTile.ProtoId ?? "Plating");

        var wallTile = _tileGrid.GetTilesByContent(TileContent.Wall)
            .FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        if (wallTile != null)
            AddMagnifierRow($"Стена: {wallTile.ProtoId ?? "WallSolid"}", null,
                placeProtoId: wallTile.ProtoId ?? "WallSolid");

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
            }), placeProtoId: doorTile.ProtoId ?? "Airlock");
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
            }), placeProtoId: ptRef.Proto);
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
                        AddMagnifierRow(pipeLine, DeleteEntity, placeProtoId: pipe.Proto);
                        break;
                    }
                case FirelockEntity firelock:
                    AddMagnifierRow($"Огнешлюз: {firelock.Proto}{(firelock.IsGlass ? " (стекло)" : "")}", DeleteEntity, placeProtoId: firelock.Proto);
                    break;
                case AirAlarmEntity air:
                    AddMagnifierRow($"Сигнализация воздуха (поворот {air.Rotation * 180 / Math.PI:F0}°)", DeleteEntity, placeProtoId: air.Proto);
                    break;
                case FireAlarmEntity fire:
                    AddMagnifierRow($"Сигнализация пожара (поворот {fire.Rotation * 180 / Math.PI:F0}°)", DeleteEntity, placeProtoId: fire.Proto);
                    break;
                default:
                    AddMagnifierRow($"Сущность: {entity.Proto}", DeleteEntity, placeProtoId: entity.Proto);
                    break;
            }
        }

        // Мощность электросети — подстанция стоит на стыке ВВ/СВ и отдаёт
        // мощность в сеть СВ, ЛКП (APC) стоит на стыке СВ/НВ и отдаёт мощность
        // в сеть НВ. Ищем связную сеть кабеля нужного типа, начиная от клетки
        // под курсором (GetConnectedWireTiles — BFS по соседям), и суммируем
        // maxSupply всех найденных в этой сети устройств.
        foreach (var (wireType, deviceLabel, idPrefix, networkLabel) in new[]
        {
            ("MV", "Подстанция", "Substation", "СВ"),
            ("LV", "ЛКП", "APC", "НВ")
        })
        {
            bool hasWireHere = grid.Entities.OfType<WireEntity>()
                .Any(w => FloorToInt(w.X) == tileX && FloorToInt(w.Y) == tileY && w.WireType == wireType);
            if (!hasWireHere) continue;

            var networkTiles = _wireBuilder.GetConnectedWireTiles(grid, wireType, tileX, tileY);

            var devices = grid.Entities
                .Where(e => e.GetType() == typeof(MapEntity))
                .Where(e => networkTiles.Contains((FloorToInt(e.X), FloorToInt(e.Y))))
                .Where(e => !string.IsNullOrEmpty(e.Proto) && e.Proto.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (devices.Count == 0) continue;

            float total = 0f;
            foreach (var device in devices)
            {
                float supply = _indexer.GetMaxSupply(device.Proto) ?? 0f;
                total += supply;
                AddMagnifierRow($"{deviceLabel}: {device.Proto} ({supply:F0} Вт)", null);
            }
            AddMagnifierRow($"— Сеть {networkLabel}: {total:F0} Вт суммарно ({devices.Count} шт.)", null);
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
                }), placeProtoId: decalRef.Proto);
        }

        if (_magnifierListPanel.Controls.Count == 0)
            AddMagnifierRow("(пусто)", null);

        _magnifierListPanel.ResumeLayout();
    }

    /// <summary>
    /// "Забирает" прототип предмета из строки лупы, как если бы он был выбран в
    /// списке прототипов панели репозитория: ставит _protoToPlace и включает
    /// инструмент "Разместить прототип" (то же, что ArmPrototypePlacement).
    /// После этого окошко лупы скрывается (инструмент сменился) и пользователь
    /// кликом на карте ставит такой же предмет.
    /// </summary>
    private void ArmMagnifierPrototypePlacement(string protoId)
    {
        if (string.IsNullOrEmpty(protoId)) return;

        _protoToPlace = protoId;
        _toolManager.ForceSetTool(ToolManager.Tool.PlacePrototype);
    }
}