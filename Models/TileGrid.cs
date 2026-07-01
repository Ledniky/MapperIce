// Models/TileGrid.cs
using System.Collections.Concurrent;

namespace MapperIce.Models;

public class TileGrid
{
    private readonly ConcurrentDictionary<(int x, int y), TileData> _tiles = new();
    
    public int GridUid { get; set; }
    public string GridName { get; set; } = "Grid";
    public PointF Position { get; set; } = new PointF(0, 0);
    public Color Color { get; set; } = Color.Blue;
    public bool IsVisible { get; set; } = true;
    
    public event EventHandler? TilesChanged;

    public TileGrid() { }

    public TileGrid(int uid, string name) : this()
    {
        GridUid = uid;
        GridName = name;
    }

    public void SetTile(int x, int y, TileContent content, string? protoId = null, 
                        string? roomType = null, int roomUid = -1, string? pipeType = null)
    {
        if (content == TileContent.Empty)
        {
            _tiles.TryRemove((x, y), out _);
        }
        else
        {
            var tile = new TileData
            {
                X = x,
                Y = y,
                Content = content,
                ProtoId = protoId,
                RoomType = roomType,
                RoomUid = roomUid,
                PipeType = pipeType
            };
            _tiles[(x, y)] = tile;
        }
        OnTilesChanged();
    }

    public TileData? GetTile(int x, int y)
    {
        _tiles.TryGetValue((x, y), out var tile);
        return tile;
    }

    public IEnumerable<TileData> GetTilesByContent(TileContent content)
    {
        return _tiles.Values.Where(t => t.Content == content);
    }

    public IEnumerable<TileData> GetTilesByRoom(int roomUid)
    {
        return _tiles.Values.Where(t => t.RoomUid == roomUid);
    }

    public IEnumerable<TileData> GetAllTiles()
    {
        return _tiles.Values;
    }

    public void Clear()
    {
        _tiles.Clear();
        OnTilesChanged();
    }

    public void ClearRoom(int roomUid)
    {
        var keysToRemove = _tiles
            .Where(kvp => kvp.Value.RoomUid == roomUid)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _tiles.TryRemove(key, out _);
        }
        OnTilesChanged();
    }

    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        if (_tiles.IsEmpty)
            return (0, 0, 0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var key in _tiles.Keys)
        {
            minX = Math.Min(minX, key.x);
            minY = Math.Min(minY, key.y);
            maxX = Math.Max(maxX, key.x);
            maxY = Math.Max(maxY, key.y);
        }

        return (minX, minY, maxX, maxY);
    }

    public bool HasTile(int x, int y)
    {
        return _tiles.ContainsKey((x, y));
    }

    public bool IsWall(int x, int y)
    {
        return _tiles.TryGetValue((x, y), out var tile) && tile.Content == TileContent.Wall;
    }

    public bool IsFloor(int x, int y)
    {
        return _tiles.TryGetValue((x, y), out var tile) && tile.Content == TileContent.Floor;
    }

    public bool IsDoor(int x, int y)
    {
        return _tiles.TryGetValue((x, y), out var tile) && tile.Content == TileContent.Door;
    }

    public bool IsPipe(int x, int y)
    {
        return _tiles.TryGetValue((x, y), out var tile) && tile.Content == TileContent.Pipe;
    }

    public Dictionary<(int x, int y), TileData> Snapshot()
    {
        return _tiles.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Clone()
        );
    }

    public void Restore(Dictionary<(int x, int y), TileData> snapshot)
    {
        _tiles.Clear();
        foreach (var kvp in snapshot)
        {
            _tiles[kvp.Key] = kvp.Value;
        }
        OnTilesChanged();
    }

    private void OnTilesChanged()
    {
        TilesChanged?.Invoke(this, EventArgs.Empty);
    }
}