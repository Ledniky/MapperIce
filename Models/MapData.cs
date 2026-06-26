namespace MapperIce.Models;

public class MapData
{
    public List<Grid> Grids { get; set; } = new();
    public int? ActiveGridUid { get; set; }
    
    public Grid? ActiveGrid => Grids.FirstOrDefault(g => g.Uid == ActiveGridUid);
    
    public void AddGrid(Grid grid)
    {
        Grids.Add(grid);
        if (ActiveGridUid == null)
            ActiveGridUid = grid.Uid;
    }
    
    public void RemoveGrid(int uid)
    {
        var grid = Grids.FirstOrDefault(g => g.Uid == uid);
        if (grid != null)
        {
            Grids.Remove(grid);
            if (ActiveGridUid == uid)
                ActiveGridUid = Grids.FirstOrDefault()?.Uid;
        }
    }
}