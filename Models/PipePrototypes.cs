public static class PipePrototypes
{
    // Базовые трубы
    public const string PipeStraight = "GasPipeStraight";
    public const string PipeBend = "GasPipeBend";
    public const string PipeTJunction = "GasPipeTJunction";
    public const string PipeFourway = "GasPipeFourway";
    public const string PipeCap = "GasPipeCap";
    
    // Соединения по направлениям
    public static string GetPipeProto(HashSet<Direction> connections)
    {
        if (connections.Count == 0) return PipeCap;
        
        // Сортируем направления для консистентности
        var dirs = connections.OrderBy(d => d).ToList();
        
        if (dirs.Count == 1) return PipeCap;
        if (dirs.Count == 2)
        {
            // Проверяем противоположные направления (прямая труба)
            if ((dirs.Contains(Direction.North) && dirs.Contains(Direction.South)) ||
                (dirs.Contains(Direction.East) && dirs.Contains(Direction.West)))
                return PipeStraight;
            
            // Угловая труба (поворот)
            return PipeBend;
        }
        if (dirs.Count == 3) return PipeTJunction;
        if (dirs.Count == 4) return PipeFourway;
        
        return PipeStraight;
    }
}

public enum Direction
{
    North,
    South,
    East,
    West
}