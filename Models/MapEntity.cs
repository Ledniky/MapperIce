namespace MapperIce.Models;

/// <summary>
/// Сущность на карте (предмет, стена, дверь и т.д.)
/// </summary>
public class MapEntity
{
    public string Proto { get; set; } = "";      // "WallSolid", "AirlockArmoryLocked"
    public float X { get; set; }                 // Координата X в тайлах (с дробной частью)
    public float Y { get; set; }                 // Координата Y в тайлах (с дробной частью)
    public int? ParentGridUid { get; set; }      // К какому гриду привязан (если null - к карте)
    public float Rotation { get; set; } = 0;     // Угол поворота в радианах
    /// <summary>
    /// Слой трубы для машин с компонентом AtmosPipeLayers (термомашины и т.п.):
    /// 0 = Primary, 1 = Secondary, 2 = Tertiary. У большинства сущностей 0
    /// (слой не выбирается). Переключается Shift+колесом и экспортируется как
    /// AtmosPipeLayers.pipeLayer. У труб GasPipe*Alt1/Alt2 слой задаётся самим
    /// прототипом, и это поле для них не используется.
    /// </summary>
    public int PipeLayer { get; set; } = 0;
    /// <summary>
    /// Разрешённый offset drawdepth (рассчитывается при загрузке карты).
    /// Чем меньше — тем ниже сущность на экране.
    /// </summary>
    public int DrawDepthOffset { get; set; } = 0;

    /// <summary>
    /// Связи беспроводной сети (для «кнопок/рычагов» с компонентом DeviceLinkSource):
    /// какие порты источника куда ведут. Пустой список — связей нет.
    /// </summary>
    public List<EntityNetworkLink> NetworkLinks { get; set; } = new();
}