namespace MapperIce.Models;

/// <summary>
/// Голопад как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототип голопада (id из holopad.yml),
/// который берётся из свойств комнаты (Room.HolopadProto) при установке.
/// При экспорте выводится как авто-устанавливаемый объект.
/// </summary>
public class HolopadEntity : MapEntity
{
    /// <summary>Прототип голопада (например "HolopadEngineeringMain").</summary>
    public string HolopadProto { get; set; } = "";
}