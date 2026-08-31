// Models/TileData.cs
namespace MapperIce.Models;

public enum TileContent
{
    Empty,
    Floor,
    Wall,
    Door,
    Pipe
}

public class TileData
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileContent Content { get; set; } = TileContent.Empty;
    public string? ProtoId { get; set; }
    public string? RoomType { get; set; }
    public int RoomUid { get; set; } = -1;
    public string? PipeType { get; set; }
    
    // Для хранения информации о поле под дверью
    public bool HasFloorUnder { get; set; } = false;
    public string? FloorProtoUnder { get; set; } = null;
    
    /// <summary>
    /// Разрешённый offset drawdepth (рассчитывается при загрузке карты).
    /// Чем меньше — тем ниже объект на экране.
    /// </summary>
    public int DrawDepthOffset { get; set; } = 0;
    
    public bool IsWall => Content == TileContent.Wall;
    public bool IsFloor => Content == TileContent.Floor;
    public bool IsDoor => Content == TileContent.Door;
    public bool IsPipe => Content == TileContent.Pipe;
    public bool IsEmpty => Content == TileContent.Empty;

    public TileData Clone()
    {
        return new TileData
        {
            X = X,
            Y = Y,
            Content = Content,
            ProtoId = ProtoId,
            RoomType = RoomType,
            RoomUid = RoomUid,
            PipeType = PipeType,
            HasFloorUnder = HasFloorUnder,
            FloorProtoUnder = FloorProtoUnder,
            DrawDepthOffset = DrawDepthOffset
        };
    }
}