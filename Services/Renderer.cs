using MapperIce.Models;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private PrototypeIndexer? _indexer;
    private string _rootPath = "";

    public Renderer(int width, int height, PrototypeIndexer? indexer = null)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        _indexer = indexer;
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

            // 2. ТАЙЛЫ ПОЛА
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                foreach (var room in grid.Rooms)
                    DrawFloorTiles(g, room, tileSize, viewOffset, grid.Position, opacity);
            }

            // 3. СТЕНЫ (под заливкой)
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                var allRooms = grid.Rooms;
                foreach (var room in allRooms)
                {
                    DrawRoomWalls(g, room, tileSize, viewOffset, grid.Position, opacity, allRooms);
                }
            }

            if (currentRoom != null && map.ActiveGrid != null)
            {
                DrawRoomWalls(g, currentRoom, tileSize, viewOffset, map.ActiveGrid.Position, 1.0f, new List<Room>());
            }

            // 4. ДВЕРИ (под заливкой)
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                foreach (var room in grid.Rooms)
                {
                    DrawDoors(g, room, tileSize, viewOffset, grid.Position);
                }
            }

            // 5. ЗАЛИВКА КОМНАТ (поверх стен и дверей)
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

            // 6. ЛИНИИ (поверх всего)
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

            // 7. ИНФОРМАЦИЯ
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
        int totalW = room.Width;
        int totalH = room.Height;
        
        if (totalW <= 0 || totalH <= 0) return;

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
        
        for (int x = 0; x < totalW; x++)
        {
            for (int y = 0; y < totalH; y++)
            {
                float tileX = (room.X + x + gridOffset.X) * tileSize - viewOffset.X;
                float tileY = (room.Y + y + gridOffset.Y) * tileSize - viewOffset.Y;
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

    private void DrawRoomWalls(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity, List<Room> allRooms)
    {
        // Получаем путь к текстуре стены
        string? wallPath = null;
        if (_indexer != null)
        {
            wallPath = _indexer.GetFullTexturePath(room.WallProto);
        }

        if (string.IsNullOrEmpty(wallPath) || !File.Exists(wallPath))
        {
            DrawRoomLines(g, room, tileSize, viewOffset, gridOffset, opacity);
            return;
        }

        Image? wallTexture = null;
        try { wallTexture = Image.FromFile(wallPath); }
        catch { return; }

        // Собираем позиции дверей в этой комнате
        var doorPositions = room.Doors.Select(d => (d.X, d.Y)).ToHashSet();

        // Верхняя стена
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            // Пропускаем, если здесь дверь
            if (doorPositions.Contains((x, room.Y))) continue;

            float wx = (x + gridOffset.X) * tileSize - viewOffset.X;
            float wy = (room.Y + gridOffset.Y) * tileSize - viewOffset.Y;
            g.DrawImage(wallTexture, new Rectangle((int)wx, (int)wy, tileSize, tileSize));
        }

        // Нижняя стена
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            if (doorPositions.Contains((x, room.Y + room.Height - 1))) continue;

            float wx = (x + gridOffset.X) * tileSize - viewOffset.X;
            float wy = (room.Y + room.Height - 1 + gridOffset.Y) * tileSize - viewOffset.Y;
            g.DrawImage(wallTexture, new Rectangle((int)wx, (int)wy, tileSize, tileSize));
        }

        // Левая стена (без углов)
        for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
        {
            if (doorPositions.Contains((room.X, y))) continue;

            float wx = (room.X + gridOffset.X) * tileSize - viewOffset.X;
            float wy = (y + gridOffset.Y) * tileSize - viewOffset.Y;
            g.DrawImage(wallTexture, new Rectangle((int)wx, (int)wy, tileSize, tileSize));
        }

        // Правая стена (без углов)
        for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
        {
            if (doorPositions.Contains((room.X + room.Width - 1, y))) continue;

            float wx = (room.X + room.Width - 1 + gridOffset.X) * tileSize - viewOffset.X;
            float wy = (y + gridOffset.Y) * tileSize - viewOffset.Y;
            g.DrawImage(wallTexture, new Rectangle((int)wx, (int)wy, tileSize, tileSize));
        }

        DrawRoomText(g, room, tileSize, opacity, GetRoomRect(room, tileSize, viewOffset, gridOffset));
    }

    private void DrawRoomLines(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int innerW = Math.Max(0, room.Width - 1);
        int innerH = Math.Max(0, room.Height - 1);
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)x, (int)y, innerW * tileSize, innerH * tileSize);

        Color color = Color.FromArgb((int)(room.LineColor.A * opacity), room.LineColor.R, room.LineColor.G, room.LineColor.B);
        using var pen = new Pen(color, 2);
        g.DrawRectangle(pen, rect);
        DrawRoomText(g, room, tileSize, opacity, rect);
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

        DrawRoomText(g, room, tileSize, opacity, rect);
    }

    private Rectangle GetRoomRect(Room room, int tileSize, PointF viewOffset, PointF gridOffset)
    {
        int innerW = Math.Max(0, room.Width - 1);
        int innerH = Math.Max(0, room.Height - 1);
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;
        return new Rectangle((int)x, (int)y, innerW * tileSize, innerH * tileSize);
    }

    private void DrawRoomText(Graphics g, Room room, int tileSize, float opacity, Rectangle rect)
    {
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

    // ===== ДВЕРИ =====

private void DrawDoors(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset)
{
    if (_indexer == null) return;
    
    foreach (var door in room.Doors)
    {
        float x = (door.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X - tileSize / 2f;
        float y = (door.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y - tileSize / 2f;
        
        Image? doorTexture = null;
        var doorPath = _indexer.GetFullTexturePath(door.Proto);
        if (doorPath != null && File.Exists(doorPath))
        {
            try { doorTexture = Image.FromFile(doorPath); }
            catch { }
        }
        
        if (doorTexture != null)
        {
            g.DrawImage(doorTexture, new Rectangle((int)x, (int)y, tileSize, tileSize));
        }
        else
        {
            // Заглушка
            using var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255));
            g.FillRectangle(brush, (int)x, (int)y, tileSize, tileSize);
            using var pen = new Pen(Color.DarkBlue, 2);
            g.DrawRectangle(pen, (int)x, (int)y, tileSize, tileSize);
            using var font = new Font("Segoe UI", 14);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString("🚪", font, textBrush, (int)x + 4, (int)y + 2);
        }
    }
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