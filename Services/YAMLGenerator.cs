using MapperIce.Models;
using System.Text;

namespace MapperIce.Services;

public class YAMLGenerator
{
    private const int CHUNK_SIZE = 16;

    // Приоритеты стен: 0 - самый низкий, больше - выше
    private static readonly Dictionary<string, int> _wallPriority = new()
    {
        { "WallSolid", 0 },
        { "WallReinforced", 1 },
        // Все остальные стены имеют приоритет 2
    };

    private int GetPriority(string wall) => _wallPriority.GetValueOrDefault(wall, 2);
    private string BestWall(string a, string b) => GetPriority(a) >= GetPriority(b) ? a : b;
    private Room? GetRoomAt(List<Room> rooms, int x, int y) => rooms.FirstOrDefault(r => x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

    public string Generate(List<Room> rooms)
    {
        var sb = new StringBuilder();

        // Meta
        sb.AppendLine("meta:");
        sb.AppendLine("  format: 7");
        sb.AppendLine("  category: Map");
        sb.AppendLine("  engineVersion: 266.0.0");
        sb.AppendLine("  forkId: \"\"");
        sb.AppendLine("  forkVersion: \"\"");
        sb.AppendLine($"  time: {DateTime.Now:MM/dd/yyyy HH:mm:ss}");
        sb.AppendLine($"  entityCount: {CountEntities(rooms)}");

        // Maps и Grids
        sb.AppendLine("maps:");
        sb.AppendLine("- 1");
        sb.AppendLine("grids:");
        sb.AppendLine("- 2");

        sb.AppendLine("orphans: []");
        sb.AppendLine("nullspace: []");

        // Tilemap
        sb.AppendLine("tilemap:");
        sb.AppendLine("  0: Space");
        sb.AppendLine("  1: Plating");

        // Entities
        sb.AppendLine("entities:");
        sb.AppendLine("- proto: \"\"");
        sb.AppendLine("  entities:");
        sb.AppendLine("  - uid: 1");
        sb.AppendLine("    components:");
        sb.AppendLine("    - type: MetaData");
        sb.AppendLine("      name: Map Entity");
        sb.AppendLine("    - type: Transform");
        sb.AppendLine("    - type: Map");
        sb.AppendLine("      mapPaused: True");
        sb.AppendLine("    - type: GridTree");
        sb.AppendLine("    - type: Broadphase");
        sb.AppendLine("    - type: OccluderTree");

        // Grid
        sb.AppendLine("  - uid: 2");
        sb.AppendLine("    components:");
        sb.AppendLine("    - type: MetaData");
        sb.AppendLine("      name: grid");
        sb.AppendLine("    - type: Transform");
        sb.AppendLine("      pos: 0,0");
        sb.AppendLine("      parent: 1");
        sb.AppendLine("    - type: MapGrid");
        sb.AppendLine("      chunks:");

        var chunks = GenerateChunks(rooms);
        foreach (var chunk in chunks)
        {
            sb.AppendLine($"        {chunk.Key.x},{chunk.Key.y}:");
            sb.AppendLine($"          ind: {chunk.Key.x},{chunk.Key.y}");
            sb.AppendLine($"          tiles: {chunk.Value}");
            sb.AppendLine($"          version: 7");
        }

        sb.AppendLine("    - type: Broadphase");
        sb.AppendLine("    - type: Physics");
        sb.AppendLine("      bodyStatus: InAir");
        sb.AppendLine("      fixedRotation: False");
        sb.AppendLine("      bodyType: Dynamic");
        sb.AppendLine("    - type: Fixtures");
        sb.AppendLine("      fixtures: {}");
        sb.AppendLine("    - type: OccluderTree");
        sb.AppendLine("    - type: SpreaderGrid");
        sb.AppendLine("    - type: Shuttle");
        sb.AppendLine("      dampingModifiers:");
        sb.AppendLine("        Cruise: 0.0075");
        sb.AppendLine("        Dampen: 0.25");
        sb.AppendLine("        Anchor: 2");
        sb.AppendLine("        None: 0.25");
        sb.AppendLine("      dampingModifier: 0.25");
        sb.AppendLine("    - type: ImplicitRoof");
        sb.AppendLine("    - type: FTLDrive");
        sb.AppendLine("    - type: GridPathfinding");
        sb.AppendLine("    - type: Gravity");
        sb.AppendLine("      gravityShakeSound: !type:SoundPathSpecifier");
        sb.AppendLine("        path: /Audio/Effects/alert.ogg");
        sb.AppendLine("    - type: DecalGrid");
        sb.AppendLine("      chunkCollection:");
        sb.AppendLine("        version: 2");
        sb.AppendLine("        nodes: []");
        sb.AppendLine("    - type: GridAtmosphere");
        sb.AppendLine("      version: 2");
        sb.AppendLine("      data:");
        sb.AppendLine("        chunkSize: 4");
        sb.AppendLine("    - type: GasTileOverlay");
        sb.AppendLine("    - type: RadiationGridResistance");

        // Генерируем стены с приоритетами
        int uid = GenerateWalls(sb, rooms);
        GenerateDoors(sb, rooms, ref uid);

        return sb.ToString();
    }

    private Dictionary<(int x, int y), string> GenerateChunks(List<Room> rooms)
    {
        var chunks = new Dictionary<(int x, int y), int[]>();

        foreach (var room in rooms)
        {
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    int invY = -y;

                    int cx = x / CHUNK_SIZE;
                    int cy = invY / CHUNK_SIZE;
                    int lx = x % CHUNK_SIZE;
                    int ly = invY % CHUNK_SIZE;

                    if (ly < 0)
                    {
                        ly += CHUNK_SIZE;
                        cy--;
                    }

                    var key = (cx, cy);
                    if (!chunks.ContainsKey(key))
                    {
                        var tiles = new int[CHUNK_SIZE * CHUNK_SIZE];
                        for (int i = 0; i < tiles.Length; i++)
                            tiles[i] = 0;
                        chunks[key] = tiles;
                    }

                    int index = ly * CHUNK_SIZE + lx;
                    chunks[key][index] = 1;
                }
            }
        }

        var result = new Dictionary<(int x, int y), string>();
        foreach (var kvp in chunks)
        {
            result[kvp.Key] = EncodeTiles(kvp.Value);
        }

        return result;
    }

    private string EncodeTiles(int[] tileIds)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < tileIds.Length; i++)
        {
            bytes.AddRange(BitConverter.GetBytes(tileIds[i]));
            bytes.Add(0);
            bytes.Add(0);
            bytes.Add(0);
        }
        return Convert.ToBase64String(bytes.ToArray());
    }

    private int CountEntities(List<Room> rooms)
    {
        int count = 2;
        foreach (var room in rooms)
        {
            count += room.Width * 2 + room.Height * 2 - 4;
            count += (room.Width - 2) * (room.Height - 2);
            count += room.Doors.Count;
        }
        return count;
    }

    private int GenerateWalls(StringBuilder sb, List<Room> rooms)
    {
        int uid = 3;

        // Собираем все позиции дверей
        var doorPositions = new HashSet<(int x, int y)>();
        foreach (var room in rooms)
        {
            foreach (var door in room.Doors)
            {
                doorPositions.Add((door.X, door.Y));
            }
        }

        // Собираем стены с учётом приоритетов
        var wallMap = new Dictionary<(int x, int y), string>();

        foreach (var room in rooms)
        {
            // Верхняя стена
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                int y = room.Y;
                if (doorPositions.Contains((x, y))) continue;

                var neighbor = GetRoomAt(rooms, x, y - 1);
                string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                
                var key = (x, y);
                if (!wallMap.ContainsKey(key) || GetPriority(wall) > GetPriority(wallMap[key]))
                    wallMap[key] = wall;
            }

            // Нижняя стена
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                int y = room.Y + room.Height - 1;
                if (doorPositions.Contains((x, y))) continue;

                var neighbor = GetRoomAt(rooms, x, y + 1);
                string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                
                var key = (x, y);
                if (!wallMap.ContainsKey(key) || GetPriority(wall) > GetPriority(wallMap[key]))
                    wallMap[key] = wall;
            }

            // Левая стена (без углов)
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
            {
                int x = room.X;
                if (doorPositions.Contains((x, y))) continue;

                var neighbor = GetRoomAt(rooms, x - 1, y);
                string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                
                var key = (x, y);
                if (!wallMap.ContainsKey(key) || GetPriority(wall) > GetPriority(wallMap[key]))
                    wallMap[key] = wall;
            }

            // Правая стена (без углов)
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
            {
                int x = room.X + room.Width - 1;
                if (doorPositions.Contains((x, y))) continue;

                var neighbor = GetRoomAt(rooms, x + 1, y);
                string wall = neighbor != null ? BestWall(room.WallProto, neighbor.WallProto) : room.WallProto;
                
                var key = (x, y);
                if (!wallMap.ContainsKey(key) || GetPriority(wall) > GetPriority(wallMap[key]))
                    wallMap[key] = wall;
            }
        }

        // Группируем стены по прототипу
        var wallsByProto = new Dictionary<string, List<(int x, int y)>>();
        foreach (var kvp in wallMap)
        {
            if (!wallsByProto.ContainsKey(kvp.Value))
                wallsByProto[kvp.Value] = new List<(int x, int y)>();
            wallsByProto[kvp.Value].Add(kvp.Key);
        }

        // Выводим стены сгруппированными
        foreach (var group in wallsByProto)
        {
            sb.AppendLine($"- proto: {group.Key}");
            sb.AppendLine("  entities:");
            
            foreach (var (x, y) in group.Value)
            {
                int invY = -y;
                float posX = x + 0.5f;
                float posY = invY + 0.5f;

                string posXStr = posX.ToString("0.0").Replace(',', '.');
                string posYStr = posY.ToString("0.0").Replace(',', '.');

                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");
                sb.AppendLine($"      pos: {posXStr},{posYStr}");
                sb.AppendLine($"      parent: 2");
                uid++;
            }
        }

        return uid;
    }

    private void GenerateDoors(StringBuilder sb, List<Room> rooms, ref int uid)
    {
        var doorsByProto = new Dictionary<string, List<(int x, int y)>>();
        
        foreach (var room in rooms)
        {
            foreach (var door in room.Doors)
            {
                if (!doorsByProto.ContainsKey(door.Proto))
                    doorsByProto[door.Proto] = new List<(int x, int y)>();
                
                doorsByProto[door.Proto].Add((door.X, door.Y));
            }
        }
        
        foreach (var kvp in doorsByProto)
        {
            if (kvp.Value.Count == 0) continue;
            
            sb.AppendLine($"- proto: {kvp.Key}");
            sb.AppendLine("  entities:");
            
            foreach (var (x, y) in kvp.Value)
            {
                int invY = -y;
                float posX = x + 0.5f;
                float posY = invY + 0.5f;
                
                string posXStr = posX.ToString("0.0").Replace(',', '.');
                string posYStr = posY.ToString("0.0").Replace(',', '.');
                
                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");
                sb.AppendLine($"      pos: {posXStr},{posYStr}");
                sb.AppendLine($"      parent: 2");
                uid++;
            }
        }
    }
}