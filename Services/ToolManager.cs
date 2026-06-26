namespace MapperIce.Services;

/// <summary>
/// Управляет выбранным инструментом и состоянием
/// </summary>
public class ToolManager
{
    public enum Tool
    {
        None,
        CreateRoom,
        Delete
    }

    private Tool _currentTool = Tool.None;

    public Tool CurrentTool => _currentTool;

    // Событие при смене инструмента
    public event Action<Tool>? ToolChanged;

    public void SetTool(Tool tool)
    {
        if (_currentTool == tool)
        {
            // Нажали на ту же кнопку - сбрасываем
            _currentTool = Tool.None;
        }
        else
        {
            _currentTool = tool;
        }
        ToolChanged?.Invoke(_currentTool);
    }

    public bool IsToolActive(Tool tool) => _currentTool == tool;
    public bool IsAnyToolActive => _currentTool != Tool.None;
}