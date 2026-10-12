namespace MapperIce.Models;

/// <summary>
/// Кровать как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототипы кровати (id из beds.yml) и привязанного
/// к ней одеяла (id из bedsheets.yml), которые берутся из свойств комнаты
/// (Room.BedProto / Room.BedsheetProto) при установке.
/// При экспорте выводится как авто-устанавливаемый объект — кровать и одеяло
/// двумя отдельными "- proto:" блоками на одной позиции, аналогично
/// HolopadEntity/LockerEntity/SuitStorageEntity.
/// </summary>
public class BedEntity : MapEntity
{
    /// <summary>Прототип кровати (например "Bed").</summary>
    public string BedProto { get; set; } = "";

    /// <summary>Прототип одеяла (например "BedsheetHOS"). Пусто — одеяло не ставится.</summary>
    public string BedsheetProto { get; set; } = "";
}