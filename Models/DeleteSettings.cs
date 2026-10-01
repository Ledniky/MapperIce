// Models/DeleteSettings.cs
namespace MapperIce.Models;

public class DeleteSettings
{
    public bool DeleteAll { get; set; } = true;

    // Категории фильтра удаления — единый список для точечного удаления и удаления областью
    public bool DeleteRooms { get; set; } = true;          // комнаты
    public bool DeleteDoors { get; set; } = true;          // двери, проёмы (Passages) и окна (Windows)
    public bool DeletePipes { get; set; } = true;          // трубы (газовые и утилизационные)
    public bool DeleteWires { get; set; } = true;          // электропроводка (HV/MV/LV)
    public bool DeleteAlarms { get; set; } = true;         // сигнализации (AirAlarm, FireAlarm)
    public bool DeletePrototypes { get; set; } = false;    // прототипы — сущности из панели репозитория (точный тип MapEntity)
    public bool DeleteDecals { get; set; } = false;        // декали из репозитория
    public bool DeleteTiles { get; set; } = false;         // тайлы из репозитория (PlacedTile)
    public bool DeleteOther { get; set; } = false;         // всё остальное, что не попало в категории выше

    public static DeleteSettings Default = new DeleteSettings();
}