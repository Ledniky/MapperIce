namespace MapperIce.Models;

public class Prototype
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";   // "tile" или "entity" — из YAML "- type: tile"
    public string? SpritePath { get; set; }
    public string? RsiPath { get; set; }
    public string? State { get; set; }
    /// <summary>
    /// YAML-поле "suffix:" (например "Open" у открытых штор). Иногда движок выбирает
    /// спрайт по суффиксу, а не по state: если suffix начинается с "Open", и в папке
    /// RSI есть файл "open.png", то рисуем его (см. GetFullTexturePath).
    /// </summary>
    public string? Suffix { get; set; }
    public string FilePath { get; set; } = "";
    public string? Parent { get; set; }
    public List<string> Parents { get; set; } = new();
    public List<string> Components { get; set; } = new();
    public bool IsStructure { get; set; } = false;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
    public bool HasOffset { get; set; } = false;
    /// <summary>
    /// Имя drawdepth из компонента Sprite (например "FloorTiles", "Walls").
    /// </summary>
    public string? DrawDepth { get; set; }

    /// <summary>
    /// Максимальная отдаваемая мощность (Вт) из компонента PowerNetworkBattery
    /// ("maxSupply:") — задаётся на подстанциях (отдают в сеть СВ) и на ЛКП/APC
    /// (отдают в сеть НВ). Null, если у прототипа и его родителей такого
    /// компонента нет.
    /// </summary>
    public float? MaxSupply { get; set; }

    /// <summary>
    /// true, если у прототипа в YAML стоит "abstract: true" — это шаблон-родитель    /// (например BaseStructureWall), а не размещаемая на карте сущность. Такие
    /// прототипы должны быть скрыты из списка репозитория/размещения.
    /// </summary>
    public bool IsAbstract { get; set; } = false;

    /// <summary>
    /// Русское название сущности, найденное в локализации (.ftl, ключ "ent-{Id}").    /// Резолвится с учётом Fluent-ссылок вида "{ ent-ДругойId }" (см. ApplyLocalizedNames
    /// в PrototypeIndexer). Null, если у прототипа нет собственной/унаследованной записи
    /// в локализации репозитория — в этом случае панель репозитория показывает id как раньше.
    /// </summary>
    public string? LocalizedName { get; set; }

    /// <summary>
    /// Суффикс прототипа для показа в списке — как "Name [Suffix]" в редакторе игры
    /// (EntityPrototype.EditorSuffix). Берётся из атрибута ".suffix" локализованной
    /// записи "ent-{Id}" (например ent-GlassBoxLaserOpen → "Антикварный лазер,
    /// Открытый"), а если перевода нет — из сырого YAML-поля "suffix:"
    /// (в том числе унаследованного от родителя). Null — показывать нечего.
    /// </summary>
    public string? LocalizedSuffix { get; set; }

    /// <summary>
    /// Ключ локализации из поля "name:" в YAML (например "tiles-steel-floor" у тайлов).
    /// Сущности обычно не имеют такого поля и локализуются через "ent-{Id}" — тогда
    /// LocalizationKey == null. Используется ApplyLocalizedNames для простановки
    /// LocalizedName из .ftl по этому ключу (префикс "tiles-*" у тайлов).
    /// </summary>
    public string? LocalizationKey { get; set; }

    /// <summary>

    /// <summary>
    /// Слои компонента Sprite (YAML "layers:"). Пустой список — прототип без
    /// многослойности, рисуется как раньше через SpritePath/RsiPath/State.
    /// Если список не пуст, Renderer рисует все видимые слои по порядку друг
    /// поверх друга (как это делает сам движок Robust Toolbox).
    /// </summary>
    public List<SpriteLayer> Layers { get; set; } = new();

    /// <summary>
    /// Состояния закрытого хранения из компонента EntityStorageVisuals
    /// ("stateBaseClosed", "stateDoorOpen", "stateDoorClosed") — это имена state
    /// в RSI предков (например у шкафчиков/локеров из closet.rsi), которые
    /// заставляют базовые слои спрайта (Base/Door) использовать именно эти state,
    /// а не дефолтные "generic"/"generic_door". Хранит сырые значения из YAML.
    /// </summary>
    public string? StorageStateBaseClosed { get; set; }
    public string? StorageStateDoorOpen { get; set; }
    public string? StorageStateDoorClosed { get; set; }

    /// <summary>
    /// Настройки сглаживания иконок из компонента IconSmooth (Corners-режим игры):
    /// объект «склеивается» с такими же соседями, а спрайт каждой клетки выбирается
    /// из стейтов "{base}0"…"{base}7" в зависимости от занятости 3 соседних тайлов
    /// вокруг каждого из 4 углов. Null — прототип не сглаживается (и у него, и у всех
    /// родителей нет компонента IconSmooth).
    /// Значения хранятся СЫРЫМИ из YAML и резолвятся по цепочке parent: через
    /// PrototypeIndexer, аналогично Sprite/State/DrawDepth — см. GetIconSmoothInfo.
    /// </summary>
    public IconSmoothSettings? IconSmooth { get; set; }

    /// <summary>
    /// Цвет однослойного компонента Sprite ("color:" прямо в Sprite, не в layers).
    /// В SS14 цвет может быть как hex ("#RRGGBB"), так и именем ("Green", "Yellow").
    /// Для кабелей это цвет тонирования единственного слоя (например lvcable_*
    /// у CableApcExtension красится в Green). Null — тонирования нет.
    /// </summary>
    public string? SpriteColor { get; set; }

    /// <summary>
    /// YAML-поле "snapCardinals:" компонента Sprite. Если true — спрайт не вращается
    /// при повороте сущности (как гироскоп: симметричный, с единственным кадром
    /// и snapCardinals: true у Sprite). В редакторе такой прототип всегда рисуется
    /// южным кадром, игнорируя rotation.
    /// </summary>
    public bool SnapCardinals { get; set; } = false;

    /// <summary>
    /// Компонент CableVisualizer (кабели HV/MV/LV/Det): в игре он сам выбирает
    /// RSI-стейт по 4-битной маске соседей-кабелей той же NodeGroupID:
    /// "{StatePrefix}{mask}" на слое 0 и "{ExtraLayerPrefix}{mask}" на слое 1.
    /// Null — у прототипа и всех родителей компонента нет (кабель рисуется
    /// фолбэком — линиями/точками).
    /// </summary>
    public CableVisualizerSettings? CableVisualizer { get; set; }

    /// <summary>
    /// NodeGroupID кабельного узла из компонента NodeContainer (HVPower, MVPower,
    /// Apc, ExCable). Определяет, какие кабели соединяются друг с другом: в игре
    /// CableVisSystem считает соседями только CableNode из той же группы.
    /// Null — у прототипа/родителей нет NodeContainer с nodeGroupID.
    /// </summary>
    public string? CableNodeGroup { get; set; }

    /// <summary>
    /// Компонент AtmosPipeLayers: карта RSI-путей слоя трубы для трёх слоёв атмос-сетей
    /// (Primary/Secondary/Tertiary). Ключ — map-ключ слоя ("enum.PipeVisualLayers.Pipe"),
    /// значение — массив из трёх путей RSI [Primary, Secondary, Tertiary]. В отличие от
    /// труб GasPipe*Alt1/Alt2, у машин (термомашины, насосы и т.п.) три слоя трубы — это
    /// не отдельные прототипы, а альтернативные RSI, переключаемые компонентом.
    /// Резолвится по цепочке parent: (см. GetPipeLayerRsiPaths).
    /// </summary>
    public Dictionary<string, string[]> PipeLayerRsiPaths { get; set; } = new();
}

/// <summary>
/// Настройки компонента CableVisualizer из YAML. Маска — WireVisDirFlags игры:
/// North=1, South=2, East=4, West=8 (см. CableVisSystem). Стейт слоя 0 —
/// StatePrefix + mask, слоя 1 — ExtraLayerPrefix + mask (если задан).
/// </summary>
public class CableVisualizerSettings
{
    /// <summary>YAML-поле "statePrefix:" (например "mvcable_").</summary>
    public string? StatePrefix { get; set; }

    /// <summary>YAML-поле "extraLayerPrefix:" (например "mvstripes_", у LV/HV null).</summary>
    public string? ExtraLayerPrefix { get; set; }
}

/// <summary>
/// Настройки одного компонента IconSmooth (режим Corners) из YAML. Может быть задан
/// у самого прототипа ИЛИ унаследован от предка через parent: — резолвинг в
/// PrototypeIndexer.GetIconSmoothInfo (который при наследовании НЕ подмешивает ключи
/// предков к дочерним, а выбирает ближайший заданный компонент целиком).
/// </summary>
public class IconSmoothSettings
{
    /// <summary>YAML-поле "key:" — SmoothKey. Объекты с одинаковым ключом сглаживаются друг с другом.</summary>
    public string? Key { get; set; }

    /// <summary>
    /// YAML-поле "base:" — префикс стейтов сглаживания. Итоговые стейты: "{base}0"…
    /// "{base}7", где число 0–7 = CornerFill (sum флагов трёх соседей угла).
    /// </summary>
    public string? Base { get; set; }

    /// <summary>
    /// YAML-поле "additionalKeys:" — объект сглаживается с такими же ключами, но сам
    /// НЕ является таковым для них (односторонняя связь — как MatchingEntity в игре:
    /// other.SmoothKey == наш Key ИЛИ наш AdditionalKeys содержит other.SmoothKey).
    /// </summary>
    public List<string> AdditionalKeys { get; set; } = new();

    /// <summary>YAML-поле "enabled:" — если явно false, сглаживание выключено. По умолчанию true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// YAML-поле "mode:" — режим сглаживания. Стандартный "Corners" (углы по соседям).
    /// "Diagonal" — диагональные окна: вместо 4 угловых слоёв рисуется один диагональный
    /// спрайт, разворачивающийся по повороту сущности (state — это кадр направления,
    /// а не маска соседей).
    /// </summary>
    public string Mode { get; set; } = "Corners";
}

/// <summary>
/// Один элемент YAML-списка "layers:" под компонентом Sprite. SpritePath/RsiPath/State
/// не заданы (null) — слой использует общий sprite: компонента Sprite и не имеет своего
/// override. Так чаще всего и бывает: у слоя задан только свой state в общей RSI.
/// </summary>
public class SpriteLayer
{
    public string? State { get; set; }
    public string? SpritePath { get; set; }
    public string? RsiPath { get; set; }
    /// <summary>Цвет тонирования слоя, формат "#RRGGBB"/"#RRGGBBAA" (YAML "color:").</summary>
    public string? Color { get; set; }
    public string? Shader { get; set; }
    public bool Visible { get; set; } = true;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
    public bool HasOffset { get; set; } = false;
    /// <summary>Имя ключа карты слоя из YAML "map:" (например "enum.StorageVisualLayers.Door").</summary>
    public string? MapKey { get; set; }
}