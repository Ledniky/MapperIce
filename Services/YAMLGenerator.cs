// Services/YAMLGenerator.cs
using MapperIce.Models;
using System.Text;

namespace MapperIce.Services;

public static class YAMLGenerator
{
    private const int CHUNK_SIZE = 16;

    public static string Generate(
        Grid grid,
        TileBuilder tileBuilder,
        Dictionary<string, PipeSettings>? pipeLayers,
        Dictionary<string, AlarmSettings> alarmSettings)
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
        sb.AppendLine($"  time: {DateTime.Now:MM.dd.yyyy HH:mm:ss}");
        sb.AppendLine($"  entityCount: {CalculateEntityCount(grid, tileGrid)}");

        // ==================== MAPS & GRIDS ====================
        sb.AppendLine("maps:");
        sb.AppendLine("- 1");
        sb.AppendLine("grids:");
        sb.AppendLine("- 2");
        sb.AppendLine("orphans: []");
        sb.AppendLine("nullspace: []");

  // ==================== TILEMAP (динамический — каждый реально используемый тип пола получает свой ID) ====================
var floorProtoIds = tileGrid.GetTilesByContent(TileContent.Floor)
    .Select(t => string.IsNullOrEmpty(t.ProtoId) ? "Plating" : t.ProtoId)
    .Distinct()
    .ToList();

if (!floorProtoIds.Contains("Plating"))
    floorProtoIds.Add("Plating"); // стены и двери всегда лежат на Plating

floorProtoIds.Sort(StringComparer.Ordinal);

var tileIdMap = new Dictionary<string, int>();
int nextTileId = 1; // 0 зарезервирован под Space
foreach (var proto in floorProtoIds)
{
    tileIdMap[proto] = nextTileId++;
}

sb.AppendLine("tilemap:");
sb.AppendLine("  0: Space");
foreach (var kvp in tileIdMap.OrderBy(k => k.Value))
{
    sb.AppendLine($"  {kvp.Value}: {kvp.Key}");
}

        // ==================== ENTITIES ====================
        sb.AppendLine("entities:");

        // === MAP ENTITY ===
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

        // === GRID ENTITY ===
        sb.AppendLine("  - uid: 2");
        sb.AppendLine("    components:");
        sb.AppendLine("    - type: MetaData");
        sb.AppendLine("      name: grid");
        sb.AppendLine("    - type: Transform");
        sb.AppendLine("      pos: 0,0");
        sb.AppendLine("      parent: 1");
        sb.AppendLine("    - type: MapGrid");
        sb.AppendLine("      chunks:");

        var chunks = GenerateChunksFromTileGrid(tileGrid, tileIdMap);
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

        // Храним маппинг позиция -> UID для устройств
        var positionToUid = new Dictionary<(int x, int y), int>();

        // === ГЕНЕРИРУЕМ ВСЕ ОБЪЕКТЫ С ГРУППИРОВКОЙ ===

        // 1. Стены (группируем по прототипу)
        GenerateWallsGrouped(sb, tileGrid, tileBuilder, ref uid);

        // 2. Двери (группируем по прототипу)
        GenerateDoorsGrouped(sb, tileGrid, ref uid);

        // 3. Трубы и вентиляции
        GeneratePipesGrouped(sb, grid, ref uid, pipeLayers, positionToUid);

        // 4. Пожарные шлюзы
        GenerateFirelocksGrouped(sb, grid, ref uid, positionToUid);

        // 5. Сигнализации
        GenerateAlarmsGrouped(sb, grid, ref uid, alarmSettings, positionToUid);

        // 6. Прочие сущности
        GenerateGenericEntitiesGrouped(sb, grid, ref uid);

        return sb.ToString();
    }

    #region Группированная генерация

    private static void GenerateWallsGrouped(
        StringBuilder sb,
        TileGrid tileGrid,
        TileBuilder tileBuilder,
        ref int uid)
    {
        var wallGroups = new Dictionary<string, List<(int x, int y)>>();

        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Wall))
        {
            string bestWall = tileBuilder.GetBestWallAt(tileGrid, tile.X, tile.Y);

            if (!wallGroups.ContainsKey(bestWall))
                wallGroups[bestWall] = new List<(int x, int y)>();

            wallGroups[bestWall].Add((tile.X, tile.Y));
        }

        foreach (var group in wallGroups)
        {
            if (group.Value.Count == 0) continue;

            sb.AppendLine($"- proto: {group.Key}");
            sb.AppendLine("  entities:");

            foreach (var (x, y) in group.Value)
            {
                int invY = -y;
                float posX = x + 0.5f;
                float posY = invY + 0.5f;

                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");
                sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
                sb.AppendLine($"      parent: 2");
                uid++;
            }
        }
    }

    private static void GenerateDoorsGrouped(
        StringBuilder sb,
        TileGrid tileGrid,
        ref int uid)
    {
        var doorGroups = new Dictionary<string, List<(int x, int y)>>();

        foreach (var tile in tileGrid.GetTilesByContent(TileContent.Door))
        {
            if (string.IsNullOrEmpty(tile.ProtoId)) continue;

            if (!doorGroups.ContainsKey(tile.ProtoId))
                doorGroups[tile.ProtoId] = new List<(int x, int y)>();

            doorGroups[tile.ProtoId].Add((tile.X, tile.Y));
        }

        foreach (var group in doorGroups)
        {
            if (group.Value.Count == 0) continue;

            sb.AppendLine($"- proto: {group.Key}");
            sb.AppendLine("  entities:");

            foreach (var (x, y) in group.Value)
            {
                int invY = -y;
                float posX = x + 0.5f;
                float posY = invY + 0.5f;

                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");
                sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
                sb.AppendLine($"      parent: 2");
                uid++;
            }
        }
    }

    private static void GeneratePipesGrouped(
        StringBuilder sb,
        Grid grid,
        ref int uid,
        Dictionary<string, PipeSettings>? pipeLayers,
        Dictionary<(int x, int y), int> positionToUid)
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

            var endpoints = new List<PipeEntity>();
            foreach (var pipe in pipeList)
            {
                int neighbors = GetNeighbors(pipeList, (int)pipe.X, (int)pipe.Y).Count;
                if (neighbors == 1)
                {
                    endpoints.Add(pipe);
                }
            }

            // Группируем трубы по прототипу
            var pipeProtos = new Dictionary<string, List<PipeEntity>>();

            foreach (var pipe in pipeList)
            {
                if (endpoints.Contains(pipe)) continue;

                int pipeX = (int)pipe.X;
                int pipeY = (int)pipe.Y;
                var neighbors = GetNeighbors(pipeList, pipeX, pipeY);
                string protoType = GetPipeProto(suffix, neighbors);
                float rotation = GetPipeRotation(neighbors);

                string key = $"{protoType}_{rotation}";
                if (!pipeProtos.ContainsKey(key))
                    pipeProtos[key] = new List<PipeEntity>();

                pipeProtos[key].Add(pipe);
            }

            // Генерируем трубы по группам
            foreach (var protoGroup in pipeProtos)
            {
                string protoName = protoGroup.Key.Split('_')[0];
                float rotation = float.Parse(protoGroup.Key.Split('_')[1]);

                sb.AppendLine($"- proto: {protoName}");
                sb.AppendLine("  entities:");

                foreach (var pipe in protoGroup.Value)
                {
                    float posX = pipe.X + 0.5f;
                    float posY = -pipe.Y + 0.5f;

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

                    if (hasColor)
                    {
                        sb.AppendLine($"    - type: AtmosPipeColor");
                        sb.AppendLine($"      color: '{hexColor}'");
                    }

                    uid++;
                }
            }

            // Генерируем вентиляции
            if (endpoints.Count > 0)
            {
                string ventProto = group.Key == "Distra" ? "GasVentPump" : "GasVentScrubber";
                string pipeLayer = group.Key == "Distra" ? "Tertiary" : "Secondary";

                sb.AppendLine($"- proto: {ventProto}");
                sb.AppendLine("  entities:");

                foreach (var endpoint in endpoints)
                {
                    int ventUid = uid;

                    var key = ((int)endpoint.X, (int)endpoint.Y);
                    positionToUid[key] = ventUid;

                    float posX = endpoint.X + 0.5f;
                    float posY = -endpoint.Y + 0.5f;

                    var neighbors = GetNeighbors(pipeList, (int)endpoint.X, (int)endpoint.Y);
                    float ventRotation = 0;

                    if (neighbors.Count > 0)
                    {
                        var (dx, dy) = neighbors[0];
                        if (dx == 1) ventRotation = (float)(Math.PI / 2);
                        else if (dx == -1) ventRotation = (float)(-Math.PI / 2);
                        else if (dy == 1) ventRotation = 0;
                        else if (dy == -1) ventRotation = (float)Math.PI;
                    }

                    sb.AppendLine($"  - uid: {ventUid}");
                    sb.AppendLine($"    components:");
                    sb.AppendLine($"    - type: Transform");

                    if (ventRotation != 0)
                    {
                        string rotStr = ventRotation.ToString("0.000000000000000").Replace(',', '.');
                        sb.AppendLine($"      rot: {rotStr} rad");
                    }

                    sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
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
    }

    private static void GenerateFirelocksGrouped(
        StringBuilder sb,
        Grid grid,
        ref int uid,
        Dictionary<(int x, int y), int> positionToUid)
    {
        var firelocks = grid.Entities.OfType<FirelockEntity>().ToList();
        if (firelocks.Count == 0) return;

        // Группируем по прототипу
        var groups = firelocks.GroupBy(f => f.Proto);

        foreach (var group in groups)
        {
            sb.AppendLine($"- proto: {group.Key}");
            sb.AppendLine("  entities:");

            foreach (var firelock in group)
            {
                int currentUid = uid;
                var key = ((int)firelock.X, (int)firelock.Y);
                positionToUid[key] = currentUid;

                float posX = firelock.X + 0.5f;
                float posY = -firelock.Y + 0.5f;

                sb.AppendLine($"  - uid: {currentUid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");
                sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
                sb.AppendLine($"      parent: 2");

                uid++;
            }
        }
    }

private static void GenerateAlarmsGrouped(
    StringBuilder sb,
    Grid grid,
    ref int uid,
    Dictionary<string, AlarmSettings> alarmSettings,
    Dictionary<(int x, int y), int> positionToUid)
{
    var airAlarms = grid.Entities.OfType<AirAlarmEntity>().ToList();
    var fireAlarms = grid.Entities.OfType<FireAlarmEntity>().ToList();

    // Генерируем воздушные сигнализации
    if (airAlarms.Count > 0)
    {
        string protoId = alarmSettings.TryGetValue("AirAlarm", out var settings) ? settings.Id : "AirAlarm";

        sb.AppendLine($"- proto: {protoId}");
        sb.AppendLine("  entities:");

        foreach (var alarm in airAlarms)
        {
            int alarmUid = uid;

            float posX = alarm.X + 0.5f;
            float posY = -alarm.Y + 0.5f;

            sb.AppendLine($"  - uid: {alarmUid}");
            sb.AppendLine($"    components:");
            sb.AppendLine($"    - type: Transform");

            if (alarm.Rotation != 0)
            {
                string rotStr = alarm.Rotation.ToString("0.000000000000000").Replace(',', '.');
                sb.AppendLine($"      rot: {rotStr} rad");
            }

            sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
            sb.AppendLine($"      parent: 2");

            // Находим устройства для привязки (по комнате)
            var linkedDevices = FindDevicesForAlarm(grid, (int)alarm.X, (int)alarm.Y, alarm.Rotation, positionToUid);

            if (linkedDevices.Count > 0)
            {
                sb.AppendLine($"    - type: DeviceList");
                sb.AppendLine($"      devices:");
                foreach (var deviceUid in linkedDevices.Distinct())
                {
                    sb.AppendLine($"      - {deviceUid}");
                }
            }

            sb.AppendLine($"    - type: Fixtures");
            sb.AppendLine($"      fixtures: {{}}");

            uid++;
        }
    }

    // Генерируем пожарные сигнализации
    if (fireAlarms.Count > 0)
    {
        string protoId = alarmSettings.TryGetValue("FireAlarm", out var settings) ? settings.Id : "FireAlarm";

        sb.AppendLine($"- proto: {protoId}");
        sb.AppendLine("  entities:");

        foreach (var alarm in fireAlarms)
        {
            int alarmUid = uid;

            float posX = alarm.X + 0.5f;
            float posY = -alarm.Y + 0.5f;

            sb.AppendLine($"  - uid: {alarmUid}");
            sb.AppendLine($"    components:");
            sb.AppendLine($"    - type: Transform");

            if (alarm.Rotation != 0)
            {
                string rotStr = alarm.Rotation.ToString("0.000000000000000").Replace(',', '.');
                sb.AppendLine($"      rot: {rotStr} rad");
            }

            sb.AppendLine($"      pos: {posX.ToString("0.0").Replace(',', '.')},{posY.ToString("0.0").Replace(',', '.')}");
            sb.AppendLine($"      parent: 2");

            // Находим устройства для привязки
            var linkedDevices = FindDevicesForAlarm(grid, (int)alarm.X, (int)alarm.Y, alarm.Rotation, positionToUid);

            if (linkedDevices.Count > 0)
            {
                sb.AppendLine($"    - type: DeviceList");
                sb.AppendLine($"      devices:");
                foreach (var deviceUid in linkedDevices.Distinct())
                {
                    sb.AppendLine($"      - {deviceUid}");
                }
            }

            sb.AppendLine($"    - type: Fixtures");
            sb.AppendLine($"      fixtures: {{}}");

            uid++;
        }
    }
}

private static List<int> FindDevicesForAlarm(
    Grid grid,
    int x,
    int y,
    float rotation,
    Dictionary<(int x, int y), int> positionToUid)
{
    var result = new List<int>();

    // Находим комнату, куда направлена сигнализация
    int dx = 0, dy = 0;
    float normalized = rotation % (float)(2 * Math.PI);
    if (normalized < 0) normalized += (float)(2 * Math.PI);

    if (Math.Abs(normalized) < 0.1f || Math.Abs(normalized - (float)(2 * Math.PI)) < 0.1f)
        dy = 1;
    else if (Math.Abs(normalized - (float)(Math.PI / 2)) < 0.1f)
        dx = -1;
    else if (Math.Abs(normalized - (float)Math.PI) < 0.1f)
        dy = -1;
    else if (Math.Abs(normalized - (float)(3 * Math.PI / 2)) < 0.1f)
        dx = 1;

    // Проверяем комнату в направлении сигнализации
    int targetX = x + dx;
    int targetY = y + dy;

    Room? room = grid.Rooms.FirstOrDefault(r =>
        targetX >= r.X && targetX < r.X + r.Width &&
        targetY >= r.Y && targetY < r.Y + r.Height);

    if (room == null)
    {
        targetX = x + dx * 2;
        targetY = y + dy * 2;
        room = grid.Rooms.FirstOrDefault(r =>
            targetX >= r.X && targetX < r.X + r.Width &&
            targetY >= r.Y && targetY < r.Y + r.Height);
    }

    if (room == null) return result;

    // Ищем все устройства в этой комнате
    foreach (var kvp in positionToUid)
    {
        var pos = kvp.Key;
        if (pos.x >= room.X && pos.x < room.X + room.Width &&
            pos.y >= room.Y && pos.y < room.Y + room.Height)
        {
            result.Add(kvp.Value);
        }
    }

    return result;
}


    
    
    
    #endregion

    // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

    private static int CalculateEntityCount(Grid grid, TileGrid tileGrid)
    {
        int count = 2; // Map Entity + Grid
        count += tileGrid.GetTilesByContent(TileContent.Wall).Count();
        count += tileGrid.GetTilesByContent(TileContent.Door).Count();

        // Считаем только концы труб (вентиляции)
        var pipes = grid.Entities.OfType<PipeEntity>().ToList();
        foreach (var group in pipes.GroupBy(p => p.PipeType))
        {
            var pipeList = group.ToList();
            foreach (var pipe in pipeList)
            {
                int neighbors = GetNeighbors(pipeList, (int)pipe.X, (int)pipe.Y).Count;
                if (neighbors == 1)
                {
                    count++; // Вентиляция
                }
                else
                {
                    count++; // Труба
                }
            }
        }

        count += grid.Entities.OfType<FirelockEntity>().Count();
        count += grid.Entities.OfType<AirAlarmEntity>().Count();
        count += grid.Entities.OfType<FireAlarmEntity>().Count();
        count += grid.Entities
        .Where(e => e is not PipeEntity && e is not FirelockEntity &&
                    e is not AirAlarmEntity && e is not FireAlarmEntity)
        .Count(e => !string.IsNullOrEmpty(e.Proto));
        return count;
    }

private static Dictionary<(int x, int y), string> GenerateChunksFromTileGrid(TileGrid tileGrid, Dictionary<string, int> tileIdMap)
{
    var chunks = new Dictionary<(int x, int y), int[]>();
    int platingId = tileIdMap["Plating"];

    var floorTiles = tileGrid.GetTilesByContent(TileContent.Floor).ToList();
    var wallTiles = tileGrid.GetTilesByContent(TileContent.Wall).ToList();
    var doorTiles = tileGrid.GetTilesByContent(TileContent.Door).ToList();

    void PlaceTile(TileData tile, int tileId)
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
            chunks[key] = new int[CHUNK_SIZE * CHUNK_SIZE];
        }

        int index = ly * CHUNK_SIZE + lx;
        chunks[key][index] = tileId;
    }

    foreach (var tile in floorTiles)
    {
        string proto = string.IsNullOrEmpty(tile.ProtoId) ? "Plating" : tile.ProtoId;
        int tileId = tileIdMap.TryGetValue(proto, out var id) ? id : platingId;
        PlaceTile(tile, tileId);
    }

    foreach (var tile in wallTiles) PlaceTile(tile, platingId);
    foreach (var tile in doorTiles) PlaceTile(tile, platingId);

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
    private static void GenerateGenericEntitiesGrouped(StringBuilder sb, Grid grid, ref int uid)
    {
        var generic = grid.Entities
            .Where(e => e is not PipeEntity && e is not FirelockEntity &&
                        e is not AirAlarmEntity && e is not FireAlarmEntity)
            .Where(e => !string.IsNullOrEmpty(e.Proto))
            .ToList();

        if (generic.Count == 0) return;

        foreach (var group in generic.GroupBy(e => e.Proto))
        {
            sb.AppendLine($"- proto: {group.Key}");
            sb.AppendLine("  entities:");

            foreach (var entity in group)
            {
                float posX = entity.X;
                float posY = -entity.Y + 1.0f;

                sb.AppendLine($"  - uid: {uid}");
                sb.AppendLine($"    components:");
                sb.AppendLine($"    - type: Transform");

                if (entity.Rotation != 0)
                {
                    string rotStr = entity.Rotation.ToString("0.000000000000000").Replace(',', '.');
                    sb.AppendLine($"      rot: {rotStr} rad");
                }

                sb.AppendLine($"      pos: {posX.ToString("0.000000").Replace(',', '.')},{posY.ToString("0.000000").Replace(',', '.')}");
                sb.AppendLine($"      parent: 2");
                uid++;
            }
        }
    }
}