namespace MapperIce.Models;

/// <summary>
/// Комод как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототип комода (id из Catalog/Fills/Lockers/dressers.yml,
/// например "DresserCaptainFilled"), который берётся из свойств комнаты
/// (Room.DresserProto) при установке. Комоды привязаны только к комнатам глав
/// отделов, поэтому у остальных типов комнат прототип пустой и привязка не ставится.
/// При экспорте выводится как авто-устанавливаемый объект — как
/// HolopadEntity/LockerEntity/SuitStorageEntity/BedEntity/CameraEntity.
/// </summary>
public class DresserEntity : MapEntity
{
    /// <summary>Прототип комода (например "DresserCaptainFilled").</summary>
    public string DresserProto { get; set; } = "";
}
