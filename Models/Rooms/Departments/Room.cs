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
    public Color FillColor { get; set; } = Color.FromArgb(100, 230, 230, 230);
    public Color LineColor { get; set; } = Color.FromArgb(255, 180, 180, 180);
    public List<Door> Doors { get; set; } = new();
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
            FillColor = FillColor,
            LineColor = LineColor,
            Priority = Priority,
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
        
        return clone;
    }
    
    
    
    public bool Contains(int x, int y)
    {
        return x >= X && x < X + Width &&
               y >= Y && y < Y + Height &&
               !RemovedCells.Contains((x, y));
    }
}