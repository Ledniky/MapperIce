namespace MapperIce.Models;

/// <summary>
/// Хранилище скафандра (костюм-бокс) как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототип (id из suit_storage.yml,
/// например "SuitStorageEVA"), который берётся из свойств комнаты
/// (Room.SuitStorageProto) при установке.
/// При экспорте выводится как авто-устанавливаемый объект — как LockerEntity/HolopadEntity.
/// </summary>
public class SuitStorageEntity : MapEntity
{
    /// <summary>Прототип хранилища скафандра (например "SuitStorageEVA").</summary>
    public string SuitStorageProto { get; set; } = "";
}