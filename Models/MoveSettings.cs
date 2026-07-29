// Models/MoveSettings.cs
namespace MapperIce.Models;

public class MoveSettings
{
    public float Step { get; set; } = 1.0f;

    public bool IncludeRooms { get; set; } = true;
    public bool IncludeTiles { get; set; } = true;
    public bool IncludePipes { get; set; } = true;
    public bool IncludeAlarms { get; set; } = true;
    public bool IncludeFirelocks { get; set; } = false; // двигаются вместе с комнатой автоматически
    public bool IncludeEntities { get; set; } = true;   // "Сущности" из репозитория (точный тип MapEntity)
    public bool IncludeOther { get; set; } = true;      // catch-all на будущее

    public static MoveSettings Default = new MoveSettings();
}