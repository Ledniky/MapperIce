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
    public static string Generate(Grid grid, TileBuilder tileBuilder, Dictionary<string, PipeSettings> pipeLayers)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (tileBuilder == null)
            throw new ArgumentNullException(nameof(tileBuilder));

        // Строим TileGrid из комнат
        var tileGrid = tileBuilder.BuildFromRooms(grid);

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
        GeneratePipesFromEntities(sb, grid, ref uid, pipeLayers);

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

// В GeneratePipesFromEntities - добавляем цвет ТОЛЬКО для Distra и Waste

private static void GeneratePipesFromEntities(
    StringBuilder sb,
    Grid grid,
    ref int uid,
    Dictionary<string, PipeSettings> pipeLayers)
{
    if (grid == null) return;

    var pipes = grid.Entities.OfType<PipeEntity>().ToList();
    if (pipes.Count == 0) return;

    var grouped = pipes.GroupBy(p => p.PipeType);

    foreach (var group in grouped)
    {
        var pipeList = group.ToList();

        // Определяем суффикс для слоя
        string suffix = group.Key switch
        {
            "Distra" => "Alt2",
            "Waste" => "Alt1",
            "Normal" => "",
            _ => ""
        };

        // Получаем цвет из настроек или используем цвета по умолчанию
        string hexColor = GetPipeHexColor(pipeLayers, group.Key);
        bool hasColor = group.Key != "Normal"; // Normal не имеет цвета

        foreach (var pipe in pipeList)
        {
            int pipeX = (int)pipe.X;
            int pipeY = (int)pipe.Y;

            var neighbors = GetNeighbors(pipeList, pipeX, pipeY);
            string protoType = GetPipeProto(suffix, neighbors);
            float rotation = GetPipeRotation(neighbors);

            sb.AppendLine($"- proto: {protoType}");
            sb.AppendLine("  entities:");

            float posX = pipe.X + 0.5f;
            float posY = -pipe.Y + 0.5f;

            string posXStr = posX.ToString("0.0").Replace(',', '.');
            string posYStr = posY.ToString("0.0").Replace(',', '.');

            sb.AppendLine($"  - uid: {uid}");
            sb.AppendLine($"    components:");
            sb.AppendLine($"    - type: Transform");

            if (rotation != 0)
            {
                string rotStr = rotation.ToString("0.000000000000000").Replace(',', '.');
                sb.AppendLine($"      rot: {rotStr} rad");
            }

            sb.AppendLine($"      pos: {posXStr},{posYStr}");
            sb.AppendLine($"      parent: 2");

            // Добавляем цвет ТОЛЬКО для Distra и Waste
            if (hasColor)
            {
                sb.AppendLine($"    - type: AtmosPipeColor");
                sb.AppendLine($"      color: '{hexColor}'");
            }

            uid++;
        }
    }
}
    /// <summary>
    /// Получает Hex цвет для слоя трубы
    /// </summary>
    private static string GetPipeHexColor(Dictionary<string, PipeSettings> pipeLayers, string layer)
    {
        // Если есть настройки - берём цвет из них
        if (pipeLayers != null && pipeLayers.TryGetValue(layer, out var settings))
        {
            return settings.HexColor;
        }

        // Цвета по умолчанию
        return layer switch
        {
            "Distra" => "#0055CCFF",
            "Waste" => "#990000FF",
            "Normal" => "#FFFFFFFF",
            _ => "#FFFFFFFF"
        };
    }

    /// <summary>
    /// Получает соседей для трубы
    /// </summary>
    private static List<(int dx, int dy)> GetNeighbors(List<PipeEntity> pipes, int x, int y)
    {
        var neighbors = new List<(int dx, int dy)>();
        var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

        foreach (var (dx, dy) in directions)
        {
            if (pipes.Any(p => (int)p.X == x + dx && (int)p.Y == y + dy))
            {
                neighbors.Add((dx, dy));
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Определяет прототип трубы по соседям
    /// </summary>
    private static string GetPipeProto(string suffix, List<(int dx, int dy)> neighbors)
    {
        int count = neighbors.Count;

        string type = count switch
        {
            0 or 1 => "Straight",
            2 when IsStraight(neighbors) => "Straight",
            2 => "Bend",
            3 => "TJunction",
            _ => "Fourway"
        };

        // Distra: suffix = ""  → GasPipeStraight
        // Waste: suffix = "Alt1" → GasPipeStraightAlt1
        // Normal: suffix = "Alt2" → GasPipeStraightAlt2
        return $"GasPipe{type}{suffix}";
    }

    private static bool IsStraight(List<(int dx, int dy)> neighbors)
    {
        if (neighbors.Count != 2) return false;
        var (dx1, dy1) = neighbors[0];
        var (dx2, dy2) = neighbors[1];
        return (dx1 == -dx2 && dy1 == -dy2) || (dx1 == dx2 && dy1 == dy2);
    }

    /// <summary>
    /// Вычисляет ротацию для трубы
    /// </summary>
    private static float GetPipeRotation(List<(int dx, int dy)> neighbors)
    {
        if (neighbors.Count == 0) return 0;

        // Для одного соседа - прямая
        if (neighbors.Count == 1)
        {
            var (dx, dy) = neighbors[0];
            if (dx != 0) return (float)(Math.PI / 2);
            if (dy != 0) return 0;
        }

        // Для прямой трубы (2 соседа)
        if (neighbors.Count == 2)
        {
            var (dx1, dy1) = neighbors[0];
            var (dx2, dy2) = neighbors[1];

            // Горизонтальная прямая
            if ((dx1 == -1 && dx2 == 1) || (dx1 == 1 && dx2 == -1))
            {
                return (float)(Math.PI / 2);
            }
            // Вертикальная прямая
            else if ((dy1 == -1 && dy2 == 1) || (dy1 == 1 && dy2 == -1))
            {
                return 0;
            }
            // Угол (поворот)
            else
            {
                bool hasUp = neighbors.Any(n => n.dy == -1);
                bool hasDown = neighbors.Any(n => n.dy == 1);
                bool hasLeft = neighbors.Any(n => n.dx == -1);
                bool hasRight = neighbors.Any(n => n.dx == 1);

                if (hasRight && hasUp) return (float)Math.PI;
                if (hasRight && hasDown) return (float)(Math.PI / 2);
                if (hasLeft && hasUp) return (float)(-Math.PI / 2);
                if (hasLeft && hasDown) return 0;
            }
        }

        // Для тройника (3 соседа)
        if (neighbors.Count == 3)
        {
            bool hasUp = neighbors.Any(n => n.dy == -1);
            bool hasDown = neighbors.Any(n => n.dy == 1);
            bool hasLeft = neighbors.Any(n => n.dx == -1);
            bool hasRight = neighbors.Any(n => n.dx == 1);

            if (!hasUp) return 0;
            if (!hasDown) return (float)Math.PI;
            if (!hasLeft) return (float)(Math.PI / 2);
            if (!hasRight) return (float)(-Math.PI / 2);
        }

        // Крестовина - без ротации
        if (neighbors.Count >= 4)
        {
            return 0;
        }

        return 0;
    }
}