// Services/Renderer.cs
using MapperIce.Models;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private readonly PrototypeIndexer? _indexer;
    private readonly TileBuilder _tileBuilder;
    private readonly string _rootPath = "";
    public bool HideRoomOverlay { get; set; } = false;
    private MapData? _currentMap;

    public Renderer(int width, int height, PrototypeIndexer? indexer, TileBuilder tileBuilder)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
        _tileBuilder = tileBuilder;
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

            // Для каждого грида строим TileGrid и рендерим
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;

                // Строим TileGrid для этого грида
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

                // 5. Оверлей комнат (если включен)
                if (!HideRoomOverlay)
                {
                    foreach (var room in grid.Rooms)
                    {
                        DrawRoomFill(g, room, tileSize, viewOffset, grid.Position, opacity);
                        DrawRoomLine(g, room, tileSize, viewOffset, grid.Position, false, opacity);
                    }
                }

                // Текущая комната (при создании)
                if (currentRoom != null && isActive)
                {
                    DrawRoomFill(g, currentRoom, tileSize, viewOffset, grid.Position, 1.0f);
                    DrawRoomLine(g, currentRoom, tileSize, viewOffset, grid.Position, true, 1.0f);
                }
            }

            // Информация
            DrawInfo(g, scale, toolName, map);

            return _buffer;
        }
    }

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
        g.DrawString($"Инструмент: {toolName}{mode}  Масштаб: {scale:P0}  Активный грид: {name}  Всего гридов: {map.Grids.Count}",
            font, brush, 10, 10);
    }
}