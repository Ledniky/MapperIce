using MapperIce.Models;
using System.Drawing;

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
        
        var fireAlarms = grid.Entities.OfType<FireAlarmEntity>().ToList();
        var airAlarms = grid.Entities.OfType<AirAlarmEntity>().ToList();
        var firelocks = grid.Entities.OfType<FirelockEntity>().ToList();
        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        
        var pipeEndpoints = GetPipeEndpoints(pipes);
        
        var allAlarms = new List<AlarmDevice>();
        
        foreach (var fireAlarm in fireAlarms)
        {
            allAlarms.Add(new FireAlarmDevice 
            { 
                X = (int)fireAlarm.X, 
                Y = (int)fireAlarm.Y, 
                Rotation = fireAlarm.Rotation 
            });
        }
        
        foreach (var airAlarm in airAlarms)
        {
            allAlarms.Add(new AirAlarmDevice 
            { 
                X = (int)airAlarm.X, 
                Y = (int)airAlarm.Y, 
                Rotation = airAlarm.Rotation 
            });
        }
        
        foreach (var alarm in allAlarms)
        {
            // Сначала ищем комнату по направлению сигнализации
            var room = GetRoomByAlarmDirection(grid, alarm.X, alarm.Y, alarm.Rotation);
            
            // Если не нашли по направлению - пробуем по координатам
            if (room == null)
            {
                room = GetRoomAt(grid, alarm.X, alarm.Y);
            }
            
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
    /// Находит комнату по направлению сигнализации
    /// </summary>
    private Room? GetRoomByAlarmDirection(Grid grid, int x, int y, float rotation)
    {
        int dx = 0, dy = 0;

        // Нормализуем ротацию в диапазон [0, 2*PI)
        float normalized = rotation % (float)(2 * Math.PI);
        if (normalized < 0) normalized += (float)(2 * Math.PI);

        // В SS14/SS13:
        // 0° = смотрит вниз (юг) → комната снизу
        // 90° = смотрит влево (запад) → комната слева
        // 180° = смотрит вверх (север) → комната сверху
        // 270° = смотрит вправо (восток) → комната справа

        if (Math.Abs(normalized) < 0.1f || Math.Abs(normalized - (float)(2 * Math.PI)) < 0.1f)
        {
            // 0° = смотрит вниз (юг)
            dy = 1; // ← ИСПРАВЛЕНО: было -1
        }
        else if (Math.Abs(normalized - (float)(Math.PI / 2)) < 0.1f)
        {
            // 90° = смотрит влево (запад)
            dx = -1; // ← ИСПРАВЛЕНО: было 1
        }
        else if (Math.Abs(normalized - (float)Math.PI) < 0.1f)
        {
            // 180° = смотрит вверх (север)
            dy = -1; // ← ИСПРАВЛЕНО: было 1
        }
        else if (Math.Abs(normalized - (float)(3 * Math.PI / 2)) < 0.1f ||
                 Math.Abs(normalized - (float)(-Math.PI / 2)) < 0.1f)
        {
            // 270° = смотрит вправо (восток)
            dx = 1; // ← ИСПРАВЛЕНО: было -1
        }
        else
        {
            // Если ротация нестандартная - определяем по ближайшему направлению
            var directions = new (float angle, int dx, int dy)[]
            {
            (0, 0, 1),      // юг
            ((float)(Math.PI / 2), -1, 0),  // запад
            ((float)Math.PI, 0, -1),        // север
            ((float)(3 * Math.PI / 2), 1, 0) // восток
            };

            float minDiff = float.MaxValue;
            foreach (var dir in directions)
            {
                float diff = Math.Abs(normalized - dir.angle);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    dx = dir.dx;
                    dy = dir.dy;
                }
            }
        }

        // Проверяем комнату в направлении сигнализации (1 клетка)
        int targetX = x + dx;
        int targetY = y + dy;

        var room = grid.Rooms.FirstOrDefault(r =>
            targetX >= r.X && targetX < r.X + r.Width &&
            targetY >= r.Y && targetY < r.Y + r.Height);

        // Если не нашли - пробуем на 2 клетки дальше
        if (room == null)
        {
            targetX = x + dx * 2;
            targetY = y + dy * 2;
            room = grid.Rooms.FirstOrDefault(r =>
                targetX >= r.X && targetX < r.X + r.Width &&
                targetY >= r.Y && targetY < r.Y + r.Height);
        }

        return room;
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
        var grouped = pipes.GroupBy(p => p.PipeType);
        
        foreach (var group in grouped)
        {
            var pipeList = group.ToList();
            
            foreach (var pipe in pipeList)
            {
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