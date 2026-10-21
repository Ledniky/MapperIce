// Services/YAMLLoader.cs
using MapperIce.Models;
using System.Globalization;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MapperIce.Services;

public class YAMLLoader
{
    private readonly PrototypeIndexer? _indexer;
    private readonly DrawDepthManager _drawDepthManager = new();

    public YAMLLoader() { }
    public YAMLLoader(PrototypeIndexer indexer) => _indexer = indexer;
    public YAMLLoader(PrototypeIndexer indexer, DrawDepthManager drawDepthManager)
    {
        _indexer = indexer;
        _drawDepthManager = drawDepthManager;
    }

    public MapData LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        
        // Удаляем все !type: теги (они не нужны для парсинга структуры)
        yaml = System.Text.RegularExpressions.Regex.Replace(
            yaml, 
            @"!type:\S+", 
            "");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // Десериализуем в object и проверяем, что корень — объект (mapping).
        // Раньше тут был жёсткий Deserialize<Dictionary<object, object>>, который
        // бросал InvalidCastException ("Specified cast is not valid"), если корень
        // YAML оказался не mapping'ом (например, одиночным списком или sequence),
        // и ронял всю загрузку карты разом.
        var root = deserializer.Deserialize<object?>(yaml);
        if (root is not Dictionary<object, object> doc)
        {
            throw new InvalidDataException(
                "Корень YAML-файла не является объектом (mapping). Возможно, файл повреждён или это не игровая карта.");
        }
        var map = new MapData();

        // Создаём один слой для всей карты — grids: содержит список всех grid-сущностей,
        // а не отдельные слои. ParseTilemap/ParseEntities/ParseDecals парсят данные
        // из всех grid-сущностей и кладут в один переданный Grid.
        var grid = new Grid { Uid = 1, Name = "Слой 1" };
        map.AddGrid(grid);

        // Определяем, статичный ли это грид (станция) или динамичный (шаттл)
        grid.IsStaticGrid = DetectStaticGrid(doc);

        ParseTilemap(doc, grid);
        ParseEntities(doc, grid);
        ParseDecals(doc, grid);

        return map;
    }

    private bool DetectStaticGrid(Dictionary<object, object> doc)
    {
        // Статичный грид определяется по наличию BecomesStation или MapAtmosphere
        // в любой сущности. Динамичный — по наличию MapGrid с Physics/Shuttle.
        if (!doc.TryGetValue("entities", out var entitiesObj)) return false;
        if (entitiesObj is not List<object> entitiesList) return false;

        foreach (var entityGroup in entitiesList)
        {
            if (entityGroup is not Dictionary<object, object> group) continue;
            if (!group.TryGetValue("entities", out var entities)) continue;
            if (entities is not List<object> entityList) continue;

            foreach (var entityObj in entityList)
            {
                if (entityObj is not Dictionary<object, object> entity) continue;
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                if (compsObj is not List<object> components) continue;

                foreach (var compObj in components)
                {
                    if (compObj is not Dictionary<object, object> comp) continue;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    var typeStr = typeObj.ToString();
                    if (typeStr == "BecomesStation") return true;
                    if (typeStr == "MapAtmosphere") return true;
                }
            }
        }
        return false;
    }

    private void ParseTilemap(Dictionary<object, object> doc, Grid grid)
    {
        if (!doc.TryGetValue("tilemap", out var tilemapObj)) return;

        if (tilemapObj is not Dictionary<object, object> tilemap) return;
        var tileIdMap = new Dictionary<int, string>();

        foreach (var kvp in tilemap)
        {
            int id = Convert.ToInt32(kvp.Key);
            string proto = kvp.Value.ToString()!;
            tileIdMap[id] = proto;
        }

        // ID "пустоты" НЕ гарантированно равен 0 — в реальных игровых картах tilemap
        // собирается динамически (каждый использованный тип пола получает свой ID
        // по мере встречи), поэтому Space может оказаться под любым номером.
        // Ищем реальный ID по имени прототипа, а не полагаемся на то, что это всегда 0.
        int spaceId = tileIdMap.FirstOrDefault(kvp => kvp.Value == "Space").Key;
        bool hasSpace = tileIdMap.ContainsValue("Space");

        if (!doc.TryGetValue("entities", out var entitiesObj)) return;
        if (entitiesObj is not List<object> entitiesList) return;

        foreach (var entityGroup in entitiesList)
        {
            if (entityGroup is not Dictionary<object, object> group) continue;
            if (!group.ContainsKey("entities")) continue;
            if (group["entities"] is not List<object> entities) continue;
            foreach (var entityObj in entities)
            {
                if (entityObj is not Dictionary<object, object> entity) continue;

                // Grid entity определяется по наличию компонента MapGrid, а не по uid
                // (uid может быть 1, 2 или другим в зависимости от экспорта)
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                if (compsObj is not List<object> components) continue;

                foreach (var compObj in components)
                {
                    if (compObj is not Dictionary<object, object> comp) continue;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() != "MapGrid") continue;

                    // Парсим чанки
                    if (!comp.TryGetValue("chunks", out var chunksObj)) continue;
                    if (chunksObj is not Dictionary<object, object> chunks) continue;

                    foreach (var chunkKvp in chunks)
                    {
                        var chunkKey = chunkKvp.Key.ToString()!.Split(',');
                        if (chunkKey.Length < 2) continue;
                        int chunkX = int.Parse(chunkKey[0], CultureInfo.InvariantCulture);
                        int chunkY = int.Parse(chunkKey[1], CultureInfo.InvariantCulture);

                        if (chunkKvp.Value is not Dictionary<object, object> chunkData) continue;
                        if (!chunkData.TryGetValue("tiles", out var tilesBase64)) continue;

                        var tileIds = DecodeTiles(tilesBase64.ToString()!);

                        // Сохраняем тайлы в Grid
                        for (int i = 0; i < 256; i++)
                        {
                            int tileId = tileIds[i];
                            if (hasSpace && tileId == spaceId) continue; // реальный ID пустоты, а не жёстко 0

                            int localX = i % 16;
                            int localY = i / 16;
                            int worldX = chunkX * 16 + localX;
                            int worldY = -(chunkY * 16 + localY); // Инвертируем Y

                            if (tileIdMap.TryGetValue(tileId, out var proto))
                            {
                                grid.Tiles.Add(new PlacedTile
                                {
                                    X = worldX,
                                    Y = worldY,
                                    Proto = proto
                                });
                            }
                        }
                    }
                }
            }
        }
    }

    private void ParseEntities(Dictionary<object, object> doc, Grid grid)
    {
        if (!doc.TryGetValue("entities", out var entitiesObj)) return;
        if (entitiesObj is not List<object> entitiesList) return;

        foreach (var entityGroup in entitiesList)
        {
            if (entityGroup is not Dictionary<object, object> group) continue;
            if (!group.TryGetValue("proto", out var protoObj)) continue;
            string proto = protoObj.ToString()!;

            if (!group.TryGetValue("entities", out var entities)) continue;
            if (entities is not List<object> entityList) continue;

            foreach (var entityObj in entityList)
            {
                if (entityObj is not Dictionary<object, object> entity) continue;

                // Пропускаем Map Entity и Grid entity — они не являются игровыми сущностями
                // Map entity определяется по наличию компонента Map (или BecomesStation в
                // некоторых экспортах), Grid entity — по наличию MapGrid компонента.
                // UID не надёжен: в map.yml Grid entity имеет uid=2, в Zmap.yml — uid=1.
                bool isMapOrGridEntity = false;
                if (entity.TryGetValue("components", out var compsObj))
                {
                    if (compsObj is List<object> components)
                    {
                        foreach (var compObj in components)
                        {
                            if (compObj is not Dictionary<object, object> comp) continue;
                            if (!comp.TryGetValue("type", out var typeObj)) continue;
                            var typeStr = typeObj.ToString();
                            // Grid entity
                            if (typeStr == "MapGrid") { isMapOrGridEntity = true; break; }
                            // Map entity (стандартный формат)
                            if (typeStr == "Map") { isMapOrGridEntity = true; break; }
                            // Map entity в некоторых экспортах (Zmap.yml)
                            if (typeStr == "BecomesStation") { isMapOrGridEntity = true; break; }
                        }
                    }
                }
                if (isMapOrGridEntity) continue;

                // Парсим Transform
                if (!entity.TryGetValue("components", out compsObj)) continue;
                if (compsObj is not List<object> transformComponents) continue;

                float posX = 0, posY = 0;
                float rotation = 0;
                int parent = 2;
                int pipeLayer = 0;

                foreach (var compObj in transformComponents)
                {
                    if (compObj is not Dictionary<object, object> comp) continue;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() == "Transform")
                    {
                        if (comp.TryGetValue("pos", out var posObj))
                        {
                            var pos = posObj.ToString()!.Split(',');
                            if (pos.Length >= 2)
                            {
                                posX = float.Parse(pos[0].Trim(), CultureInfo.InvariantCulture);
                                posY = float.Parse(pos[1].Trim(), CultureInfo.InvariantCulture);
                            }
                        }
                        if (comp.TryGetValue("rot", out var rotObj))
                        {
                            string rotStr;
                            if (rotObj is float f)
                            {
                                rotStr = f.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (rotObj is double d)
                            {
                                rotStr = d.ToString(CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                rotStr = rotObj.ToString()!.Replace(" rad", "").Trim();
                            }
                            rotation = float.Parse(rotStr, CultureInfo.InvariantCulture);
                        }
                        if (comp.TryGetValue("parent", out var parentObj))
                        {
                            parent = Convert.ToInt32(parentObj);
                        }
                    }
                    else if (typeObj.ToString() == "AtmosPipeLayers")
                    {
                        // Слой трубы машины (термомашины и т.п.): Primary=0, Secondary=1, Tertiary=2.
                        if (comp.TryGetValue("pipeLayer", out var plObj))
                        {
                            var pl = plObj.ToString();
                            pipeLayer = pl == "Tertiary" ? 2 : pl == "Secondary" ? 1 : 0;
                        }
                    }
                }

                // Конвертируем координаты (игровые → редакторские). ВАЖНО: у разных типов
                // сущностей РАЗНАЯ внутренняя система координат (см. YAMLGenerator), поэтому
                // и обратное преобразование должно быть разным — единая формула
                // "gridY = -posY" (как было раньше) не была верной инверсией НИ для одного
                // из типов и давала системный сдвиг ровно на 1 тайл.

                // Пожарные шлюзы и сигнализации хранят X,Y как ЦЕЛЫЙ индекс тайла —
                // при экспорте к ним прибавляется +0.5 (GenerateFirelocksGrouped и т.п.),
                // здесь эту половину тайла вычитаем обратно.
                float structuralX = posX - 0.5f;
                float structuralY = -posY + 0.5f;

                // Обычные ("generic") размещённые сущности хранят X,Y как ДРОБНЫЙ центр
                // тайла — при экспорте используется формула "posY = -entity.Y + 1.0f"
                // (GenerateGenericEntitiesGrouped), которая самообратна (f(f(v)) == v),
                // поэтому обратное преобразование — та же самая формула.
                float genericX = posX;
                float genericY = -posY + 1.0f;

                // Определяем тип сущности по прототипу.
                // Трубы (Pipe) и вентиляции (GasVent) больше НЕ превращаются во внутреннюю
                // абстракцию PipeEntity (точки/линии сети без реальной текстуры) — они
                // проваливаются в общую ветку ниже и появляются как обычные сущности из
                // репозитория, с настоящим прототипом и спрайтом, как и любой другой
                // размещённый объект. PipeBuilder/сеть труб остаётся рабочей для того, что
                // пользователь рисует инструментом "Труба" внутри самого редактора — сюда
                // это не относится, тут только импорт готовой игровой карты.
                if (proto.Contains("Firelock"))
                {
                    var firelock = new FirelockEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Proto = proto,
                        IsGlass = proto.Contains("Glass")
                    };
                    if (_indexer != null)
                    {
                        var drawDepthName = _indexer.GetDrawDepth(proto);
                        if (!string.IsNullOrEmpty(drawDepthName))
                        {
                            firelock.DrawDepthOffset = _drawDepthManager.GetOffset(drawDepthName);
                        }
                    }
                    grid.Entities.Add(firelock);
                }
                else if (proto == "AirAlarm" || proto.Contains("AirAlarm"))
                {
                    var airAlarm = new AirAlarmEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Rotation = rotation
                    };
                    if (_indexer != null)
                    {
                        var drawDepthName = _indexer.GetDrawDepth(proto);
                        if (!string.IsNullOrEmpty(drawDepthName))
                        {
                            airAlarm.DrawDepthOffset = _drawDepthManager.GetOffset(drawDepthName);
                        }
                    }
                    grid.Entities.Add(airAlarm);
                }
                else if (proto == "FireAlarm" || proto.Contains("FireAlarm"))
                {
                    var fireAlarm = new FireAlarmEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Rotation = rotation
                    };
                    if (_indexer != null)
                    {
                        var drawDepthName = _indexer.GetDrawDepth(proto);
                        if (!string.IsNullOrEmpty(drawDepthName))
                        {
                            fireAlarm.DrawDepthOffset = _drawDepthManager.GetOffset(drawDepthName);
                        }
                    }
                    grid.Entities.Add(fireAlarm);
                }
                else
                {
                    var mapEntity = new MapEntity
                    {
                        Proto = proto,
                        X = genericX,
                        Y = genericY,
                        Rotation = rotation,
                        PipeLayer = pipeLayer,
                        ParentGridUid = parent
                    };

                    // Разрешаем drawdepth из прототипа (с рекурсивным поиском по родителям)
                    if (_indexer != null)
                    {
                        var drawDepthName = _indexer.GetDrawDepth(proto);
                        if (!string.IsNullOrEmpty(drawDepthName))
                        {
                            mapEntity.DrawDepthOffset = _drawDepthManager.GetOffset(drawDepthName);
                        }
                    }

                    grid.Entities.Add(mapEntity);
                }
            }
        }
    }

    private void ParseDecals(Dictionary<object, object> doc, Grid grid)
    {
        if (!doc.TryGetValue("entities", out var entitiesObj)) return;
        if (entitiesObj is not List<object> entitiesList) return;

        foreach (var entityGroup in entitiesList)
        {
            if (entityGroup is not Dictionary<object, object> group) continue;
            if (!group.ContainsKey("entities")) continue;
            if (group["entities"] is not List<object> entities) continue;
            foreach (var entityObj in entities)
            {
                if (entityObj is not Dictionary<object, object> entity) continue;

                // Grid entity определяется по наличию компонента DecalGrid, а не по uid
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                if (compsObj is not List<object> components) continue;

                bool hasDecalGrid = false;
                foreach (var compObj in components)
                {
                    if (compObj is not Dictionary<object, object> comp) continue;
                    if (comp.TryGetValue("type", out var typeObj) && typeObj.ToString() == "DecalGrid")
                    {
                        hasDecalGrid = true;
                        break;
                    }
                }
                if (!hasDecalGrid) continue;

                // Перебираем компоненты снова для парсинга
                foreach (var compObj in components)
                {
                    if (compObj is not Dictionary<object, object> comp) continue;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() != "DecalGrid") continue;

                    if (!comp.TryGetValue("chunkCollection", out var chunkColl)) continue;
                    if (chunkColl is not Dictionary<object, object> collection) continue;

                    if (!collection.TryGetValue("nodes", out var nodesObj)) continue;
                    if (nodesObj is not List<object> nodes) continue;

                    foreach (var nodeObj in nodes)
                    {
                        if (nodeObj is not Dictionary<object, object> node) continue;
                        if (!node.TryGetValue("node", out var nodeDataObj)) continue;
                        if (nodeDataObj is not Dictionary<object, object> nodeData) continue;

                        string proto = nodeData.TryGetValue("id", out var idObj) ? idObj.ToString()! : "decal";
                        string color = nodeData.TryGetValue("color", out var colorObj) ? colorObj.ToString()! : "#FFFFFFFF";
                        float rotation = nodeData.TryGetValue("angle", out var angleObj) ?
                            float.Parse(angleObj.ToString()!.Replace(" rad", ""), CultureInfo.InvariantCulture) : 0;
                        bool cleanable = nodeData.TryGetValue("cleanable", out _);

                        if (!node.TryGetValue("decals", out var decalsObj)) continue;
                        if (decalsObj is not Dictionary<object, object> decals) continue;

                        foreach (var decalKvp in decals)
                        {
                            var pos = decalKvp.Value.ToString()!.Split(',');
                            if (pos.Length < 2) continue;
                            float x = float.Parse(pos[0].Trim(), CultureInfo.InvariantCulture);
                            float y = float.Parse(pos[1].Trim(), CultureInfo.InvariantCulture);

                            float gridX = x + 0.5f;
                            float gridY = -y + 0.5f;

                            grid.Decals.Add(new PlacedDecal
                            {
                                X = gridX,
                                Y = gridY,
                                Proto = proto,
                                Color = color,
                                Rotation = -rotation,
                                Cleanable = cleanable
                            });
                        }
                    }
                }
            }
        }
    }

    // Реальный формат тайла в чанке — 7 байт: 4 байта TypeId (int32) + 3 служебных
    // байта (flags/variant/padding). Это тот же формат, что пишет YAMLGenerator.EncodeTiles —
    // раньше тут ошибочно читали с шагом 4 байта, из-за чего после первого же тайла
    // чтение расходилось с реальными границами тайлов и на карте вместо сплошного
    // прямоугольника получалась "плетёнка"/шахматка.
    private const int TILE_BYTE_SIZE = 7;

    private int[] DecodeTiles(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var tiles = new int[256];

        for (int i = 0; i < 256; i++)
        {
            int offset = i * TILE_BYTE_SIZE;
            tiles[i] = BitConverter.ToInt32(bytes, offset);
        }

        return tiles;
    }
}
