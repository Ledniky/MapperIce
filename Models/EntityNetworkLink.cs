// Models/EntityNetworkLink.cs
namespace MapperIce.Models;

/// <summary>
/// Связь «кнопка/рычаг → устройство» в беспроводной сети (DeviceNetwork).
/// Хранится на источнике (у MapEntity, у которого прототип имеет компонент
/// DeviceLinkSource). При экспорте в YAML превращается в
/// DeviceLinkSource.linkedPorts: { <UID цели>: [ [портИсточника, портЦели], ... ] }.
/// Цель идентифицируется своей позицией (тайлы), т.к. у сущностей в редакторе
/// нет стабильных UID.
/// </summary>
public class EntityNetworkLink
{
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public string SourcePort { get; set; } = "";
    public string TargetPort { get; set; } = "";
}