// Services/ToolManager.cs
using System;

namespace MapperIce.Services;

public class ToolManager
{
    public enum Tool
    {
        None,
        CreateRoom,
        SubtractRoom,
        RestoreRoom,
        Delete,
        DeleteArea,
        DeleteSettings,
        Door,
        DoorGlass,
        Passage,
        RestoreWall,
        PipeDistra,
        PipeWaste,
        PipeNormal,
        PipeUtil,
        PipeUtilSettings,
        UtilWrenches,
        UtilNodeLabel,
        AirAlarm,
        FireAlarm,
        PlacePrototype,
        Move,
        DecalRule
    }

    private Tool _currentTool = Tool.None;

    public Tool CurrentTool => _currentTool;

    public event Action<Tool>? ToolChanged;

    public void SetTool(Tool tool)
    {
        if (_currentTool == tool)
        {
            _currentTool = Tool.None;
        }
        else
        {
            _currentTool = tool;
        }

        ToolChanged?.Invoke(_currentTool);
    }

    public void ResetTool()
    {
        _currentTool = Tool.None;
        ToolChanged?.Invoke(_currentTool);
    }
    public void ForceSetTool(Tool tool)
    {
        _currentTool = tool;
        ToolChanged?.Invoke(_currentTool);
    }
}