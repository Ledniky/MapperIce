namespace MapperIce.Services;

public class ToolManager
{
    public enum Tool
    {
        None,
        CreateRoom,
        Delete,
        Door  
    }

    private Tool _currentTool = Tool.None;
    // В ToolManager.cs добавьте:
    public string DoorProto { get; set; } = "Airlock";
    public Tool CurrentTool => _currentTool;
    public event Action<Tool>? ToolChanged;

    public void SetTool(Tool tool)
    {
        if (_currentTool == tool)
            _currentTool = Tool.None;
        else
            _currentTool = tool;
        
        ToolChanged?.Invoke(_currentTool);
    }
}