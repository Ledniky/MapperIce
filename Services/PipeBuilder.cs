using MapperIce.Models;

namespace MapperIce.Services;

public class PipeBuilder
{
    private readonly PipeTypeManager _pipeTypeManager;

    public PipeBuilder(PipeTypeManager pipeTypeManager)
    {
        _pipeTypeManager = pipeTypeManager;
    }

    public List<(int x, int y, string proto)> BuildPipeNetwork(HashSet<(int x, int y)> pipePositions, string pipeTypeName)
    {
        var result = new List<(int x, int y, string proto)>();
        if (pipePositions.Count == 0) return result;

        var pipeType = _pipeTypeManager.GetPipeType(pipeTypeName);
        var visited = new HashSet<(int x, int y)>();
        
        // Находим все кластеры (связные группы труб)
        var clusters = FindClusters(pipePositions);
        
        foreach (var cluster in clusters)
        {
            // Для каждого кластера определяем концы (тупики)
            var ends = FindEnds(cluster);
            
            // Строим сеть
            var network = BuildNetwork(cluster, pipeType);
            result.AddRange(network);
        }
        
        return result;
    }
    
    private List<HashSet<(int x, int y)>> FindClusters(HashSet<(int x, int y)> positions)
    {
        var clusters = new List<HashSet<(int x, int y)>>();
        var unvisited = new HashSet<(int x, int y)>(positions);
        
        while (unvisited.Count > 0)
        {
            var start = unvisited.First();
            var cluster = new HashSet<(int x, int y)>();
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(start);
            cluster.Add(start);
            unvisited.Remove(start);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbors = GetNeighbors(current, unvisited);
                foreach (var neighbor in neighbors)
                {
                    cluster.Add(neighbor);
                    unvisited.Remove(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
            
            clusters.Add(cluster);
        }
        
        return clusters;
    }
    
    private List<(int x, int y, string proto)> BuildNetwork(HashSet<(int x, int y)> cluster, PipeType pipeType)
    {
        var result = new List<(int x, int y, string proto)>();
        
        foreach (var pos in cluster)
        {
            var connections = GetConnections(pos, cluster);
            var proto = GetPipeProto(connections, pipeType);
            result.Add((pos.x, pos.y, proto));
        }
        
        return result;
    }
    
    private HashSet<(int x, int y)> FindEnds(HashSet<(int x, int y)> cluster)
    {
        var ends = new HashSet<(int x, int y)>();
        
        foreach (var pos in cluster)
        {
            var connections = GetConnections(pos, cluster);
            if (connections.Count == 1)
                ends.Add(pos);
        }
        
        return ends;
    }
    
    private HashSet<Direction> GetConnections((int x, int y) pos, HashSet<(int x, int y)> cluster)
    {
        var connections = new HashSet<Direction>();
        
        if (cluster.Contains((pos.x, pos.y - 1))) connections.Add(Direction.North);
        if (cluster.Contains((pos.x, pos.y + 1))) connections.Add(Direction.South);
        if (cluster.Contains((pos.x + 1, pos.y))) connections.Add(Direction.East);
        if (cluster.Contains((pos.x - 1, pos.y))) connections.Add(Direction.West);
        
        return connections;
    }
    
    private List<(int x, int y)> GetNeighbors((int x, int y) pos, HashSet<(int x, int y)> positions)
    {
        var neighbors = new List<(int x, int y)>();
        var directions = new (int dx, int dy)[] { (0, -1), (0, 1), (1, 0), (-1, 0) };
        
        foreach (var (dx, dy) in directions)
        {
            var neighbor = (pos.x + dx, pos.y + dy);
            if (positions.Contains(neighbor))
                neighbors.Add(neighbor);
        }
        
        return neighbors;
    }

    private string GetPipeProto(HashSet<Direction> connections, PipeType pipeType)
    {
        if (connections.Count == 0) return pipeType.ProtoCap;
        
        var dirs = connections.OrderBy(d => d).ToList();
        
        if (dirs.Count == 1) return pipeType.ProtoCap;
        if (dirs.Count == 2)
        {
            // Проверяем противоположные направления (прямая труба)
            if ((dirs.Contains(Direction.North) && dirs.Contains(Direction.South)) ||
                (dirs.Contains(Direction.East) && dirs.Contains(Direction.West)))
                return pipeType.ProtoStraight;
            
            // Угловая труба (поворот)
            return pipeType.ProtoBend;
        }
        if (dirs.Count == 3) return pipeType.ProtoTJunction;
        if (dirs.Count == 4) return pipeType.ProtoFourway;
        
        return pipeType.ProtoStraight;
    }
}

public enum Direction
{
    North,
    South,
    East,
    West
}