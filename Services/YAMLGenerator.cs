using MapperIce.Models;
using System.Text;

namespace MapperIce.Services;

public class YAMLGenerator
{
    private const int CHUNK_SIZE = 16;

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

        // Генерируем стены и двери
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
                    // ИНВЕРТИРУЕМ Y (как и в стенах)
                    int invY = -y;

                    int cx = x / CHUNK_SIZE;
                    int cy = invY / CHUNK_SIZE;
                    int lx = x % CHUNK_SIZE;
                    int ly = invY % CHUNK_SIZE;

                    // Для отрицательных координат корректируем
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
        int count = 2; // Map Entity и Grid
        foreach (var room in rooms)
        {
            // Стены
            count += room.Width * 2 + room.Height * 2 - 4;
            // Полы (тайлы)
            count += (room.Width - 2) * (room.Height - 2);
            // Двери
            count += room.Doors.Count;
        }
        return count;
    }

    private int GenerateWalls(StringBuilder sb, List<Room> rooms)
    {
        int uid = 3;
        var entities = new List<string>();
        var wallPositions = new HashSet<(int x, int y)>();

        foreach (var room in rooms)
        {
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int y = room.Y; y < room.Y + room.Height; y++)
                {
                    if (x == room.X || x == room.X + room.Width - 1 ||
                        y == room.Y || y == room.Y + room.Height - 1)
                    {
                        wallPositions.Add((x, y));
                    }
                }
            }
        }

        foreach (var (x, y) in wallPositions)
        {
            // Используем ТЕ ЖЕ координаты, что и для тайлов
            int invY = -y;

            // Стены должны быть в центре тайла
            float posX = x + 0.5f;
            float posY = invY + 0.5f;

            string posXStr = posX.ToString("0.0").Replace(',', '.');
            string posYStr = posY.ToString("0.0").Replace(',', '.');

            entities.Add($"  - uid: {uid}");
            entities.Add($"    components:");
            entities.Add($"    - type: Transform");
            entities.Add($"      pos: {posXStr},{posYStr}");
            entities.Add($"      parent: 2");
            uid++;
        }

        if (entities.Count > 0)
        {
            sb.AppendLine("- proto: WallSolid");
            sb.AppendLine("  entities:");
            foreach (var line in entities)
                sb.AppendLine(line);
        }
        
        return uid;
    }

    private void GenerateDoors(StringBuilder sb, List<Room> rooms, ref int uid)
    {
        // Собираем все двери по прототипам
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
        
        // Генерируем YAML для каждой группы дверей
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