using System.Drawing;

namespace MapperIce.Services;

/// <summary>
/// Базовый класс для устройств сигнализации
/// </summary>
public abstract class AlarmDevice
{
    public int X { get; set; }
    public int Y { get; set; }
    public float Rotation { get; set; }
    public abstract string Type { get; }
}

/// <summary>
/// Пожарная сигнализация
/// </summary>
public class FireAlarmDevice : AlarmDevice
{
    public override string Type => "FireAlarm";
}

/// <summary>
/// Воздушная сигнализация
/// </summary>
public class AirAlarmDevice : AlarmDevice
{
    public override string Type => "AirAlarm";
}

/// <summary>
/// Пожарный шлюз
/// </summary>
public class FirelockDevice : AlarmDevice
{
    public bool IsGlass { get; set; }
    public override string Type => "Firelock";
}

/// <summary>
/// Труба (конец трубы)
/// </summary>
public class PipeDevice : AlarmDevice
{
    public string PipeType { get; set; } = "";
    public override string Type => "Pipe";
}

/// <summary>
/// Сеть сигнализации
/// </summary>
public class AlarmNetwork
{
    private readonly List<AlarmConnection> _connections = new();
    
    public IReadOnlyList<AlarmConnection> Connections => _connections;
    
    public void Connect(AlarmDevice source, AlarmDevice target)
    {
        _connections.Add(new AlarmConnection(source, target));
    }
    
    public void Connect(AlarmDevice source, AlarmDevice target, Color lineColor)
    {
        _connections.Add(new AlarmConnection(source, target, lineColor));
    }
    
    public void Connect(AlarmDevice source, AlarmDevice target, Color lineColor, float lineWidth)
    {
        _connections.Add(new AlarmConnection(source, target, lineColor, lineWidth));
    }
    
    public void Clear()
    {
        _connections.Clear();
    }
}

/// <summary>
/// Соединение между сигнализацией и устройством
/// </summary>
public class AlarmConnection
{
    public AlarmDevice Source { get; }
    public AlarmDevice Target { get; }
    public Color LineColor { get; set; }
    public float LineWidth { get; set; }
    
    public AlarmConnection(AlarmDevice source, AlarmDevice target)
    {
        Source = source;
        Target = target;
        LineColor = Color.FromArgb(255, 0, 255, 0); // Зелёный по умолчанию
        LineWidth = 2.0f;
    }
    
    public AlarmConnection(AlarmDevice source, AlarmDevice target, Color lineColor)
    {
        Source = source;
        Target = target;
        LineColor = lineColor;
        LineWidth = 2.0f;
    }
    
    public AlarmConnection(AlarmDevice source, AlarmDevice target, Color lineColor, float lineWidth)
    {
        Source = source;
        Target = target;
        LineColor = lineColor;
        LineWidth = lineWidth;
    }
}