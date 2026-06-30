using MapperIce.Models;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();
    private PrototypeIndexer? _indexer;
    private string _rootPath = "";
    public bool HideRoomOverlay { get; set; } = false;

    // Приоритеты стен (как в YAMLGenerator)
    private static readonly Dictionary<string, int> _wallPriority = new()
    {
        { "WallSolid", 0 },
        { "WallReinforced", 1 },
    };

    private int GetPriority(string wall) => _wallPriority.GetValueOrDefault(wall, 2);
    private string BestWall(string a, string b) => GetPriority(a) >= GetPriority(b) ? a : b;
    private Room? GetRoomAt(List<Room> rooms, int x, int y) => rooms.FirstOrDefault(r => x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

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

            // 3. СТЕНЫ с приоритетами (как в YAMLGenerator)
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                var allRooms = grid.Rooms;
                
                // Собираем позиции дверей
                var doorPos = allRooms.SelectMany(r => r.Doors.Select(d => (d.X, d.Y))).ToHashSet();
                var wallMap = new Dictionary<(int x, int y), string>();

                foreach (var r in allRooms)
                {
                    // Верх/низ
                    for (int x = r.X; x < r.X + r.Width; x++)
                    {
                        foreach (var (y, dy) in new[] { (r.Y, -1), (r.Y + r.Height - 1, 1) })
                        {
                            if (doorPos.Contains((x, y))) continue;
                            var n = GetRoomAt(allRooms, x, y + dy);
                            var w = n != null ? BestWall(r.WallProto, n.WallProto) : r.WallProto;
                            var key = (x, y);
                            if (!wallMap.ContainsKey(key) || GetPriority(w) > GetPriority(wallMap[key]))
                                wallMap[key] = w;
                        }
                    }

                    // Лево/право (без углов)
                    for (int y = r.Y + 1; y < r.Y + r.Height - 1; y++)
                    {
                        foreach (var (x, dx) in new[] { (r.X, -1), (r.X + r.Width - 1, 1) })
                        {
                            if (doorPos.Contains((x, y))) continue;
                            var n = GetRoomAt(allRooms, x + dx, y);
                            var w = n != null ? BestWall(r.WallProto, n.WallProto) : r.WallProto;
                            var key = (x, y);
                            if (!wallMap.ContainsKey(key) || GetPriority(w) > GetPriority(wallMap[key]))
                                wallMap[key] = w;
                        }
                    }
                }

                // Рисуем стены с правильным прототипом
                foreach (var kvp in wallMap)
                {
                    var (x, y) = kvp.Key;
                    string wallProto = kvp.Value;
                    DrawWallAt(g, wallProto, x, y, tileSize, viewOffset, grid.Position, opacity);
                }
            }

            // Текущая комната (при создании)
            if (currentRoom != null && map.ActiveGrid != null)
            {
                var doorPos = currentRoom.Doors.Select(d => (d.X, d.Y)).ToHashSet();
                var allRooms = map.ActiveGrid.Rooms;
                
                // Верх/низ
                for (int x = currentRoom.X; x < currentRoom.X + currentRoom.Width; x++)
                {
                    foreach (var (y, dy) in new[] { (currentRoom.Y, -1), (currentRoom.Y + currentRoom.Height - 1, 1) })
                    {
                        if (doorPos.Contains((x, y))) continue;
                        var n = GetRoomAt(allRooms, x, y + dy);
                        var w = n != null ? BestWall(currentRoom.WallProto, n.WallProto) : currentRoom.WallProto;
                        DrawWallAt(g, w, x, y, tileSize, viewOffset, map.ActiveGrid.Position, 1.0f);
                    }
                }

                // Лево/право (без углов)
                for (int y = currentRoom.Y + 1; y < currentRoom.Y + currentRoom.Height - 1; y++)
                {
                    foreach (var (x, dx) in new[] { (currentRoom.X, -1), (currentRoom.X + currentRoom.Width - 1, 1) })
                    {
                        if (doorPos.Contains((x, y))) continue;
                        var n = GetRoomAt(allRooms, x + dx, y);
                        var w = n != null ? BestWall(currentRoom.WallProto, n.WallProto) : currentRoom.WallProto;
                        DrawWallAt(g, w, x, y, tileSize, viewOffset, map.ActiveGrid.Position, 1.0f);
                    }
                }
            }

            // 4. ДВЕРИ
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                foreach (var room in grid.Rooms)
                {
                    DrawDoors(g, room, tileSize, viewOffset, grid.Position);
                }
            }

            // 5. ЗАЛИВКА КОМНАТ
            if (!HideRoomOverlay)
            {
                foreach (var grid in map.Grids)
                {
                    if (!grid.IsVisible) continue;
                    bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                    float opacity = isActive ? 1.0f : 0.3f;
                    foreach (var room in grid.Rooms)
                    {
                        DrawRoomFill(g, room, tileSize, viewOffset, grid.Position, opacity);
                        DrawRoomLine(g, room, tileSize, viewOffset, grid.Position, false, opacity);
                    }
                }

                if (currentRoom != null && map.ActiveGrid != null)
                {
                    DrawRoomFill(g, currentRoom, tileSize, viewOffset, map.ActiveGrid.Position, 1.0f);
                    DrawRoomLine(g, currentRoom, tileSize, viewOffset, map.ActiveGrid.Position, true, 1.0f);
                }
            }

            // 6. ИНФОРМАЦИЯ
            DrawInfo(g, scale, toolName, map);

            return _buffer;
        }
    }

    private void DrawWallAt(Graphics g, string wallProto, int x, int y, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        string? wallPath = null;
        if (_indexer != null)
        {
            wallPath = _indexer.GetFullTexturePath(wallProto);
        }

        float wx = (x + gridOffset.X) * tileSize - viewOffset.X;
        float wy = (y + gridOffset.Y) * tileSize - viewOffset.Y;
        var rect = new Rectangle((int)wx, (int)wy, tileSize, tileSize);

        if (!string.IsNullOrEmpty(wallPath) && File.Exists(wallPath))
        {
            try
            {
                using var img = Image.FromFile(wallPath);
                var srcRect = GetSourceRect(img);
                g.DrawImage(img, rect, srcRect, GraphicsUnit.Pixel);
                return;
            }
            catch { }
        }

        // Fallback - рисуем линию
        using var pen = new Pen(Color.Gray, 1);
        g.DrawRectangle(pen, rect);
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

    private void DrawFloorTiles(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        int totalW = room.Width;
        int totalH = room.Height;
        
        if (totalW <= 0 || totalH <= 0) return;

        Image? floorTexture = null;
        Rectangle sourceRect = new Rectangle(0, 0, 32, 32);
        
        if (_indexer != null)
        {
            var floorPath = _indexer.GetFullTexturePath(room.FloorProto);
            if (floorPath != null && File.Exists(floorPath))
            {
                try 
                { 
                    floorTexture = Image.FromFile(floorPath);
                    sourceRect = GetSourceRect(floorTexture);
                }
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
                    g.DrawImage(floorTexture, rect, sourceRect, GraphicsUnit.Pixel);
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
                try 
                { 
                    doorTexture = Image.FromFile(doorPath);
                    var srcRect = GetSourceRect(doorTexture);
                    g.DrawImage(doorTexture, new Rectangle((int)x, (int)y, tileSize, tileSize), srcRect, GraphicsUnit.Pixel);
                    continue;
                }
                catch { }
            }
            
            // Fallback
            using var brush = new SolidBrush(Color.FromArgb(200, 0, 200, 255));
            g.FillRectangle(brush, (int)x, (int)y, tileSize, tileSize);
            using var pen = new Pen(Color.DarkBlue, 2);
            g.DrawRectangle(pen, (int)x, (int)y, tileSize, tileSize);
            using var font = new Font("Segoe UI", 14);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString("🚪", font, textBrush, (int)x + 4, (int)y + 2);
        }
    }

    private void DrawInfo(Graphics g, float scale, string toolName, MapData map)
    {
        using var font = new Font("Arial", 12, FontStyle.Bold);
        using var brush = new SolidBrush(Color.DarkGray);
        var name = map.ActiveGrid?.Name ?? "Нет";
        string mode = HideRoomOverlay ? " [ОВЕРЛЕЙ СКРЫТ]" : "";
        g.DrawString($"Инструмент: {toolName}{mode}  Масштаб: {scale:P0}  Активный грид: {name}  Всего: {map.Grids.Count}",
            font, brush, 10, 10);
    }
}