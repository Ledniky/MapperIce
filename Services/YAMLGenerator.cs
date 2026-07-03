// Services/YAMLGenerator.cs
using MapperIce.Models;
using System.Text;

namespace MapperIce.Services;

public static class YAMLGenerator
{
    private const int CHUNK_SIZE = 16;

    public static string Generate(Grid grid, TileBuilder tileBuilder, Dictionary<string, PipeSettings>? pipeLayers, Dictionary<string, AlarmSettings> alarmSettings)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (tileBuilder == null)
            throw new ArgumentNullException(nameof(tileBuilder));

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

        sb.AppendLine("  - uid: 2");
        sb.AppendLine("    components:");
        sb.AppendLine("    - type: MetaData");
        sb.AppendLine("      name: grid");
        sb.AppendLine("    - type: Transform");
        sb.AppendLine("      pos: 0,0");
        sb.AppendLine("      parent: 1");
        sb.AppendLine("    - type: MapGrid");
        sb.AppendLine("      chunks:");

        var chunks = GenerateChunksFromTileGrid(tileGrid);
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

        int uid = 3;
        GenerateWallsFromTileGrid(sb, tileGrid, tileBuilder, ref uid);
        GenerateDoorsFromTileGrid(sb, tileGrid, ref uid);
        GeneratePipesFromEntities(sb, grid, ref uid, pipeLayers);
        GenerateAlarms(sb, grid, ref uid, alarmSettings);

        return sb.ToString();
    }

    private static int CountPipes(Grid grid)
    {
        return grid.Entities.OfType<PipeEntity>().Count();
    }

    private static Dictionary<(int x, int y), string> GenerateChunksFromTileGrid(TileGrid tileGrid)
    {
        var chunks = new Dictionary<(int x, int y), int[]>();

        var floorTiles = tileGrid.GetTilesByContent(TileContent.Floor).ToList();
        var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall).ToList();
        var allTiles = floorTiles.Concat(wallTiles);

        foreach (var tile in allTiles)
        {
            int x = tile.X;
            int y = -tile.Y;

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
            chunks[key][index] = 1;
        }

        var result = new Dictionary<(int x, int y), string>();
        foreach (var kvp in chunks)
        {
            result[kvp.Key] = EncodeTiles(kvp.Value);
        }

        return result;
    }

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

    private static int CountEntities(TileGrid tileGrid)
    {
        int count = 2;
        count += tileGrid.GetTilesByContent(TileContent.Wall).Count();
        count += tileGrid.GetTilesByContent(TileContent.Door).Count();
        return count;
    }

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

    private static void GenerateAlarms(StringBuilder sb, Grid grid, ref int uid, Dictionary<string, AlarmSettings> alarmSettings)
    {
        foreach (var entity in grid.Entities)
        {
            if (entity is AirAlarmEntity airAlarm)
            {
                string protoId = alarmSettings.TryGetValue("AirAlarm", out var settings) ? settings.Id : "AirAlarm";
                GenerateAlarmEntity(sb, protoId, airAlarm.X, airAlarm.Y, airAlarm.Rotation, ref uid);
            }
            else if (entity is FireAlarmEntity fireAlarm)
            {
                string protoId = alarmSettings.TryGetValue("FireAlarm", out var settings) ? settings.Id : "FireAlarm";
                GenerateAlarmEntity(sb, protoId, fireAlarm.X, fireAlarm.Y, fireAlarm.Rotation, ref uid);
            }
        }
    }
private static void GenerateAlarmEntity(StringBuilder sb, string protoId, float x, float y, float rotation, ref int uid)
{
    float posX = x + 0.5f;
    float posY = -y + 0.5f;
    sb.AppendLine($"- proto: {protoId}");
    sb.AppendLine("  entities:");
    sb.AppendLine($"  - uid: {uid}");
    sb.AppendLine($"    components:");
    sb.AppendLine($"    - type: Transform");
    if (rotation != 0)
    {
        string rotStr = rotation.ToString("0.000000000000000").Replace(',', '.');
        sb.AppendLine($"      rot: {rotStr} rad");
    }
    sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
    sb.AppendLine($"      parent: 2");
    sb.AppendLine($"    - type: Fixtures");
    sb.AppendLine($"      fixtures: {{}}");
    uid++;
}


    private static void GeneratePipesFromEntities(
        StringBuilder sb,
        Grid grid,
        ref int uid,
        Dictionary<string, PipeSettings>? pipeLayers)
    {
        if (grid == null) return;

        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        if (pipes.Count == 0) return;

        var grouped = pipes.GroupBy(p => p.PipeType);

        foreach (var group in grouped)
        {
            var pipeList = group.ToList();

            string suffix = group.Key switch
            {
                "Distra" => "Alt2",
                "Waste" => "Alt1",
                "Normal" => "",
                _ => ""
            };

            bool hasColor = pipeLayers != null &&
                            pipeLayers.TryGetValue(group.Key, out var settings) &&
                            settings.HasColor;
            string hexColor = hasColor ? GetPipeHexColor(pipeLayers, group.Key) : "";

            // Находим концы труб (с 1 соседом)
            var endpoints = new List<PipeEntity>();
            foreach (var pipe in pipeList)
            {
                int neighbors = GetNeighbors(pipeList, (int)pipe.X, (int)pipe.Y).Count;
                if (neighbors == 1)
                {
                    endpoints.Add(pipe);
                }
            }

            // Сначала генерируем все трубы, кроме концов
            foreach (var pipe in pipeList)
            {
                if (endpoints.Contains(pipe)) continue;

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

                if (hasColor)
                {
                    sb.AppendLine($"    - type: AtmosPipeColor");
                    sb.AppendLine($"      color: '{hexColor}'");
                }

                uid++;
            }

            // Генерируем вентиляции/скрубберы на концах (вместо труб)
            foreach (var endpoint in endpoints)
            {
                string ventProto;
                string pipeLayer;

                if (group.Key == "Distra")
                {
                    ventProto = "GasVentPump";
                    pipeLayer = "Tertiary";
                }
                else if (group.Key == "Waste")
                {
                    ventProto = "GasVentScrubber";
                    pipeLayer = "Secondary";
                }
                else
                {
                    continue;
                }

                float posX = endpoint.X + 0.5f;
                float posY = -endpoint.Y + 0.5f;

                string posXStr = posX.ToString("0.0").Replace(',', '.');
                string posYStr = posY.ToString("0.0").Replace(',', '.');

                var neighbors = GetNeighbors(pipeList, (int)endpoint.X, (int)endpoint.Y);
                float ventRotation = 0;

                if (neighbors.Count > 0)
                {
                    var (dx, dy) = neighbors[0];
                    if (dx == 1) ventRotation = (float)Math.PI;
                    else if (dx == -1) ventRotation = 0;
                    else if (dy == 1) ventRotation = (float)(-Math.PI / 2);
                    else if (dy == -1) ventRotation = (float)(Math.PI / 2);
                }

                sb.AppendLine($"- proto: {ventProto}");
                sb.AppendLine("  entities:");

                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");

                if (ventRotation != 0)
                {
                    string rotStr = ventRotation.ToString("0.000000000000000").Replace(',', '.');
                    sb.AppendLine($"      rot: {rotStr} rad");
                }

                sb.AppendLine($"      pos: {posXStr},{posYStr}");
                sb.AppendLine($"      parent: 2");
                sb.AppendLine($"    - type: AtmosPipeLayers");
                sb.AppendLine($"      pipeLayer: {pipeLayer}");

                if (hasColor)
                {
                    sb.AppendLine($"    - type: AtmosPipeColor");
                    sb.AppendLine($"      color: '{hexColor}'");
                }

                uid++;
            }
        }
    }

    private static string GetPipeHexColor(Dictionary<string, PipeSettings>? pipeLayers, string layer)
    {
        if (pipeLayers != null && pipeLayers.TryGetValue(layer, out var settings))
        {
            return settings.HexColor;
        }

        return layer switch
        {
            "Distra" => "#0055CCFF",
            "Waste" => "#990000FF",
            "Normal" => "#FFFFFFFF",
            _ => "#FFFFFFFF"
        };
    }

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

        return $"GasPipe{type}{suffix}";
    }

    private static bool IsStraight(List<(int dx, int dy)> neighbors)
    {
        if (neighbors.Count != 2) return false;
        var (dx1, dy1) = neighbors[0];
        var (dx2, dy2) = neighbors[1];
        return (dx1 == -dx2 && dy1 == -dy2) || (dx1 == dx2 && dy1 == dy2);
    }

    private static float GetPipeRotation(List<(int dx, int dy)> neighbors)
    {
        if (neighbors.Count == 0) return 0;

        if (neighbors.Count == 1)
        {
            var (dx, dy) = neighbors[0];
            if (dx != 0) return (float)(Math.PI / 2);
            if (dy != 0) return 0;
        }

        if (neighbors.Count == 2)
        {
            var (dx1, dy1) = neighbors[0];
            var (dx2, dy2) = neighbors[1];

            if ((dx1 == -1 && dx2 == 1) || (dx1 == 1 && dx2 == -1))
            {
                return (float)(Math.PI / 2);
            }
            else if ((dy1 == -1 && dy2 == 1) || (dy1 == 1 && dy2 == -1))
            {
                return 0;
            }
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

        return 0;
    }
}