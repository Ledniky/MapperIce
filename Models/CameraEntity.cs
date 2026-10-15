namespace MapperIce.Models;

/// <summary>
/// Камера наблюдения как «привязка», а не классическая ECS-сущность.
/// Хранит координаты на карте и прототип камеры (id из surveillance_camera.yml,
/// например "SurveillanceCameraSecurity"), который берётся из свойств комнаты
/// (Room.CameraProto) при установке. Прототип камеры соответствует отделу
/// комнаты, поэтому в свойствах типа он подставляется автоматически по Category.
/// При экспорте выводится как авто-устанавливаемый объект — как
/// HolopadEntity/LockerEntity/SuitStorageEntity/BedEntity.
/// </summary>
public class CameraEntity : MapEntity
{
    /// <summary>Прототип камеры (например "SurveillanceCameraSecurity").</summary>
    public string CameraProto { get; set; } = "";
}
