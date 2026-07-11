using System.Drawing;

namespace MapperIce.Models;

public class AlarmNetwork
{
    public List<AlarmConnection> Connections { get; set; } = new();
    
    public void Connect(AlarmDevice source, AlarmDevice target)
    {
        if (source == null || target == null) return;
        if (source == target) return;
        
        // Проверяем, есть ли уже такая связь
        if (Connections.Any(c => c.Source == source && c.Target == target))
            return;
            
        Connections.Add(new AlarmConnection(source, target));
    }
    
    public void Disconnect(AlarmDevice source, AlarmDevice target)
    {
        var connection = Connections.FirstOrDefault(c => c.Source == source && c.Target == target);
        if (connection != null)
            Connections.Remove(connection);
    }
    
    public List<AlarmConnection> GetConnectionsFor(AlarmDevice device)
    {
        return Connections.Where(c => c.Source == device || c.Target == device).ToList();
    }
}

public class AlarmConnection
{
    public AlarmDevice Source { get; set; }
    public AlarmDevice Target { get; set; }
    public Color LineColor { get; set; } = Color.FromArgb(255, 255, 200, 50);
    public int LineWidth { get; set; } = 2;
    
    public AlarmConnection(AlarmDevice source, AlarmDevice target)
    {
        Source = source;
        Target = target;
    }
}

public abstract class AlarmDevice
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Id { get; set; } = "";
    public virtual string Type { get; set; } = "";  // ← virtual ДОБАВЛЕНО
    public List<AlarmDevice> ConnectedDevices { get; set; } = new();
}

public class FireAlarmDevice : AlarmDevice
{
    public List<FirelockEntity> ConnectedFirelocks { get; set; } = new();
    public List<PipeEntity> ConnectedPipes { get; set; } = new();
    public float Rotation { get; set; }
    public override string Type => "FireAlarm";  // ← override ДОБАВЛЕНО
}

public class AirAlarmDevice : AlarmDevice
{
    public List<FirelockEntity> ConnectedFirelocks { get; set; } = new();
    public List<PipeEntity> ConnectedPipes { get; set; } = new();
    public float Rotation { get; set; }
    public override string Type => "AirAlarm";  // ← override ДОБАВЛЕНО
}