namespace MapperIce.Models;

/// <summary>
/// Интерком как «привязка». Особенность интеркома: он висит не в клетке комнаты,
/// как камера, а прямо В тайле-стене, поэтому его координаты — центр тайла-стены,
/// найденного ближайшим к указанной пользователем клетке комнаты, а Rotation —
/// направление вовнутрь комнаты (спрайт встаёт на грани стены, обращённой в комнату,
/// см. WallMountOrientation.ResolveIntercomRotation).
/// Прототип (id из intercom.yml, например "IntercomSecurity") берётся из свойств
/// комнаты (Room.IntercomProto) и подбирается по отделу комнаты.
/// При экспорте выводится как авто-устанавливаемый объект — как остальные привязки.
/// </summary>
public class IntercomEntity : MapEntity
{
    /// <summary>Прототип интеркома (например "IntercomSecurity").</summary>
    public string IntercomProto { get; set; } = "";
}
