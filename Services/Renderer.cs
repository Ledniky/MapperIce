using MapperIce.Models;

namespace MapperIce.Services;

public class Renderer
{
    private Bitmap _buffer;
    private readonly object _lock = new();

    public Renderer(int width, int height)
    {
        _buffer = new Bitmap(Math.Max(1, width), Math.Max(1, height));
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

            // Сетка
            foreach (var grid in map.Grids)
            {
                if (!grid.IsVisible) continue;
                bool isActive = map.ActiveGrid != null && map.ActiveGrid.Uid == grid.Uid;
                float opacity = isActive ? 1.0f : 0.3f;
                DrawGrid(g, tileSize, viewOffset, grid.Position, opacity);
            }

            // Заливка комнат
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

            // Линии комнат
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

    private void DrawRoomFill(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, float opacity)
    {
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        var rect = new Rectangle(
            (int)x,
            (int)y,
            (room.Width - 1) * tileSize,
            (room.Height - 1) * tileSize
        );

        int alpha = (int)(room.FillColor.A * opacity);
        var color = Color.FromArgb(alpha, room.FillColor.R, room.FillColor.G, room.FillColor.B);

        using var fill = new SolidBrush(color);
        g.FillRectangle(fill, rect);
    }

    private void DrawRoomLine(Graphics g, Room room, int tileSize, PointF viewOffset, PointF gridOffset, bool isCurrent, float opacity)
    {
        float x = (room.X + 0.5f + gridOffset.X) * tileSize - viewOffset.X;
        float y = (room.Y + 0.5f + gridOffset.Y) * tileSize - viewOffset.Y;

        var rect = new Rectangle(
            (int)x,
            (int)y,
            (room.Width - 1) * tileSize,
            (room.Height - 1) * tileSize
        );

        Color lineColor;
        if (isCurrent)
        {
            lineColor = Color.Red;
        }
        else
        {
            int alpha = (int)(room.LineColor.A * opacity);
            lineColor = Color.FromArgb(alpha, room.LineColor.R, room.LineColor.G, room.LineColor.B);
        }

        using var stroke = new Pen(lineColor, isCurrent ? 3 : 2);
        g.DrawRectangle(stroke, rect);

        // Внутренний объём: (ширина - 2) × (высота - 2)
        if (tileSize > 20 && opacity > 0.3f)
        {
            int innerW = Math.Max(0, room.Width - 2);
            int innerH = Math.Max(0, room.Height - 2);
            
            if (innerW > 0 && innerH > 0)
            {
                using var font = new Font("Arial", Math.Min(10, tileSize / 3));
                int textAlpha = (int)(200 * opacity);
                using var textBrush = new SolidBrush(Color.FromArgb(textAlpha, 50, 50, 50));
                g.DrawString($"{innerW}×{innerH}", font, textBrush, rect.X + 2, rect.Y + 2);
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