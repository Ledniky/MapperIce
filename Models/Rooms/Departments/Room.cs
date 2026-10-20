using System.Drawing;

namespace MapperIce.Models;

public class Room
{
    public int X { get; set; }
    public int Y { get; set; }
    public HashSet<(int X, int Y)> RemovedCells { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }
    public string RoomType { get; set; } = "General";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
public string? AirAlarmProto { get; set; } = null;   // переопределение прототипа сигнализации для этой комнаты
    public string? FireAlarmProto { get; set; } = null;
    public string? HolopadProto { get; set; } = null;   // прототип голопада, привязанный к этой комнате
    public string? LockerProto { get; set; } = null;   // прототип шкафчика, привязанный к этой комнате
    public string? SuitStorageProto { get; set; } = null;   // прототип хранилища скафандра, привязанный к этой комнате
    public string? BedProto { get; set; } = null;   // прототип кровати, привязанный к этой комнате
    public string? BedsheetProto { get; set; } = null;   // прототип одеяла, привязанный к этой комнате
    public string? CameraProto { get; set; } = null;   // прототип камеры, привязанный к этой комнате
    public string? DresserProto { get; set; } = null;   // прототип комода, привязанный к этой комнате (только у глав отделов)
    public string? IntercomProto { get; set; } = null;   // прототип интеркома, привязанный к этой комнате (вешается в тайл-стену)
    public string? FaxName { get; set; } = null;   // адрес факса, привязанного к этой комнате (name компонента FaxMachine)
    public string? FaxProto { get; set; } = null;   // прототип факса, привязанного к этой комнате (FaxMachineBase / FaxMachineCaptain)
    public string? LightProto { get; set; } = null;   // обычная лампа (по умолчанию Poweredlight)
    public string? EmergencyLightProto { get; set; } = null;   // аварийная лампа (по умолчанию EmergencyLight)
    public Color FillColor { get; set; } = Color.FromArgb(100, 230, 230, 230);
    public Color LineColor { get; set; } = Color.FromArgb(255, 180, 180, 180);
    public List<Door> Doors { get; set; } = new();
    public List<(int X, int Y)> Passages { get; set; } = new();
    // Клетки на границе комнаты, где стена убрана инструментом «Убрать стену»:
    // TileBuilder не строит на них стену (как у прохода), но DecalPatternBuilder
    // продолжает считать их стеной — узор обводит такую клетку, будто стена на
    // месте. В отличие от прохода, это «невидимая» стена для декальрула.
    public List<(int X, int Y)> HiddenWalls { get; set; } = new();
    // Клетки на границе комнаты, где стена заменена на окно (инструмент
    // "Заменить на окно") — BuildFromRooms пропускает их так же, как Passages,
    // иначе стена восстанавливается заново при следующей пересборке TileGrid
    public List<(int X, int Y)> Windows { get; set; } = new();
    public DecalPatternMode DecalMode { get; set; } = DecalPatternMode.Auto;
    public DecalRuleSet AutoDecalRule { get; set; } = new();
    public List<ManualDecalArea> ManualDecalAreas { get; set; } = new();

    // false (по умолчанию) — AutoDecalRule ещё не редактировалось вручную через "Узор
    // по периметру", поэтому при каждом пересчёте декалей DecalPatternBuilder имеет
    // право заново подтягивать актуальное правило узла из "Наследования декалей"
    // (или дефолт RoomType, если явного правила нигде в цепочке нет). true — комната
    // хоть раз была отредактирована вручную (добавлен/удалён/переставлен слой, выбран
    // другой пак, изменён цвет и т.п.) — с этого момента она "отвязывается" от узла
    // и живёт своей собственной, независимой копией правила.
    public bool HasCustomDecalRule { get; set; } = false;
    public int Priority { get; set; } = 0;


    // Стабильный идентификатор комнаты: сохраняется в Clone(), в отличие от
    // GetHashCode(), который у каждого клона новый. По нему декали узора
    // привязаны к комнате (PlacedDecal.PatternOwnerId).
    private static int _nextId = 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public int Id { get; set; } = Interlocked.Increment(ref _nextId);

    /// <summary>
    /// Восстановить стабильный Id из файла проекта (см. RoomGridData.Id). Гарантирует,
    /// что после загрузки комната и её декали (PlacedDecal.PatternOwnerId) снова сходятся
    /// по одному ключу. Игнорирует невалидные/дублирующиеся значения: комнате оставляется
    /// авто-инкремент, а декали пересоберутся RecalculateAll.
    /// </summary>
    public void RestoreId(int id)
    {
        if (id > 0)
        {
            Id = id;
            if (_nextId <= id)
                Interlocked.Exchange(ref _nextId, id + 1);
        }
    }

    public Room Clone()
    {
        var clone = new Room
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            RoomType = RoomType,
            WallProto = WallProto,
            FloorProto = FloorProto,
            DoorProto = DoorProto,
            GlassDoorProto = GlassDoorProto,
            AirAlarmProto = AirAlarmProto,
            FireAlarmProto = FireAlarmProto,
            HolopadProto = HolopadProto,
            LockerProto = LockerProto,
            SuitStorageProto = SuitStorageProto,
            BedProto = BedProto,
            BedsheetProto = BedsheetProto,
            CameraProto = CameraProto,
            DresserProto = DresserProto,
            IntercomProto = IntercomProto,
            FaxName = FaxName,
            FaxProto = FaxProto,
            LightProto = LightProto,
            EmergencyLightProto = EmergencyLightProto,
            FillColor = FillColor,
            LineColor = LineColor,
            Id = Id,
            Priority = Priority,
            HasCustomDecalRule = HasCustomDecalRule,
            RemovedCells = new HashSet<(int X, int Y)>(RemovedCells),
            DecalMode = DecalMode,
            AutoDecalRule = AutoDecalRule.Clone(),
            ManualDecalAreas = ManualDecalAreas.Select(a => a.Clone()).ToList()
        };

        foreach (var door in Doors)
        {
            clone.Doors.Add(new Door
            {
                X = door.X,
                Y = door.Y,
                Proto = door.Proto
            });
        }

        clone.Passages = new List<(int X, int Y)>(Passages);
        clone.HiddenWalls = new List<(int X, int Y)>(HiddenWalls);
        clone.Windows = new List<(int X, int Y)>(Windows);

        return clone;
    }



    public bool Contains(int x, int y)
    {
        return x >= X && x < X + Width &&
               y >= Y && y < Y + Height &&
               !RemovedCells.Contains((x, y));
    }

    /// <summary>
    /// true, если клетка (x,y) принадлежит комнате и при этом её сосед в
    /// направлении (dx,dy) комнате НЕ принадлежит — то есть на этой стороне
    /// клетки физически стена. Учитывает RemovedCells через Contains, поэтому
    /// корректно работает для нерямоугольных комнат, а не только для краёв
    /// ограничивающего прямоугольника.
    /// </summary>
    public bool HasWallOnSide(int x, int y, int dx, int dy)
    {
        return Contains(x, y) && !Contains(x + dx, y + dy);
    }
}