// Models/MoveSettings.cs
namespace MapperIce.Models;

public class MoveSettings
{
    public float Step { get; set; } = 1.0f;

    // Категории фильтра выделяемых объектов — единый список для HitTest и рамки
    public bool IncludeRooms { get; set; } = true;          // комнаты
    public bool IncludeDoors { get; set; } = true;          // двери, проёмы (Passages) и окна (Windows)
    public bool IncludePipes { get; set; } = true;          // трубы (газовые и утилизационные)
    public bool IncludeWires { get; set; } = true;          // электропроводка (HV/MV/LV)
    public bool IncludeAlarms { get; set; } = true;         // сигнализации (AirAlarm, FireAlarm)
    public bool IncludePrototypes { get; set; } = true;     // прототипы — сущности из панели репозитория (точный тип MapEntity)
    public bool IncludeDecals { get; set; } = true;         // декали из репозитория
    public bool IncludeTiles { get; set; } = true;          // тайлы из репозитория (PlacedTile)
    public bool IncludeOther { get; set; } = true;          // всё остальное (напр. FirelockEntity), что не попало в категории выше

    public static MoveSettings Default = new MoveSettings();
}