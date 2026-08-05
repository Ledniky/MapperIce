// Services/Renderer.cs
using MapperIce.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private readonly PrototypeIndexer? _indexer;
    private readonly TileBuilder _tileBuilder;
    private readonly PipeBuilder _pipeBuilder;
    private readonly string _rootPath = "";
    public bool HideRoomOverlay { get; set; } = false;
    private MapData? _currentMap;
    public bool ShowPipeOverlay { get; set; } = true;

    // Кэш текстур для ускорения рендеринга
    private readonly Dictionary<string, Image?> _textureCache = new();
    private readonly Dictionary<string, Rectangle> _sourceRectCache = new();

    // Размеры тайлов в пикселях для кэширования
    private int _cachedTileSize = 0;

    // Интерполяция для масштабирования
    private readonly InterpolationMode _interpolationMode = InterpolationMode.NearestNeighbor;
    private AlarmNetwork? _currentNetwork;
    public bool ShowAlarmConnections { get; set; } = true;

    // Предпросмотр сигнализации
    private bool _showAlarmPreview = false;
    private int _previewX;
    private int _previewY;
    private float _previewRotation;
    private string _previewType = "";
    private bool _showEntityPreview = false;
    private float _previewEntityX;
    private float _previewEntityY;
    private float _previewEntityRotation;
    private string _previewEntityProto = "";
    private List<object> _selection = new();
    private bool _showSelectionBox = false;
    private Point _selectionBoxStart;
    private Point _selectionBoxEnd;


    public void SetSelectionBox(Point start, Point end)
    {
        _selectionBoxStart = start;
        _selectionBoxEnd = end;
        _showSelectionBox = true;
    }

    public void ClearSelectionBox()
    {
        _showSelectionBox = false;
    }


    public void SetSelection(List<object> selection)
    {
        _selection = selection;
    }

    public Renderer(int width, int height, PrototypeIndexer? indexer, TileBuilder tileBuilder, PipeBuilder pipeBuilder)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
        _tileBuilder = tileBuilder;
        _pipeBuilder = pipeBuilder;
        if (_indexer != null)
            _rootPath = _indexer.GetRootPath();
    }

    public void Resize(int width, int height)
    {
        lock (_lock)
        {
            if (width > 0 && height > 0)
            {
                _buffer = new Bitmap(width, height);
                _cachedTileSize = 0;
            }
        }
    }

    public Bitmap Render(MapData map, float scale, PointF viewOffset, Room? currentRoom, string toolName)
    {
        _currentMap = map;

        lock (_lock)
        {
            if (_buffer.Width == 0 || _buffer.Height == 0) return _buffer;

            using var g = Graphics.FromImage(_buffer);

            g.InterpolationMode = _interpolationMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.Clear(Color.White);

            int tileSize = (int)(Constants.TILE_SIZE * scale);

            if (_cachedTileSize != tileSize)
            {
                _cachedTileSize = tileSize;
            }

            var visibleGrids = map.Grids.Where(g => g.IsVisible).ToList();

            foreach (var grid in visibleGrids)
            {
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;

                var tileGrid = _tileBuilder.BuildFromRooms(grid);

                var floorTiles = tileGrid.GetTilesByContent(TileContent.Floor).ToList();
                var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall).ToList();
                var doorTiles = tileGrid.GetTilesByContent(TileContent.Door).ToList();

                DrawGrid(g, tileSize, viewOffset, grid.Position, opacity);

                DrawFloorTilesBatch(g, floorTiles, tileSize, viewOffset, grid.Position, opacity);

                var floorUnderDoors = doorTiles
                    .Where(t => t.HasFloorUnder && !string.IsNullOrEmpty(t.FloorProtoUnder))
                    .Select(t => new TileData
                    {
                        X = t.X,
                        Y = t.Y,
                        Content = TileContent.Floor,
                        ProtoId = t.FloorProtoUnder ?? "Plating"
                    })
                    .ToList();
                DrawFloorTilesBatch(g, floorUnderDoors, tileSize, viewOffset, grid.Position, opacity);
                
                DrawDecalsBatch(g, grid.Decals, tileSize, viewOffset, grid.Position, opacity);

                DrawWallTilesBatch(g, wallTiles, tileGrid, tileSize, viewOffset, grid.Position, opacity);

                DrawDoorTilesBatch(g, doorTiles, tileSize, viewOffset, grid.Position);

                var firelocks = grid.Entities.OfType<FirelockEntity>().ToList();
                DrawFirelocksBatch(g, firelocks, tileSize, viewOffset, grid.Position);

                var genericEntities = grid.Entities
                    .Where(e => e is not PipeEntity && e is not FirelockEntity &&
                                e is not AirAlarmEntity && e is not FireAlarmEntity)
                    .Select(e => new MapEntity { Proto = e.Proto, X = e.X, Y = e.Y, ParentGridUid = e.ParentGridUid, Rotation = e.Rotation })
                    .ToList();
                DrawGenericEntitiesBatch(g, genericEntities, tileSize, viewOffset, grid.Position);

                if (!HideRoomOverlay)
                {
                    var rooms = grid.Rooms.ToList();
                    DrawRoomFillsBatch(g, rooms, tileSize, viewOffset, grid.Position, opacity);

                    if (currentRoom != null && isActive)
                    {
                        DrawRoomFill(g, currentRoom, tileSize, viewOffset, grid.Position, 1.0f);
                        DrawRoomLine(g, currentRoom, tileSize, viewOffset, grid.Position, true, 1.0f);
                    }
                    else
                    {
                        DrawRoomLinesBatch(g, rooms, tileSize, viewOffset, grid.Position, false, opacity);
                    }
                }

                if (ShowPipeOverlay)
                {
                    var allPipes = _pipeBuilder.GetPipes(grid);
                    DrawPipeLinesBatch(g, allPipes, tileSize, viewOffset, grid.Position);

                    if (allPipes.Count > 0)
                    {
                        DrawPipeDotsBatch(g, allPipes, tileSize, viewOffset, grid.Position);
                    }

                    if (_pipeBuilder.IsDrawing && _pipeBuilder.StartPoint.HasValue)
                    {
                        var start = _pipeBuilder.StartPoint.Value;
                        var end = _pipeBuilder.EndPoint ?? start;
                        var path = CalculatePipePath(start, end);
                        DrawTempPipePath(g, path, tileSize, viewOffset, grid.Position);
                    }
                }

                var airAlarms = grid.Entities.OfType<AirAlarmEntity>().ToList();
                var fireAlarms = grid.Entities.OfType<FireAlarmEntity>().ToList();

                DrawAlarmsBatch(g, airAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, grid.Position, "AirAlarm", Color.FromArgb(200, 255, 200, 100));
                DrawAlarmsBatch(g, fireAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, grid.Position, "FireAlarm", Color.FromArgb(200, 255, 100, 100));

                if (ShowAlarmConnections && ShowPipeOverlay && _currentNetwork != null)
                {
                    DrawAlarmConnections(g, _currentNetwork, tileSize, viewOffset, grid.Position);
                }

                // Рисуем стрелки направления у существующих сигнализаций
                DrawAlarmDirectionArrows(g, grid, scale, viewOffset);
            }

            DrawInfo(g, scale, toolName, map);

            // Рисуем предпросмотр сигнализации под курсором
            DrawAlarmPreview(g, scale, viewOffset);

            // Рисуем предпросмотр размещаемого прототипа под курсором
            DrawEntityPreview(g, scale, viewOffset);
            DrawSelectionHighlight(g, scale, viewOffset);
            DrawSelectionBox(g);

            return _buffer;
        }
    }

    #region Оптимизированные методы пакетной отрисовки

    private void DrawFloorTilesBatch(Graphics g, List<TileData> tiles, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (tiles.Count == 0) return;

        var grouped = tiles.GroupBy(t => t.ProtoId ?? "Plating");

        foreach (var group in grouped)
        {
            string protoId = group.Key;
            Image? texture = GetOrLoadTexture(protoId);

            foreach (var tile in group)
            {
                float tileX = (tile.X + gridOffset.X) * tileSize - viewOffset.X;
                float tileY = (tile.Y + gridOffset.Y) * tileSize - viewOffset.Y;
                var rect = new Rectangle((int)tileX, (int)tileY, tileSize, tileSize);

                if (texture != null)
                {
                    var srcRect = GetSourceRect(protoId, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    using var brush = new SolidBrush(Color.FromArgb((int)(150 * opacity), 200, 200, 200));
                    g.FillRectangle(brush, rect);
                }
            }
        }
    }




private void DrawDecalsBatch(Graphics g, List<PlacedDecal> decals, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (decals == null || decals.Count == 0) return;

        foreach (var group in decals.GroupBy(d => d.Proto))
        {
            string protoId = group.Key;
            Image? texture = GetOrLoadTexture(protoId);

            foreach (var decal in group)
            {
                // decal.X/Y — это точная мировая координата (как у MapEntity), а не индекс
                // тайла, поэтому центрируем прямоугольник текстуры так же, как для
                // обычных сущностей (DrawGenericEntitiesBatch), а не рисуем от угла как тайл
                float cx = (decal.X + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (decal.Y + gridOffset.Y) * tileSize - viewOffset.Y;
                var rect = new Rectangle((int)(cx - tileSize / 2f), (int)(cy - tileSize / 2f), tileSize, tileSize);

                var oldTransform = g.Transform;

                if (decal.Rotation != 0)
                {
                    var matrix = new Matrix();
                    float angleDegrees = decal.Rotation * 180 / (float)Math.PI;
                    matrix.RotateAt(angleDegrees, new PointF(cx, cy));
                    g.Transform = matrix;
                }

                if (texture != null)
                {
                    var srcRect = GetSourceRect(protoId, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    using var brush = new SolidBrush(Color.FromArgb((int)(160 * opacity), 255, 220, 120));
                    g.FillRectangle(brush, rect);
                }

                g.Transform = oldTransform;
            }
        }
    }



    private void DrawWallTilesBatch(Graphics g, List<TileData> tiles, TileGrid tileGrid, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (tiles.Count == 0) return;

        var grouped = new Dictionary<string, List<TileData>>();

        foreach (var tile in tiles)
        {
            string wallProto = _tileBuilder.GetBestWallAt(tileGrid, tile.X, tile.Y);
            if (!grouped.ContainsKey(wallProto))
                grouped[wallProto] = new List<TileData>();
            grouped[wallProto].Add(tile);
        }

        foreach (var group in grouped)
        {
            string wallProto = group.Key;
            Image? texture = GetOrLoadTexture(wallProto);

            foreach (var tile in group.Value)
            {
                float wx = (tile.X + gridOffset.X) * tileSize - viewOffset.X;
                float wy = (tile.Y + gridOffset.Y) * tileSize - viewOffset.Y;
                var rect = new Rectangle((int)wx, (int)wy, tileSize, tileSize);

                if (texture != null)
                {
                    var srcRect = GetSourceRect(wallProto, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    using var pen = new Pen(Color.Gray, 1);
                    g.DrawRectangle(pen, rect);
                }
            }
        }
    }

    private void DrawDoorTilesBatch(Graphics g, List<TileData> tiles, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (tiles.Count == 0) return;

        var grouped = tiles.GroupBy(t => t.ProtoId ?? "Airlock");

        foreach (var group in grouped)
        {
            string protoId = group.Key;
            Image? texture = GetOrLoadTexture(protoId);

            foreach (var tile in group)
            {
                float x = (tile.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X - tileSize / 2f;
                float y = (tile.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y - tileSize / 2f;
                var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

                if (texture != null)
                {
                    var srcRect = GetSourceRect(protoId, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    using var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255));
                    g.FillRectangle(brush, rect);
                    using var pen = new Pen(Color.DarkBlue, 2);
                    g.DrawRectangle(pen, rect);
                    using var font = new Font("Segoe UI", 14);
                    using var textBrush = new SolidBrush(Color.White);
                    g.DrawString("🚪", font, textBrush, rect.X + 4, rect.Y + 2);
                }
            }
        }
    }

    private void DrawFirelocksBatch(Graphics g, List<FirelockEntity> firelocks, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (firelocks.Count == 0) return;

        foreach (var firelock in firelocks)
        {
            float x = (firelock.X + gridOffset.X) * tileSize - viewOffset.X;
            float y = (firelock.Y + gridOffset.Y) * tileSize - viewOffset.Y;
            var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

            Image? texture = GetOrLoadTexture(firelock.Proto);

            if (texture != null)
            {
                var srcRect = GetSourceRect(firelock.Proto, texture);
                g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
            }
            else
            {
                Color color = firelock.IsGlass ? Color.FromArgb(150, 100, 200, 255) : Color.FromArgb(200, 200, 100, 100);
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, rect);
                using var pen = new Pen(Color.Black, 1);
                g.DrawRectangle(pen, rect);

                using var font = new Font("Segoe UI", tileSize / 3, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.White);
                g.DrawString("🔥", font, textBrush, rect.X + tileSize / 4, rect.Y + tileSize / 4);
            }
        }
    }

    private void DrawPipeLinesBatch(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        var grouped = pipes.GroupBy(p => p.PipeType);
        var pipeDict = pipes.ToDictionary(p => (p.X, p.Y), p => p);

        foreach (var group in grouped)
        {
            Color color = GetPipeColor(group.Key);
            using var pen = new Pen(color, Math.Max(2, tileSize / 10));

            foreach (var pipe in group)
            {
                float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

                var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
                foreach (var (dx, dy) in directions)
                {
                    var key = (pipe.X + dx, pipe.Y + dy);
                    if (pipeDict.ContainsKey(key))
                    {
                        float nx = (key.Item1 + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                        float ny = (key.Item2 + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
                        g.DrawLine(pen, cx, cy, nx, ny);
                    }
                }
            }
        }
    }

    private void DrawPipeDotsBatch(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        float dotSize = Math.Max(4, tileSize / 6);

        var grouped = pipes.GroupBy(p => p.PipeType);

        foreach (var group in grouped)
        {
            Color color = GetPipeDotColor(group.Key);
            using var brush = new SolidBrush(color);
            using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);

            foreach (var pipe in group)
            {
                float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

                g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
                g.DrawEllipse(borderPen, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
            }
        }
    }

    /// <summary>
    /// ОБОБЩЁННЫЙ МЕТОД для отрисовки сигнализации
    /// </summary>
    private void DrawAlarmsBatch(Graphics g, List<MapEntity> alarms, int tileSize, PointF viewOffset, PointF gridOffset, string protoId, Color bgColor)
    {
        if (alarms.Count == 0) return;

        Image? texture = GetOrLoadTexture(protoId);

        foreach (var entity in alarms)
        {
            float x = (entity.X + gridOffset.X) * tileSize - viewOffset.X;
            float y = (entity.Y + gridOffset.Y) * tileSize - viewOffset.Y;
            var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

            float rotation = 0;
            if (entity is AirAlarmEntity airAlarm)
                rotation = airAlarm.Rotation;
            else if (entity is FireAlarmEntity fireAlarm)
                rotation = fireAlarm.Rotation;

            if (texture != null)
            {
                var oldTransform = g.Transform;

                if (rotation != 0)
                {
                    var matrix = new Matrix();
                    float angleDegrees = rotation * 180 / (float)Math.PI;
                    matrix.RotateAt(angleDegrees, new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
                    g.Transform = matrix;
                }

                var srcRect = GetSourceRect(protoId, texture);
                g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);

                g.Transform = oldTransform;
            }
            else
            {
                using var brush = new SolidBrush(bgColor);
                g.FillRectangle(brush, rect);
                using var pen = new Pen(Color.Black, 1);
                g.DrawRectangle(pen, rect);

                string icon = protoId == "AirAlarm" ? "🔊" : "🔥";
                using var font = new Font("Segoe UI", tileSize / 2, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.Black);
                g.DrawString(icon, font, textBrush, rect.X + tileSize / 4, rect.Y + tileSize / 4);

                using var arrowPen = new Pen(Color.Red, 2);
                float cx = rect.X + rect.Width / 2;
                float cy = rect.Y + rect.Height / 2;
                float radius = tileSize / 2 - 4;
                float angle = rotation;
                g.DrawLine(arrowPen, cx, cy, cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
            }
        }
    }

    private void DrawRoomFillsBatch(Graphics g, List<Room> rooms, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        if (rooms.Count == 0) return;

        foreach (var room in rooms)
        {
            DrawRoomFill(g, room, tileSize, viewOffset, gridOffset, opacity);
        }
    }

    private void DrawRoomLinesBatch(Graphics g, List<Room> rooms, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        if (rooms.Count == 0) return;

        foreach (var room in rooms)
        {
            DrawRoomLine(g, room, tileSize, viewOffset, gridOffset, false, opacity);
        }
    }

    private void DrawTempPipePath(Graphics g, List<(int x, int y)> path, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (path.Count == 0) return;

        float dotSize = Math.Max(4, tileSize / 6);
        using var brush = new SolidBrush(Color.FromArgb(120, 0, 255, 100));

        foreach (var pos in path)
        {
            float cx = (pos.x + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float cy = (pos.y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
            g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
        }
    }

    #endregion

    #region Кэширование текстур

    private Image? GetOrLoadTexture(string protoId)
    {
        if (string.IsNullOrEmpty(protoId)) return null;

        if (_textureCache.TryGetValue(protoId, out var cached))
            return cached;

        Image? texture = null;
        if (_indexer != null)
        {
            var texturePath = _indexer.GetFullTexturePath(protoId);
            if (texturePath != null && File.Exists(texturePath))
            {
                try
                {
                    if (protoId == "Firelock" || protoId == "FirelockGlass")
                    {
                        string directory = Path.GetDirectoryName(texturePath)!;
                        string fileName = Path.GetFileName(texturePath);
                        if (fileName.Equals("closed.png", StringComparison.OrdinalIgnoreCase))
                        {
                            string openPath = Path.Combine(directory, "open.png");
                            if (File.Exists(openPath))
                            {
                                texturePath = openPath;
                            }
                        }
                    }

                    texture = Image.FromFile(texturePath);
                }
                catch { }
            }
        }

        _textureCache[protoId] = texture;
        return texture;
    }

    private Rectangle GetSourceRect(string protoId, Image img)
    {
        if (img == null) return Rectangle.Empty;

        string key = $"{protoId}_{img.Width}_{img.Height}";
        if (_sourceRectCache.TryGetValue(key, out var cached))
            return cached;

        int w = img.Width, h = img.Height;
        Rectangle rect;

        if (w == 32 && h == 32) rect = new Rectangle(0, 0, 32, 32);
        else if (w == 32 && h >= 32) rect = new Rectangle(0, 0, 32, 32);
        else if (w >= 32 && h == 32) rect = new Rectangle(0, 0, 32, 32);
        else if (w > 32 || h > 32) rect = new Rectangle(0, 0, Math.Min(32, w), Math.Min(32, h));
        else rect = new Rectangle(0, 0, w, h);

        _sourceRectCache[key] = rect;
        return rect;
    }

    public void ClearCache()
    {
        foreach (var kvp in _textureCache)
        {
            if (kvp.Value != null)
                kvp.Value.Dispose();
        }
        _textureCache.Clear();
        _sourceRectCache.Clear();
    }

    #endregion

    #region Вспомогательные методы

    private Color GetPipeColor(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => Color.FromArgb(180, 100, 200, 255),
            "Waste" => Color.FromArgb(180, 255, 150, 150),
            "Normal" => Color.FromArgb(180, 200, 200, 200),
            _ => Color.FromArgb(180, 150, 150, 150)
        };
    }

    private Color GetPipeDotColor(string pipeType)
    {
        return pipeType switch
        {
            "Distra" => Color.FromArgb(200, 100, 200, 255),
            "Waste" => Color.FromArgb(200, 255, 150, 150),
            "Normal" => Color.FromArgb(200, 200, 200, 200),
            _ => Color.FromArgb(200, 150, 150, 150)
        };
    }

    #endregion

    #region Остальные методы

    private void DrawGrid(Graphics g, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int alpha = (int)(180 * opacity);
        using var pen = new Pen(Color.FromArgb(alpha, 200, 200, 200), 1);

        float offsetX = viewOffset.X - (gridOffset.X * tileSize);
        float offsetY = viewOffset.Y - (gridOffset.Y * tileSize);

        int startX = (int)(-offsetX % tileSize);
        int startY = (int)(-offsetY % tileSize);
        if (startX < 0) startX += tileSize;
        if (startY < 0) startY += tileSize;

        for (int x = startX; x <= _buffer.Width; x += tileSize)
            g.DrawLine(pen, x, 0, x, _buffer.Height);
        for (int y = startY; y <= _buffer.Height; y += tileSize)
            g.DrawLine(pen, 0, y, _buffer.Width, y);
    }

    private void DrawRoomFill(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        float startX = (room.X + gridOffset.X) * tileSize - viewOffset.X;
        float startY = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        float width = room.Width * tileSize;
        float height = room.Height * tileSize;

        float offset = tileSize / 2f;
        var rect = new Rectangle(
            (int)(startX + offset),
            (int)(startY + offset),
            (int)(width - tileSize),
            (int)(height - tileSize)
        );

        int alpha = (int)(room.FillColor.A * opacity);
        using var brush = new SolidBrush(Color.FromArgb(alpha, room.FillColor.R, room.FillColor.G, room.FillColor.B));
        g.FillRectangle(brush, rect);
    }

    private void DrawRoomLine(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        float startX = (room.X + gridOffset.X) * tileSize - viewOffset.X;
        float startY = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        float width = room.Width * tileSize;
        float height = room.Height * tileSize;

        float offset = tileSize / 2f;
        var rect = new Rectangle(
            (int)(startX + offset),
            (int)(startY + offset),
            (int)(width - tileSize),
            (int)(height - tileSize)
        );

        Color color = isCurrent ? Color.Red : Color.FromArgb((int)(room.LineColor.A * opacity), room.LineColor.R, room.LineColor.G, room.LineColor.B);
        using var pen = new Pen(color, isCurrent ? 3 : 2);
        g.DrawRectangle(pen, rect);

        if (!HideRoomOverlay && tileSize > 20 && opacity > 0.3f)
        {
            using var font = new Font("Arial", Math.Min(10, tileSize / 3));
            Color textColor = GetContrastColor(room.FillColor);
            int alpha = (int)(200 * opacity);
            using var brush = new SolidBrush(Color.FromArgb(alpha, textColor));

            int innerWidth = Math.Max(0, room.Width - 2);
            int innerHeight = Math.Max(0, room.Height - 2);
            g.DrawString($"{innerWidth}×{innerHeight}", font, brush, rect.X + 2, rect.Y + 2);
        }
    }

    private Color GetContrastColor(Color backgroundColor)
    {
        int brightness = (int)(backgroundColor.R * 0.299 + backgroundColor.G * 0.587 + backgroundColor.B * 0.114);
        return brightness < 128 ? Color.White : Color.Black;
    }

    private void DrawInfo(Graphics g, float scale, string toolName, MapData map)
    {
        using var font = new Font("Arial", 12, FontStyle.Bold);
        using var brush = new SolidBrush(Color.DarkGray);
        var name = map.ActiveGrid?.Name ?? "Нет";
        string mode = HideRoomOverlay ? " [ОВЕРЛЕЙ СКРЫТ]" : "";
        string pipeMode = ShowPipeOverlay ? "" : " [ТРУБЫ СКРЫТЫ]";
        string connectionsMode = ShowAlarmConnections ? "" : " [СВЯЗИ СКРЫТЫ]";
        g.DrawString($"Инструмент: {toolName}{mode}{pipeMode}{connectionsMode}  Масштаб: {scale:P0}  Активный грид: {name}  Всего гридов: {map.Grids.Count}",
            font, brush, 10, 10);
    }

    private List<(int x, int y)> CalculatePipePath((int x, int y) start, (int x, int y) end)
    {
        var positions = new List<(int x, int y)>();

        int startX = start.x;
        int startY = start.y;
        int endX = end.x;
        int endY = end.y;

        int stepY = startY <= endY ? 1 : -1;
        for (int y = startY; y != endY + stepY; y += stepY)
        {
            positions.Add((startX, y));
        }

        int stepX = startX <= endX ? 1 : -1;
        int startXPos = startX + stepX;
        for (int x = startXPos; x != endX + stepX; x += stepX)
        {
            positions.Add((x, endY));
        }

        return positions;
    }

    private void DrawAlarmConnections(Graphics g, AlarmNetwork network, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (network == null || network.Connections.Count == 0) return;

        foreach (var connection in network.Connections)
        {
            float sx = (connection.Source.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float sy = (connection.Source.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            float tx = (connection.Target.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
            float ty = (connection.Target.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

            using (var pen = new Pen(connection.LineColor, connection.LineWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, sx, sy, tx, ty);
            }

            float dotSize = 4;
            using (var brush = new SolidBrush(connection.LineColor))
            {
                g.FillEllipse(brush, sx - dotSize / 2, sy - dotSize / 2, dotSize, dotSize);
                g.FillEllipse(brush, tx - dotSize / 2, ty - dotSize / 2, dotSize, dotSize);
            }
        }
    }

    public void SetAlarmNetwork(AlarmNetwork network)
    {
        _currentNetwork = network;
    }

    public void SetAlarmPreview(int x, int y, float rotation, string type)
    {
        _previewX = x;
        _previewY = y;
        _previewRotation = rotation;
        _previewType = type;
        _showAlarmPreview = true;
    }

    public void ClearAlarmPreview()
    {
        _showAlarmPreview = false;
    }

    private void DrawAlarmPreview(Graphics g, float scale, PointF viewOffset)
    {
        if (!_showAlarmPreview || string.IsNullOrEmpty(_previewType)) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        float gridOffsetX = _currentMap.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _currentMap.ActiveGrid.Position.Y * tileSize;

        float screenX = _previewX * tileSize + gridOffsetX - viewOffset.X;
        float screenY = _previewY * tileSize + gridOffsetY - viewOffset.Y;

        // Рисуем полупрозрачный фон тайла
        using (var brush = new SolidBrush(Color.FromArgb(60, 100, 200, 255)))
        {
            g.FillRectangle(brush, screenX, screenY, tileSize, tileSize);
        }

        // Рисуем рамку
        using (var pen = new Pen(Color.FromArgb(200, 0, 200, 255), 2))
        {
            g.DrawRectangle(pen, screenX, screenY, tileSize, tileSize);
        }

        // Рисуем иконку сигнализации
        float centerX = screenX + tileSize / 2;
        float centerY = screenY + tileSize / 2;
        float iconSize = tileSize * 0.4f;

        var state = g.Save();

        g.TranslateTransform(centerX, centerY);
        g.RotateTransform(_previewRotation * 180 / (float)Math.PI);

        string iconText = _previewType == "AirAlarm" ? "🔊" : "🔥";
        using (var font = new Font("Segoe UI", iconSize, FontStyle.Regular))
        using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
        {
            var size = g.MeasureString(iconText, font);
            g.DrawString(iconText, font, brush, -size.Width / 2, -size.Height / 2);
        }

        // Стрелка направления
        float arrowLength = tileSize * 0.35f;
        using (var pen = new Pen(Color.FromArgb(200, 0, 255, 255), 3))
        {
            pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
            g.DrawLine(pen, 0, 0, 0, -arrowLength);
        }

        // Круг в центре
        using (var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255)))
        {
            g.FillEllipse(brush, -4, -4, 8, 8);
        }

        g.Restore(state);

        // Текст с типом сигнализации под тайлом
        using (var font = new Font("Arial", 8, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.White))
        using (var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        {
            string label = _previewType == "AirAlarm" ? "Воздух" : "Пожар";
            var size = g.MeasureString(label, font);
            float textX = screenX + tileSize / 2 - size.Width / 2;
            float textY = screenY + tileSize + 2;

            g.DrawString(label, font, shadow, textX + 1, textY + 1);
            g.DrawString(label, font, brush, textX, textY);
        }
    }


    private void DrawEntityPreview(Graphics g, float scale, PointF viewOffset)
    {
        if (!_showEntityPreview || string.IsNullOrEmpty(_previewEntityProto)) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        float gridOffsetX = _currentMap.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _currentMap.ActiveGrid.Position.Y * tileSize;

        float cx = _previewEntityX * tileSize + gridOffsetX - viewOffset.X;
        float cy = _previewEntityY * tileSize + gridOffsetY - viewOffset.Y;
        var rect = new Rectangle((int)(cx - tileSize / 2f), (int)(cy - tileSize / 2f), tileSize, tileSize);

        var oldTransform = g.Transform;

        if (_previewEntityRotation != 0)
        {
            var matrix = new Matrix();
            float angleDegrees = _previewEntityRotation * 180 / (float)Math.PI;
            matrix.RotateAt(angleDegrees, new PointF(cx, cy));
            g.Transform = matrix;
        }

        Image? texture = GetOrLoadTexture(_previewEntityProto);

        if (texture != null)
        {
            var srcRect = GetSourceRect(_previewEntityProto, texture);
            g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
        }
        else
        {
            using var brush = new SolidBrush(Color.FromArgb(120, 255, 0, 255));
            g.FillRectangle(brush, rect);
            using var pen = new Pen(Color.FromArgb(180, 0, 0, 0), 1);
            g.DrawRectangle(pen, rect);
        }

        g.Transform = oldTransform;
    }

    private void DrawAlarmDirectionArrows(Graphics g, Grid grid, float scale, PointF viewOffset)
    {
        if (!ShowAlarmConnections) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        float gridOffsetX = grid.Position.X * tileSize;
        float gridOffsetY = grid.Position.Y * tileSize;

        var alarms = grid.Entities
            .OfType<AirAlarmEntity>()
            .Cast<MapEntity>()
            .Concat(grid.Entities.OfType<FireAlarmEntity>())
            .ToList();

        foreach (var alarm in alarms)
        {
            float screenX = (float)alarm.X * tileSize + gridOffsetX - viewOffset.X;
            float screenY = (float)alarm.Y * tileSize + gridOffsetY - viewOffset.Y;
            float centerX = screenX + tileSize / 2;
            float centerY = screenY + tileSize / 2;

            float rotation = alarm is AirAlarmEntity air ? air.Rotation :
                            (alarm as FireAlarmEntity)?.Rotation ?? 0;

            float arrowLength = tileSize * 0.4f;

            var state = g.Save();
            g.TranslateTransform(centerX, centerY);
            g.RotateTransform(rotation * 180 / (float)Math.PI);

            using (var pen = new Pen(Color.FromArgb(180, 255, 255, 0), 2))
            {
                pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                g.DrawLine(pen, 0, 0, 0, -arrowLength);
            }

            g.Restore(state);
        }
    }

    private void DrawGenericEntitiesBatch(Graphics g, List<MapEntity> entities, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (entities.Count == 0) return;

        foreach (var group in entities.GroupBy(e => e.Proto))
        {
            string protoId = group.Key;
            Image? texture = GetOrLoadTexture(protoId);

            foreach (var entity in group)
            {
                float cx = (entity.X + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (entity.Y + gridOffset.Y) * tileSize - viewOffset.Y;
                var rect = new Rectangle((int)(cx - tileSize / 2f), (int)(cy - tileSize / 2f), tileSize, tileSize);

                var oldTransform = g.Transform;

                if (entity.Rotation != 0)
                {
                    var matrix = new Matrix();
                    float angleDegrees = entity.Rotation * 180 / (float)Math.PI;
                    matrix.RotateAt(angleDegrees, new PointF(cx, cy));
                    g.Transform = matrix;
                }

                if (texture != null)
                {
                    var srcRect = GetSourceRect(protoId, texture);
                    g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
                }
                else
                {
                    using var brush = new SolidBrush(Color.FromArgb(180, 255, 0, 255));
                    g.FillRectangle(brush, rect);
                    using var pen = new Pen(Color.Black, 1);
                    g.DrawRectangle(pen, rect);

                    if (tileSize > 16)
                    {
                        using var font = new Font("Segoe UI", 6);
                        using var textBrush = new SolidBrush(Color.White);
                        string label = protoId.Length > 8 ? protoId.Substring(0, 8) : protoId;
                        g.DrawString(label, font, textBrush, rect.X + 1, rect.Y + 1);
                    }
                }

                g.Transform = oldTransform;
            }
        }
    }


    private void DrawSelectionBox(Graphics g)
    {
        if (!_showSelectionBox) return;

        int x = Math.Min(_selectionBoxStart.X, _selectionBoxEnd.X);
        int y = Math.Min(_selectionBoxStart.Y, _selectionBoxEnd.Y);
        int w = Math.Abs(_selectionBoxEnd.X - _selectionBoxStart.X);
        int h = Math.Abs(_selectionBoxEnd.Y - _selectionBoxStart.Y);
        var rect = new Rectangle(x, y, w, h);

        using var fillBrush = new SolidBrush(Color.FromArgb(45, 255, 140, 0));
        g.FillRectangle(fillBrush, rect);

        using var pen = new Pen(Color.FromArgb(255, 255, 140, 0), 1.5f)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        g.DrawRectangle(pen, rect);
    }





    private void DrawSelectionHighlight(Graphics g, float scale, PointF viewOffset)
    {
        if (_selection == null || _selection.Count == 0) return;
        if (_currentMap?.ActiveGrid == null) return;

        int tileSize = (int)(Constants.TILE_SIZE * scale);
        float gridOffsetX = _currentMap.ActiveGrid.Position.X * tileSize;
        float gridOffsetY = _currentMap.ActiveGrid.Position.Y * tileSize;

        // Контрастная "обводка": тёмная подложка + яркий пунктир поверх —
        // читается и на белом, и на тёмном фоне
        using var outlinePen = new Pen(Color.FromArgb(220, 0, 0, 0), 4);
        using var pen = new Pen(Color.FromArgb(255, 255, 60, 0), 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            DashPattern = new float[] { 4, 3 }
        };

        foreach (var obj in _selection)
        {
            Rectangle rect;

switch (obj)
            {
                case Room room:
                    float rx = (room.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ry = (room.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)rx, (int)ry, (int)(room.Width * tileSize), (int)(room.Height * tileSize));
                    break;

                case PlacedDecal decal:
                    float dx = (decal.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float dy = (decal.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)(dx - tileSize / 2f), (int)(dy - tileSize / 2f), tileSize, tileSize);
                    break;

                case MapEntity entity:
                    float ex = (entity.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ey = (entity.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)(ex - tileSize / 2f), (int)(ey - tileSize / 2f), tileSize, tileSize);
                    break;

                case PlacedTile tile:
                    float tx = (tile.X + gridOffsetX / tileSize) * tileSize - viewOffset.X;
                    float ty = (tile.Y + gridOffsetY / tileSize) * tileSize - viewOffset.Y;
                    rect = new Rectangle((int)tx, (int)ty, tileSize, tileSize);
                    break;

                default:
                    continue;
            }




            g.DrawRectangle(outlinePen, rect);
            g.DrawRectangle(pen, rect);
        }
    }
    public void SetEntityPreview(float x, float y, float rotation, string proto)
    {
        _previewEntityX = x;
        _previewEntityY = y;
        _previewEntityRotation = rotation;
        _previewEntityProto = proto;
        _showEntityPreview = true;
    }

    public void ClearEntityPreview()
    {
        _showEntityPreview = false;
    }

    #endregion
}