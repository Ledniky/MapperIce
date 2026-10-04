namespace MapperIce.Models;

/// <summary>
/// Шкафчик как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототип шкафчика (id из Catalog/Fills/Lockers),
/// который берётся из свойств комнаты (Room.LockerProto) при установке.
/// При экспорте выводится как авто-устанавливаемый объект — аналогично HolopadEntity.
/// </summary>
public class LockerEntity : MapEntity
{
    /// <summary>Прототип шкафчика (например "LockerQuarterMasterFilled").</summary>
    public string LockerProto { get; set; } = "";
}