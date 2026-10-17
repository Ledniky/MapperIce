using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapperIce.Models;

public class ProjectData
{
    public List<GridData> Grids { get; set; } = new();
    public string? ActiveGridName { get; set; }
    public DateTime LastSaved { get; set; }
    public string? Version { get; set; } = "1.0";

    // Настройки слоёв труб (цвет, вентиляция на концах) — часть проекта, а не
    // сеанса. Без сохранения после загрузки .ice цвета слоёв возвращались к
    // PipeSettings.DefaultLayers (цвет «сбрасывался»).
    public List<PipeLayerData> PipeLayers { get; set; } = new();
}

/// <summary>
/// Сохранённая настройка слоя труб. Цвет — строкой "A,R,G,B", как у прочих
/// цветов проекта (GridData.Color, RoomGridData.FillColor). Подпись слоя (L1/L2/L3)
/// здесь НЕ хранится: это фиксированная метка из PipeSettings.DefaultLayers, а не
/// пользовательская настройка — иначе старый файл возвращал бы прежнее название
/// слоя после переименования.
/// </summary>
public class PipeLayerData
{
    public string Layer { get; set; } = "";
    public string Color { get; set; } = "255,0,85,204";
    public bool HasColor { get; set; } = true;
    public string VentProto { get; set; } = "GasVentScrubber";
}

public class GridData
{
    public int Uid { get; set; }
    public string Name { get; set; } = "grid";
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public List<RoomGridData> Rooms { get; set; } = new();
    public List<GenericEntityData> Entities { get; set; } = new();
    public List<PlacedTile> Tiles { get; set; } = new();
    public List<PlacedDecal> Decals { get; set; } = new();
    public List<DoorData> LooseDoors { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public string Color { get; set; } = "255,0,0,255";
    public bool IsStaticGrid { get; set; } = false;
    public string ExteriorWallProto { get; set; } = "";
    public bool ReplaceExteriorWalls { get; set; } = true;
    public List<GridDividerAreaData> DividerAreas { get; set; } = new();
}

/// <summary>
/// Сохранённая область разделения грида (инструмент «Разделитель гридов»).
/// Новый формат — потайловый: Cells. AddRects/SubRects оставлены только для
/// чтения старых .ice-проектов и при сохранении не пишутся (null).
/// </summary>
public class GridDividerAreaData
{
    public List<GridDividerCellData> Cells { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GridDividerRectData>? AddRects { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GridDividerRectData>? SubRects { get; set; }
}

/// <summary>Одна выделенная клетка области разделения (формат проекта).</summary>
public class GridDividerCellData
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Прямоугольник старого формата — только для чтения прежних проектов.</summary>
public class GridDividerRectData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class RoomGridData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string RoomType { get; set; } = "General";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string? AirAlarmProto { get; set; }
    public string? FireAlarmProto { get; set; }
    public string? HolopadProto { get; set; }
    public string? LockerProto { get; set; }
    public string? SuitStorageProto { get; set; }
    public string? BedProto { get; set; }
    public string? BedsheetProto { get; set; }
    public string? CameraProto { get; set; }
    public string? DresserProto { get; set; }
    public string? IntercomProto { get; set; }
    public string? FaxName { get; set; }
    public string? FaxProto { get; set; }
    public string FillColor { get; set; } = "100,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public List<DoorData> Doors { get; set; } = new();
    public List<string> RemovedCells { get; set; } = new();
    public List<string> Passages { get; set; } = new();
    public List<string> Windows { get; set; } = new();
    public DecalPatternMode DecalMode { get; set; } = DecalPatternMode.Auto;
    public DecalRuleData? AutoDecalRule { get; set; }
    public List<ManualDecalAreaData> ManualDecalAreas { get; set; } = new();
    public bool HasCustomDecalRule { get; set; } = false;
    public int Priority { get; set; } = 0;
    public int Id { get; set; } = 0;
}

public class DecalRuleData
{
    public List<DecalLayerData> Layers { get; set; } = new();
}

public class DecalLayerData
{
    public string Name { get; set; } = "Слой";
    public string? SourcePackId { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Color { get; set; }
    public DecalPatternMode Mode { get; set; } = DecalPatternMode.Auto;
    public List<ManualDecalAreaData> ManualAreas { get; set; } = new();
}

public class ManualDecalAreaData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ManualAreaMode Mode { get; set; } = ManualAreaMode.WallRegion;
    public List<DecalAreaRectData> Subtractions { get; set; } = new();
    // Потайлёво вырезанные клетки декалей — список строк "x,y"
    public List<string> CutCells { get; set; } = new();
    // Потайлёво удалённые из области клетки (изменение формы, «➖ Вычесть») — список строк "x,y"
    public List<string> RemovedCells { get; set; } = new();
}

public class DecalAreaRectData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class RoomData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string RoomType { get; set; } = "";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public List<DoorData> Doors { get; set; } = new();
}

public class DoorData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Proto { get; set; } = "Airlock";
}

public class GenericEntityData
{
    public string Type { get; set; } = "";
    public JsonElement Data { get; set; }
}
