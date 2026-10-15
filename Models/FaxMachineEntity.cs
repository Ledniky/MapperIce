namespace MapperIce.Models;

/// <summary>
/// Факс как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте, прототип факса (FaxMachineBase / FaxMachineCaptain)
/// и АДРЕС приёмника — строку с именем из свойств комнаты (Room.FaxName), например
/// "Medical" или "HoP Office". Адрес запекается в привязку при установке: он попадает
/// в компонент FaxMachine при экспорте (name), поэтому перенос строки в свойствах
/// комнаты уже поставленные факсы не меняет.
/// Привязка есть только у комнат, которым факс положен по списку адресов (у остальных
/// FaxName пустой и инструмент факс не ставит).
/// При экспорте выводится как авто-устанавливаемый объект — как
/// HolopadEntity/LockerEntity/SuitStorageEntity/BedEntity/CameraEntity/DresserEntity/IntercomEntity.
/// </summary>
public class FaxMachineEntity : MapEntity
{
    /// <summary>Прототип факса (например "FaxMachineBase").</summary>
    public string FaxProto { get; set; } = "";

    /// <summary>Адрес факса — значение поля name компонента FaxMachine.</summary>
    public string FaxName { get; set; } = "";
}
