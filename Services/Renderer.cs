using MapperIce.Models;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private PrototypeIndexer? _indexer;

    public Renderer(int width, int height, PrototypeIndexer? indexer = null)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
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
        lock (_lock)
        {
            if (_buffer.Width == 0 || _buffer.Height == 0) return _buffer;

            using var g = Graphics.FromImage(_buffer);
            g.Clear(Color.White);

            int tileSize = (int)(Constants.TILE_SIZE * scale);

            // 1. СЕТКА
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                DrawGrid(g, tileSize, viewOffset, grid.Position, opacity);
            }

            // 1.5. ТАЙЛЫ ПОЛА (под комнатами)
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                foreach (var room in grid.Rooms)
                    DrawFloorTiles(g, room, tileSize, viewOffset, grid.Position, opacity);
            }

            // 2. ЗАЛИВКА КОМНАТ
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                foreach (var room in grid.Rooms)
                    DrawRoomFill(g, room, tileSize, viewOffset, grid.Position, opacity);
            }

            if (currentRoom != null && map.ActiveGrid != null)
                DrawRoomFill(g, currentRoom, tileSize, viewOffset, map.ActiveGrid.Position, 1.0f);

            // 3. ЛИНИИ КОМНАТ
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                foreach (var room in grid.Rooms)
                    DrawRoomLine(g, room, tileSize, viewOffset, grid.Position, false, opacity);
            }

            if (currentRoom != null && map.ActiveGrid != null)
                DrawRoomLine(g, currentRoom, tileSize, viewOffset, map.ActiveGrid.Position, true, 1.0f);

            // 4. ИНФОРМАЦИЯ
            DrawInfo(g, scale, toolName, map);

            return _buffer;
        }
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

    private void DrawFloorTiles(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int innerW = Math.Max(0, room.Width - 2);
        int innerH = Math.Max(0, room.Height - 2);
        
        if (innerW <= 0 || innerH <= 0) return;

        Image? floorTexture = null;
        if (_indexer != null)
        {
            var floorPath = _indexer.GetFullTexturePath(room.FloorProto);
            if (floorPath != null && File.Exists(floorPath))
            {
                try { floorTexture = Image.FromFile(floorPath); }
                catch { }
            }
        }

        using var fillBrush = new SolidBrush(Color.FromArgb((int)(150 * opacity), 200, 200, 200));
        
        for (int x = 0; x < innerW; x++)
        {
            for (int y = 0; y < innerH; y++)
            {
                float tileX = (room.X + 1 + x + gridOffset.X) * tileSize - viewOffset.X;
                float tileY = (room.Y + 1 + y + gridOffset.Y) * tileSize - viewOffset.Y;
                var rect = new Rectangle((int)tileX, (int)tileY, tileSize, tileSize);
                
                if (floorTexture != null)
                    g.DrawImage(floorTexture, rect);
                else
                    g.FillRectangle(fillBrush, rect);
            }
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

        if (tileSize > 20 && opacity > 0.3f)
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
        g.DrawString($"Инструмент: {toolName}  Масштаб: {scale:P0}  Активный грид: {name}  Всего: {map.Grids.Count}",
            font, brush, 10, 10);
    }
}