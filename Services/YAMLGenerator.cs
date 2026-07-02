// Services/YAMLGenerator.cs
using MapperIce.Models;
using System.Text;

namespace MapperIce.Services;

public static class YAMLGenerator
{
    private const int CHUNK_SIZE = 16;

    /// <summary>
    /// Генерирует YAML карту из грида
    /// </summary>
    public static string Generate(Grid grid, TileBuilder tileBuilder)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (tileBuilder == null)
            throw new ArgumentNullException(nameof(tileBuilder));

        // Строим TileGrid из комнат
        var tileGrid = tileBuilder.BuildFromRooms(grid);
        var rooms = grid.Rooms;

        var sb = new StringBuilder();

        // ==================== META ====================
        sb.AppendLine("meta:");
        sb.AppendLine("  format: 7");
        sb.AppendLine("  category: Map");
        sb.AppendLine("  engineVersion: 266.0.0");
        sb.AppendLine("  forkId: \"\"");
        sb.AppendLine("  forkVersion: \"\"");
        sb.AppendLine($"  time: {DateTime.Now:MM/dd/yyyy HH:mm:ss}");
        sb.AppendLine($"  entityCount: {CountEntities(tileGrid) + CountPipes(grid)}");

        // ==================== MAPS & GRIDS ====================
        sb.AppendLine("maps:");
        sb.AppendLine("- 1");
        sb.AppendLine("grids:");
        sb.AppendLine("- 2");

        sb.AppendLine("orphans: []");
        sb.AppendLine("nullspace: []");

        // ==================== TILEMAP ====================
        sb.AppendLine("tilemap:");
        sb.AppendLine("  0: Space");
        sb.AppendLine("  1: Plating");

        // ==================== ENTITIES ====================
        sb.AppendLine("entities:");

        // Map Entity
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

        // Grid Entity
        sb.AppendLine("  - uid: 2");
        sb.AppendLine("    components:");
        sb.AppendLine("    - type: MetaData");
        sb.AppendLine("      name: grid");
        sb.AppendLine("    - type: Transform");
        sb.AppendLine("      pos: 0,0");
        sb.AppendLine("      parent: 1");
        sb.AppendLine("    - type: MapGrid");
        sb.AppendLine("      chunks:");

        // ==================== CHUNKS ====================
        var chunks = GenerateChunksFromTileGrid(tileGrid);
        foreach (var chunk in chunks)
        {
            sb.AppendLine($"        {chunk.Key.x},{chunk.Key.y}:");
            sb.AppendLine($"          ind: {chunk.Key.x},{chunk.Key.y}");
            sb.AppendLine($"          tiles: {chunk.Value}");
            sb.AppendLine($"          version: 7");
        }

        // ==================== GRID COMPONENTS ====================
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

        // ==================== WALLS ====================
        int uid = 3;
        GenerateWallsFromTileGrid(sb, tileGrid, tileBuilder, ref uid);

        // ==================== DOORS ====================
        GenerateDoorsFromTileGrid(sb, tileGrid, ref uid);

        // ==================== PIPES ====================
        GeneratePipesFromEntities(sb, grid, ref uid);

        return sb.ToString();
    }

    /// <summary>
    /// Подсчитывает количество труб
    /// </summary>
    private static int CountPipes(Grid grid)
    {
        return grid.Entities.OfType<PipeEntity>().Count();
    }

    /// <summary>
    /// Генерирует чанки из TileGrid
    /// </summary>
    private static Dictionary<(int x, int y), string> GenerateChunksFromTileGrid(TileGrid tileGrid)
    {
        var chunks = new Dictionary<(int x, int y), int[]>();

        // Полы и стены дают тайлы
        var floorTiles = tileGrid.GetTilesByContent(TileContent.Floor).ToList();
        var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall).ToList();
        var allTiles = floorTiles.Concat(wallTiles);

        foreach (var tile in allTiles)
        {
            int x = tile.X;
            int y = -tile.Y; // Инвертируем Y как в SS14

            int cx = x / CHUNK_SIZE;
            int cy = y / CHUNK_SIZE;
            int lx = x % CHUNK_SIZE;
            int ly = y % CHUNK_SIZE;

            if (ly < 0)
            {
                ly += CHUNK_SIZE;
                cy--;
            }

            var key = (cx, cy);
            if (!chunks.ContainsKey(key))
            {
                var tiles = new int[CHUNK_SIZE * CHUNK_SIZE];
                chunks[key] = tiles;
            }

            int index = ly * CHUNK_SIZE + lx;
            chunks[key][index] = 1; // Plating
        }

        var result = new Dictionary<(int x, int y), string>();
        foreach (var kvp in chunks)
        {
            result[kvp.Key] = EncodeTiles(kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Кодирует тайлы в Base64
    /// </summary>
    private static string EncodeTiles(int[] tileIds)
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

    /// <summary>
    /// Подсчитывает количество сущностей
    /// </summary>
    private static int CountEntities(TileGrid tileGrid)
    {
        int count = 2; // Map Entity + Grid
        count += tileGrid.GetTilesByContent(TileContent.Wall).Count();
        count += tileGrid.GetTilesByContent(TileContent.Door).Count();
        return count;
    }

    /// <summary>
    /// Генерирует стены из TileGrid
    /// </summary>
    private static void GenerateWallsFromTileGrid(
        StringBuilder sb,
        TileGrid tileGrid,
        TileBuilder tileBuilder,
        ref int uid)
    {
        var wallsByProto = new Dictionary<string, List<(int x, int y)>>();

        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            string bestWall = tileBuilder.GetBestWallAt(tileGrid, tile.X, tile.Y);

            if (!wallsByProto.ContainsKey(bestWall))
                wallsByProto[bestWall] = new List<(int x, int y)>();

            wallsByProto[bestWall].Add((tile.X, tile.Y));
        }

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
    }

    /// <summary>
    /// Генерирует двери из TileGrid
    /// </summary>
    private static void GenerateDoorsFromTileGrid(
        StringBuilder sb,
        TileGrid tileGrid,
        ref int uid)
    {
        var doorsByProto = new Dictionary<string, List<(int x, int y)>>();

        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            if (string.IsNullOrEmpty(tile.ProtoId)) continue;

            if (!doorsByProto.ContainsKey(tile.ProtoId))
                doorsByProto[tile.ProtoId] = new List<(int x, int y)>();

            doorsByProto[tile.ProtoId].Add((tile.X, tile.Y));
        }

        foreach (var group in doorsByProto)
        {
            if (group.Value.Count == 0) continue;

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
    }

    /// <summary>
    /// Генерирует трубы из сущностей грида
    /// </summary>
    private static void GeneratePipesFromEntities(StringBuilder sb, Grid grid, ref int uid)
    {
        if (grid == null) return;

        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        if (pipes.Count == 0) return;

        // Группируем трубы по типу
        var grouped = pipes.GroupBy(p => p.PipeType);

        foreach (var group in grouped)
        {
            // Определяем прототип для каждого типа
            // Distra -> GasPipeStraight (слой Secondary)
            // Normal -> GasPipeStraightAlt2 (слой Tertiary)  
            // Waste -> GasPipeStraightAlt1 (слой Waste)
            string proto = group.Key switch
            {
                "Distra" => "GasPipeStraight",
                "Normal" => "GasPipeStraightAlt2",
                "Waste" => "GasPipeStraightAlt1",
                _ => "GasPipeStraight"
            };

            sb.AppendLine($"- proto: {proto}");
            sb.AppendLine("  entities:");

            foreach (var pipe in group)
            {
                float invY = -pipe.Y;
                float posX = pipe.X + 0.5f;
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