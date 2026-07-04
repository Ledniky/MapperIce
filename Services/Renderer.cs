// Services/Renderer.cs
using MapperIce.Models;
using System.Drawing.Drawing2D;

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

                DrawWallTilesBatch(g, wallTiles, tileGrid, tileSize, viewOffset, grid.Position, opacity);

                DrawDoorTilesBatch(g, doorTiles, tileSize, viewOffset, grid.Position);

                var firelocks = grid.Entities.OfType<FirelockEntity>().ToList();
                DrawFirelocksBatch(g, firelocks, tileSize, viewOffset, grid.Position);

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

                // ИСПРАВЛЕНО: передаём как List<MapEntity>
                var airAlarms = grid.Entities.OfType<AirAlarmEntity>().ToList();
                var fireAlarms = grid.Entities.OfType<FireAlarmEntity>().ToList();
                
                DrawAlarmsBatch(g, airAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, grid.Position, "AirAlarm", Color.FromArgb(200, 255, 200, 100));
                DrawAlarmsBatch(g, fireAlarms.Cast<MapEntity>().ToList(), tileSize, viewOffset, grid.Position, "FireAlarm", Color.FromArgb(200, 255, 100, 100));
            }

            DrawInfo(g, scale, toolName, map);

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
        int innerW = Math.Max(0, room.Width - 1);
        int innerH = Math.Max(0, room.Height - 1);
        
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        var rect = new Rectangle((int)x, (int)y, innerW * tileSize, innerH * tileSize);
        int alpha = (int)(room.FillColor.A * opacity);
        using var brush = new SolidBrush(Color.FromArgb(alpha, room.FillColor.R, room.FillColor.G, room.FillColor.B));
        g.FillRectangle(brush, rect);
    }

    private void DrawRoomLine(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        int innerW = Math.Max(0, room.Width - 1);
        int innerH = Math.Max(0, room.Height - 1);
        
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        var rect = new Rectangle((int)x, (int)y, innerW * tileSize, innerH * tileSize);

        Color color = isCurrent ? Color.Red : Color.FromArgb((int)(room.LineColor.A * opacity), room.LineColor.R, room.LineColor.G, room.LineColor.B);
        using var pen = new Pen(color, isCurrent ? 3 : 2);
        g.DrawRectangle(pen, rect);

        if (!HideRoomOverlay && tileSize > 20 && opacity > 0.3f)
        {
            int innerWText = Math.Max(0, room.Width - 2);
            int innerHText = Math.Max(0, room.Height - 2);
            
            if (innerWText > 0 && innerHText > 0)
            {
                using var font = new Font("Arial", Math.Min(10, tileSize / 3));
                Color textColor = GetContrastColor(room.FillColor);
                int alpha = (int)(200 * opacity);
                using var brush = new SolidBrush(Color.FromArgb(alpha, textColor));
                g.DrawString($"{innerWText}×{innerHText}", font, brush, rect.X + 2, rect.Y + 2);
            }
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
        g.DrawString($"Инструмент: {toolName}{mode}{pipeMode}  Масштаб: {scale:P0}  Активный грид: {name}  Всего гридов: {map.Grids.Count}",
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

    #endregion
}