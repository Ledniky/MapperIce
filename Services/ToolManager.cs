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
        ExpandRoom,
        Delete,
        DeleteArea,
        DeleteSettings,
        Door,
        DoorGlass,
        Passage,
        RestoreWall,
        RemoveWall,
        PipeDistra,
        PipeWaste,
        PipeNormal,
        PipeUtil,
        PipeUtilSettings,
        UtilWrenches,
        UtilNodeLabel,
        AirAlarm,
        FireAlarm,
        PlaceHolopad,
        PlaceLocker,
        PlaceSuitStorage,
        PlaceBed,
        PlaceCamera,
        PlaceDresser,
        PlaceIntercom,
        PlaceFax,
        PlaceLight,
        PlaceEmergencyLight,
        PlacePrototype,
        Move,
        DecalRule,
        NetworkConfigurator,
        Magnifier,
        WireHV,
        WireMV,
        WireLV,
        PlaceSubstation,
        PlaceApc,
        ReplaceWallWithWindow,
        GridDividerAdd,
        GridDividerSubtract
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