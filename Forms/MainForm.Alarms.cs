// Forms/MainForm.Alarms.cs

using MapperIce.Models;
using MapperIce.Services;

namespace MapperIce.Forms;

public partial class MainForm
{

    private bool HasWallAt(Grid grid, int x, int y)
    {
        return grid.Rooms.Any(r =>
            x >= r.X && x < r.X + r.Width &&
            y >= r.Y && y < r.Y + r.Height &&
            (x == r.X || x == r.X + r.Width - 1 ||
             y == r.Y || y == r.Y + r.Height - 1));
    }


    private void AddAirAlarm(Grid grid, int x, int y)
    {
        if (grid == null) return;
        if (grid.Entities.OfType<AirAlarmEntity>().Any(e => (int)e.X == x && (int)e.Y == y)) return;

        if (_snapToGrid)
        {
            if (!HasWallAt(grid, x, y)) return;
            grid.Entities.Add(new AirAlarmEntity { X = x, Y = y, Rotation = _placeRotation });
        }
        else
        {
            grid.Entities.Add(new AirAlarmEntity { X = x, Y = y, Rotation = _placeRotation });
        }
    }


    private void AddFireAlarm(Grid grid, int x, int y)
    {
        if (grid == null) return;
        if (grid.Entities.OfType<FireAlarmEntity>().Any(e => (int)e.X == x && (int)e.Y == y)) return;

        if (_snapToGrid)
        {
            if (!HasWallAt(grid, x, y)) return;
            grid.Entities.Add(new FireAlarmEntity { X = x, Y = y, Rotation = _placeRotation });
        }
        else
        {
            grid.Entities.Add(new FireAlarmEntity { X = x, Y = y, Rotation = _placeRotation });
        }
    }


    private bool HasFloorAt(Grid grid, int x, int y)
    {
        return grid.Rooms.Any(r => r.Contains(x, y));
    }
}
