// Services/Renderer.cs
using MapperIce.Models;

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
                _buffer = new Bitmap(width, height);
        }
    }

    public Bitmap Render(MapData map, float scale, PointF viewOffset, Room? currentRoom, string toolName)
    {
        _currentMap = map;
        
        lock (_lock)
        {
            if (_buffer.Width == 0 || _buffer.Height == 0) return _buffer;

            using var g = Graphics.FromImage(_buffer);
            g.Clear(Color.White);

            int tileSize = (int)(Constants.TILE_SIZE * scale);

            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;

                var tileGrid = _tileBuilder.BuildFromRooms(grid);

                // 1. Сетка
                DrawGrid(g, tileSize, viewOffset, grid.Position, opacity);

                // 2. Полы
                foreach (var tile in tileGrid.GetTilesByContent(TileContent.Floor))
                {
                    DrawFloorTile(g, tile, tileSize, viewOffset, grid.Position, opacity);
                }

                // 3. Стены
                foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
                {
                    string bestWall = _tileBuilder.GetBestWallAt(tileGrid, tile.X, tile.Y);
                    DrawWallAt(g, bestWall, tile.X, tile.Y, tileSize, viewOffset, grid.Position, opacity);
                }

                // 4. Двери
                foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
                {
                    DrawDoorAt(g, tile, tileSize, viewOffset, grid.Position);
                }

                // 4.5. ПОЖАРНЫЕ ШЛЮЗЫ
                foreach (var entity in grid.Entities)
                {
                    if (entity is FirelockEntity firelock)
                    {
                        DrawFirelock(g, firelock, tileSize, viewOffset, grid.Position);
                    }
                }

                // 5. Оверлей комнат
                if (!HideRoomOverlay)
                {
                    foreach (var room in grid.Rooms)
                    {
                        DrawRoomFill(g, room, tileSize, viewOffset, grid.Position, opacity);
                        DrawRoomLine(g, room, tileSize, viewOffset, grid.Position, false, opacity);
                    }
                }

                if (currentRoom != null && isActive)
                {
                    DrawRoomFill(g, currentRoom, tileSize, viewOffset, grid.Position, 1.0f);
                    DrawRoomLine(g, currentRoom, tileSize, viewOffset, grid.Position, true, 1.0f);
                }

                // 6. ТРУБЫ
                if (ShowPipeOverlay)
                {
                    var allPipes = _pipeBuilder.GetPipes(grid);
                    
                    DrawPipeLines(g, allPipes, tileSize, viewOffset, grid.Position);
                    
                    foreach (var pipe in allPipes)
                    {
                        DrawPipeDot(g, pipe, tileSize, viewOffset, grid.Position);
                    }

                    if (_pipeBuilder.IsDrawing && _pipeBuilder.StartPoint.HasValue)
                    {
                        var start = _pipeBuilder.StartPoint.Value;
                        var end = _pipeBuilder.EndPoint ?? start;
                        
                        var path = CalculatePipePath(start, end);
                        foreach (var pos in path)
                        {
                            DrawTempPipeAt(g, pos.x, pos.y, tileSize, viewOffset, grid.Position);
                        }
                    }
                }

                // 7. СИГНАЛИЗАЦИЯ (AirAlarm, FireAlarm)
                foreach (var entity in grid.Entities)
                {
                    if (entity is AirAlarmEntity airAlarm)
                    {
                        DrawAlarm(g, airAlarm, tileSize, viewOffset, grid.Position, "AirAlarm", Color.FromArgb(200, 255, 200, 100));
                    }
                    else if (entity is FireAlarmEntity fireAlarm)
                    {
                        DrawAlarm(g, fireAlarm, tileSize, viewOffset, grid.Position, "FireAlarm", Color.FromArgb(200, 255, 100, 100));
                    }
                }
            }

            DrawInfo(g, scale, toolName, map);

            return _buffer;
        }
    }

    // ============ МЕТОДЫ ДЛЯ ТРУБ ============

    private void DrawPipeLines(Graphics g, List<PipeEntity> pipes, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        if (pipes.Count == 0) return;

        var grouped = pipes.GroupBy(p => p.PipeType);

        foreach (var group in grouped)
        {
            Color color = group.Key switch
            {
                "Distra" => Color.FromArgb(180, 100, 200, 255),
                "Waste" => Color.FromArgb(180, 255, 150, 150),
                "Normal" => Color.FromArgb(180, 200, 200, 200),
                _ => Color.FromArgb(180, 150, 150, 150)
            };

            var pipeDict = new Dictionary<(float x, float y), PipeEntity>();
            foreach (var pipe in group)
            {
                var key = (pipe.X, pipe.Y);
                if (!pipeDict.ContainsKey(key))
                {
                    pipeDict[key] = pipe;
                }
            }

            using var pen = new Pen(color, Math.Max(2, tileSize / 10));

            foreach (var pipe in pipeDict.Values)
            {
                float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

                var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

                foreach (var (dx, dy) in directions)
                {
                    var key = (pipe.X + dx, pipe.Y + dy);
                    if (pipeDict.ContainsKey(key))
                    {
                        // ИСПОЛЬЗУЕМ Item1 И Item2 ВМЕСТО x И y
                        float nx = (key.Item1 + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
                        float ny = (key.Item2 + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
                        g.DrawLine(pen, cx, cy, nx, ny);
                    }
                }
            }
        }
    }

    private void DrawPipeDot(Graphics g, PipeEntity pipe, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        float cx = (pipe.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float cy = (pipe.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        Color color = pipe.PipeType switch
        {
            "Distra" => Color.FromArgb(200, 100, 200, 255),
            "Waste" => Color.FromArgb(200, 255, 150, 150),
            "Normal" => Color.FromArgb(200, 200, 200, 200),
            _ => Color.FromArgb(200, 150, 150, 150)
        };

        float dotSize = Math.Max(4, tileSize / 6);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);

        using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
        g.DrawEllipse(borderPen, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
    }

    private void DrawTempPipeAt(Graphics g, int x, int y, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        float cx = (x + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float cy = (y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        using var brush = new SolidBrush(Color.FromArgb(120, 0, 255, 100));
        float dotSize = Math.Max(4, tileSize / 6);
        g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
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

    // ============ МЕТОДЫ ДЛЯ СИГНАЛИЗАЦИИ ============

    private void DrawAlarm(Graphics g, MapEntity entity, int tileSize, PointF viewOffset, PointF gridOffset, string protoId, Color bgColor)
    {
        float x = (entity.X + gridOffset.X) * tileSize - viewOffset.X;
        float y = (entity.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

        float rotation = 0;
        if (entity is AirAlarmEntity airAlarm)
            rotation = airAlarm.Rotation;
        else if (entity is FireAlarmEntity fireAlarm)
            rotation = fireAlarm.Rotation;

        Image? texture = null;
        if (_indexer != null)
        {
            var texturePath = _indexer.GetFullTexturePath(protoId);
            if (texturePath != null && File.Exists(texturePath))
            {
                try
                {
                    texture = Image.FromFile(texturePath);
                }
                catch { }
            }
        }

        if (texture != null)
        {
            var oldTransform = g.Transform;
            
            if (rotation != 0)
            {
                var matrix = new System.Drawing.Drawing2D.Matrix();
                float angleDegrees = rotation * 180 / (float)Math.PI;
                matrix.RotateAt(angleDegrees, new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
                g.Transform = matrix;
            }
            
            var srcRect = GetSourceRect(texture);
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
            
            // Стрелка направления
            using var arrowPen = new Pen(Color.Red, 2);
            float cx = rect.X + rect.Width / 2;
            float cy = rect.Y + rect.Height / 2;
            float radius = tileSize / 2 - 4;
            float angle = rotation;
            g.DrawLine(arrowPen, cx, cy, cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
        }
    }

    // ============ МЕТОДЫ ДЛЯ ПОЖАРНЫХ ШЛЮЗОВ ============

    private void DrawFirelock(Graphics g, FirelockEntity firelock, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        float x = (firelock.X + gridOffset.X) * tileSize - viewOffset.X;
        float y = (firelock.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

        Image? texture = null;
        if (_indexer != null)
        {
            var texturePath = _indexer.GetFullTexturePath(firelock.Proto);
            if (texturePath != null && File.Exists(texturePath))
            {
                try
                {
                    // Заменяем closed.png на open.png
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
                    texture = Image.FromFile(texturePath);
                }
                catch { }
            }
        }

        if (texture != null)
        {
            var srcRect = GetSourceRect(texture);
            g.DrawImage(texture, rect, srcRect, GraphicsUnit.Pixel);
        }
        else
        {
            // Fallback
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

    // ============ ОСТАЛЬНЫЕ МЕТОДЫ ============

    private Rectangle GetSourceRect(Image img)
    {
        int w = img.Width, h = img.Height;
        if (w == 32 && h == 32) return new Rectangle(0, 0, 32, 32);
        if (w == 32 && h >= 32) return new Rectangle(0, 0, 32, 32);
        if (w >= 32 && h == 32) return new Rectangle(0, 0, 32, 32);
        if (w > 32 || h > 32) return new Rectangle(0, 0, Math.Min(32, w), Math.Min(32, h));
        return new Rectangle(0, 0, w, h);
    }

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

    private void DrawFloorTile(Graphics g, TileData tile, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        float tileX = (tile.X + gridOffset.X) * tileSize - viewOffset.X;
        float tileY = (tile.Y + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)tileX, (int)tileY, tileSize, tileSize);

        Image? floorTexture = null;
        if (_indexer != null && !string.IsNullOrEmpty(tile.ProtoId))
        {
            var floorPath = _indexer.GetFullTexturePath(tile.ProtoId);
            if (floorPath != null && File.Exists(floorPath))
            {
                try 
                { 
                    floorTexture = Image.FromFile(floorPath);
                }
                catch { }
            }
        }

        if (floorTexture != null)
        {
            var srcRect = GetSourceRect(floorTexture);
            g.DrawImage(floorTexture, rect, srcRect, GraphicsUnit.Pixel);
        }
        else
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(150 * opacity), 200, 200, 200));
            g.FillRectangle(brush, rect);
        }
    }

    private void DrawWallAt(Graphics g, string wallProto, int x, int y, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        float wx = (x + gridOffset.X) * tileSize - viewOffset.X;
        float wy = (y + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)wx, (int)wy, tileSize, tileSize);

        Image? wallTexture = null;
        if (_indexer != null && !string.IsNullOrEmpty(wallProto))
        {
            var wallPath = _indexer.GetFullTexturePath(wallProto);
            if (wallPath != null && File.Exists(wallPath))
            {
                try 
                { 
                    wallTexture = Image.FromFile(wallPath);
                }
                catch { }
            }
        }

        if (wallTexture != null)
        {
            var srcRect = GetSourceRect(wallTexture);
            g.DrawImage(wallTexture, rect, srcRect, GraphicsUnit.Pixel);
        }
        else
        {
            using var pen = new Pen(Color.Gray, 1);
            g.DrawRectangle(pen, rect);
        }
    }

    private void DrawDoorAt(Graphics g, TileData tile, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        float x = (tile.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X - tileSize / 2f;
        float y = (tile.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y - tileSize / 2f;
        var rect = new Rectangle((int)x, (int)y, tileSize, tileSize);

        Image? doorTexture = null;
        if (_indexer != null && !string.IsNullOrEmpty(tile.ProtoId))
        {
            var doorPath = _indexer.GetFullTexturePath(tile.ProtoId);
            if (doorPath != null && File.Exists(doorPath))
            {
                try 
                { 
                    doorTexture = Image.FromFile(doorPath);
                }
                catch { }
            }
        }

        if (doorTexture != null)
        {
            var srcRect = GetSourceRect(doorTexture);
            g.DrawImage(doorTexture, rect, srcRect, GraphicsUnit.Pixel);
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
}