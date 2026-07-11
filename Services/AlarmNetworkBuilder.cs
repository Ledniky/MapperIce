using MapperIce.Models;

namespace MapperIce.Services;

public class AlarmNetworkBuilder
{
    private readonly Dictionary<string, AlarmSettings> _settings;
    
    public AlarmNetworkBuilder(Dictionary<string, AlarmSettings> settings)
    {
        _settings = settings;
    }
    
    public AlarmNetwork BuildNetwork(Grid grid)
    {
        var network = new AlarmNetwork();
        
        // Находим все сигнализации
        var fireAlarms = grid.Entities.OfType<FireAlarmEntity>().ToList();
        var airAlarms = grid.Entities.OfType<AirAlarmEntity>().ToList();
        var firelocks = grid.Entities.OfType<FirelockEntity>().ToList();
        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        
        // Находим концы труб (скрубберы и вентиляции)
        var pipeEndpoints = GetPipeEndpoints(pipes);
        
        // Объединяем все сигнализации в один список
        var allAlarms = new List<AlarmDevice>();
        
        // Добавляем пожарные сигнализации
        foreach (var fireAlarm in fireAlarms)
        {
            allAlarms.Add(new FireAlarmDevice 
            { 
                X = (int)fireAlarm.X, 
                Y = (int)fireAlarm.Y, 
                Rotation = fireAlarm.Rotation 
            });
        }
        
        // Добавляем воздушные сигнализации
        foreach (var airAlarm in airAlarms)
        {
            allAlarms.Add(new AirAlarmDevice 
            { 
                X = (int)airAlarm.X, 
                Y = (int)airAlarm.Y, 
                Rotation = airAlarm.Rotation 
            });
        }
        
        // Для КАЖДОЙ сигнализации связываем со ВСЕМИ устройствами в комнате
        foreach (var alarm in allAlarms)
        {
            // Находим комнату, в которой стоит сигнализация
            var room = GetRoomAt(grid, alarm.X, alarm.Y);
            if (room == null) continue;
            
            // 1. Связываем с пожарными шлюзами в этой комнате
            var roomFirelocks = firelocks
                .Where(f => IsInsideRoom(f.X, f.Y, room))
                .ToList();
                
            foreach (var firelock in roomFirelocks)
            {
                network.Connect(
                    alarm,
                    new FirelockDevice { X = (int)firelock.X, Y = (int)firelock.Y, IsGlass = firelock.IsGlass }
                );
            }
            
            // 2. Связываем с концами труб в этой комнате
            var roomEndpoints = pipeEndpoints
                .Where(p => IsInsideRoom(p.X, p.Y, room))
                .ToList();
                
            foreach (var endpoint in roomEndpoints)
            {
                network.Connect(
                    alarm,
                    new PipeDevice { X = (int)endpoint.X, Y = (int)endpoint.Y, PipeType = endpoint.PipeType }
                );
            }
        }
        
        return network;
    }
    
    /// <summary>
    /// Находит комнату по координатам
    /// </summary>
    private Room? GetRoomAt(Grid grid, int x, int y)
    {
        return grid.Rooms.FirstOrDefault(r => 
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height);
    }
    
    /// <summary>
    /// Проверяет, находится ли точка внутри комнаты
    /// </summary>
    private bool IsInsideRoom(float x, float y, Room room)
    {
        return x >= room.X && x < room.X + room.Width &&
               y >= room.Y && y < room.Y + room.Height;
    }
    
    /// <summary>
    /// Находит концы труб (скрубберы и вентиляции)
    /// </summary>
    private List<PipeEntity> GetPipeEndpoints(List<PipeEntity> pipes)
    {
        var endpoints = new List<PipeEntity>();
        
        // Группируем трубы по типу
        var grouped = pipes.GroupBy(p => p.PipeType);
        
        foreach (var group in grouped)
        {
            var pipeList = group.ToList();
            
            foreach (var pipe in pipeList)
            {
                // Проверяем, является ли труба концом
                int neighbors = GetNeighbors(pipeList, (int)pipe.X, (int)pipe.Y).Count;
                
                // Если у трубы 1 сосед - это конец (скруббер или вентиляция)
                if (neighbors == 1)
                {
                    endpoints.Add(pipe);
                }
            }
        }
        
        return endpoints;
    }
    
    /// <summary>
    /// Находит соседей трубы
    /// </summary>
    private List<(int dx, int dy)> GetNeighbors(List<PipeEntity> pipes, int x, int y)
    {
        var neighbors = new List<(int dx, int dy)>();
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        
        foreach (var (dx, dy) in directions)
        {
            if (pipes.Any(p => (int)p.X == x + dx && (int)p.Y == y + dy))
            {
                neighbors.Add((dx, dy));
            }
        }
        
        return neighbors;
    }
}

// ============================================================
// ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ДЛЯ УСТРОЙСТВ
// ============================================================

public class FirelockDevice : AlarmDevice
{
    public bool IsGlass { get; set; }
    public override string Type => "Firelock";
}

public class PipeDevice : AlarmDevice
{
    public string PipeType { get; set; } = "";
    public override string Type => "Pipe";
}