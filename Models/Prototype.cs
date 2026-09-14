namespace MapperIce.Models;

public class Prototype
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";   // "tile" или "entity" — из YAML "- type: tile"
    public string? SpritePath { get; set; }
    public string? RsiPath { get; set; }
    public string? State { get; set; }
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
    /// Русское название сущности, найденное в локализации (.ftl, ключ "ent-{Id}").
    /// Резолвится с учётом Fluent-ссылок вида "{ ent-ДругойId }" (см. ApplyLocalizedNames
    /// в PrototypeIndexer). Null, если у прототипа нет собственной/унаследованной записи
    /// в локализации репозитория — в этом случае панель репозитория показывает id как раньше.
    /// </summary>
    public string? LocalizedName { get; set; }

    /// <summary>

    /// <summary>
    /// Слои компонента Sprite (YAML "layers:"). Пустой список — прототип без
    /// многослойности, рисуется как раньше через SpritePath/RsiPath/State.
    /// Если список не пуст, Renderer рисует все видимые слои по порядку друг
    /// поверх друга (как это делает сам движок Robust Toolbox).
    /// </summary>
    public List<SpriteLayer> Layers { get; set; } = new();
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
}