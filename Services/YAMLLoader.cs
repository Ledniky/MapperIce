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

    public YAMLLoader() { }
    public YAMLLoader(PrototypeIndexer indexer) => _indexer = indexer;

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

        var doc = deserializer.Deserialize<Dictionary<object, object>>(yaml);
        var map = new MapData();

        if (doc.TryGetValue("grids", out var gridsObj))
        {
            var gridIds = ((List<object>)gridsObj).Select(Convert.ToInt32).ToList();

            foreach (var gridId in gridIds)
            {
                var grid = new Grid { Uid = gridId, Name = $"Слой {gridId}" };
                map.AddGrid(grid);

                ParseTilemap(doc, grid);
                ParseEntities(doc, grid);
                ParseDecals(doc, grid);
            }
        }

        return map;
    }

    private void ParseTilemap(Dictionary<object, object> doc, Grid grid)
    {
        if (!doc.TryGetValue("tilemap", out var tilemapObj)) return;

        var tilemap = (Dictionary<object, object>)tilemapObj;
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
        var entitiesList = (List<object>)entitiesObj;

        foreach (var entityGroup in entitiesList)
        {
            var group = (Dictionary<object, object>)entityGroup;
            if (!group.ContainsKey("entities")) continue;

            var entities = (List<object>)group["entities"];
            foreach (var entityObj in entities)
            {
                var entity = (Dictionary<object, object>)entityObj;

                // Grid entity определяется по наличию компонента MapGrid, а не по uid
                // (uid может быть 1, 2 или другим в зависимости от экспорта)
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                var components = (List<object>)compsObj;

                foreach (var compObj in components)
                {
                    var comp = (Dictionary<object, object>)compObj;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() != "MapGrid") continue;

                    // Парсим чанки
                    if (!comp.TryGetValue("chunks", out var chunksObj)) continue;
                    var chunks = (Dictionary<object, object>)chunksObj;

                    foreach (var chunkKvp in chunks)
                    {
                        var chunkKey = chunkKvp.Key.ToString()!.Split(',');
                        int chunkX = int.Parse(chunkKey[0], CultureInfo.InvariantCulture);
                        int chunkY = int.Parse(chunkKey[1], CultureInfo.InvariantCulture);

                        var chunkData = (Dictionary<object, object>)chunkKvp.Value;
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
        var entitiesList = (List<object>)entitiesObj;

        foreach (var entityGroup in entitiesList)
        {
            var group = (Dictionary<object, object>)entityGroup;
            if (!group.TryGetValue("proto", out var protoObj)) continue;
            string proto = protoObj.ToString()!;

            if (!group.TryGetValue("entities", out var entities)) continue;
            var entityList = (List<object>)entities;

            foreach (var entityObj in entityList)
            {
                var entity = (Dictionary<object, object>)entityObj;

                // Пропускаем Map Entity и Grid entity — они не являются игровыми сущностями
                // Map entity определяется по наличию компонента Map (или BecomesStation в
                // некоторых экспортах), Grid entity — по наличию MapGrid компонента.
                // UID не надёжен: в map.yml Grid entity имеет uid=2, в Zmap.yml — uid=1.
                bool isMapOrGridEntity = false;
                if (entity.TryGetValue("components", out var compsObj))
                {
                    var components = (List<object>)compsObj;
                    foreach (var compObj in components)
                    {
                        var comp = (Dictionary<object, object>)compObj;
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
                if (isMapOrGridEntity) continue;

                // Парсим Transform
                if (!entity.TryGetValue("components", out compsObj)) continue;
                var transformComponents = (List<object>)compsObj;

                float posX = 0, posY = 0;
                float rotation = 0;
                int parent = 2;

                foreach (var compObj in transformComponents)
                {
                    var comp = (Dictionary<object, object>)compObj;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() == "Transform")
                    {
                        if (comp.TryGetValue("pos", out var posObj))
                        {
                            var pos = posObj.ToString()!.Split(',');
                            posX = float.Parse(pos[0].Trim(), CultureInfo.InvariantCulture);
                            posY = float.Parse(pos[1].Trim(), CultureInfo.InvariantCulture);
                        }
                        if (comp.TryGetValue("rot", out var rotObj))
                        {
                            var rotStr = rotObj.ToString()!.Replace(" rad", "");
                            rotation = float.Parse(rotStr, CultureInfo.InvariantCulture);
                        }
                        if (comp.TryGetValue("parent", out var parentObj))
                        {
                            parent = Convert.ToInt32(parentObj);
                        }
                    }
                }

                // Если noRot: true — сбрасываем вращение
                rotation = ResolveRotation(proto, rotation);

                // Конвертируем координаты (игровые → редакторские). ВАЖНО: у разных типов
                // сущностей РАЗНАЯ внутренняя система координат (см. YAMLGenerator), поэтому
                // и обратное преобразование должно быть разным — единая формула
                // "gridY = -posY" (как было раньше) не была верной инверсией НИ для одного
                // из типов и давала системный сдвиг ровно на 1 тайл.

                // Труба/ферлок/сигнализация хранят X,Y как ЦЕЛЫЙ индекс тайла (левый
                // верхний угол клетки) — при экспорте к ним прибавляется +0.5
                // (GeneratePipesGrouped/GenerateFirelocksGrouped/GenerateAlarmsGrouped),
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
                    grid.Entities.Add(new FirelockEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Proto = proto,
                        IsGlass = proto.Contains("Glass")
                    });
                }
                else if (proto == "AirAlarm" || proto.Contains("AirAlarm"))
                {
                    grid.Entities.Add(new AirAlarmEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Rotation = rotation
                    });
                }
                else if (proto == "FireAlarm" || proto.Contains("FireAlarm"))
                {
                    grid.Entities.Add(new FireAlarmEntity
                    {
                        X = structuralX,
                        Y = structuralY,
                        Rotation = rotation
                    });
                }
                else
                {
                    grid.Entities.Add(new MapEntity
                    {
                        Proto = proto,
                        X = genericX,
                        Y = genericY,
                        Rotation = rotation,
                        ParentGridUid = parent
                    });
                }
            }
        }
    }

    /// <summary>
    /// Проверяет, запрещён ли поворот для данного прототипа (noRot: true)
    /// или любого из его родителей.
    /// </summary>
    private float ResolveRotation(string protoId, float rotation)
    {
        if (_indexer == null) return rotation;
        return _indexer.FindPrototypeNoRotate(protoId) ? 0f : rotation;
    }

    private void ParseDecals(Dictionary<object, object> doc, Grid grid)
    {
        if (!doc.TryGetValue("entities", out var entitiesObj)) return;
        var entitiesList = (List<object>)entitiesObj;

        foreach (var entityGroup in entitiesList)
        {
            var group = (Dictionary<object, object>)entityGroup;
            if (!group.ContainsKey("entities")) continue;

            var entities = (List<object>)group["entities"];
            foreach (var entityObj in entities)
            {
                var entity = (Dictionary<object, object>)entityObj;

                // Grid entity определяется по наличию компонента DecalGrid, а не по uid
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                var components = (List<object>)compsObj;

                bool hasDecalGrid = false;
                foreach (var compObj in components)
                {
                    var comp = (Dictionary<object, object>)compObj;
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
                    var comp = (Dictionary<object, object>)compObj;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() != "DecalGrid") continue;

                    if (!comp.TryGetValue("chunkCollection", out var chunkColl)) continue;
                    var collection = (Dictionary<object, object>)chunkColl;

                    if (!collection.TryGetValue("nodes", out var nodesObj)) continue;
                    var nodes = (List<object>)nodesObj;

                    foreach (var nodeObj in nodes)
                    {
                        var node = (Dictionary<object, object>)nodeObj;
                        if (!node.TryGetValue("node", out var nodeDataObj)) continue;
                        var nodeData = (Dictionary<object, object>)nodeDataObj;

                        string proto = nodeData.TryGetValue("id", out var idObj) ? idObj.ToString()! : "decal";
                        string color = nodeData.TryGetValue("color", out var colorObj) ? colorObj.ToString()! : "#FFFFFFFF";
                        float rotation = nodeData.TryGetValue("angle", out var angleObj) ?
                            float.Parse(angleObj.ToString()!.Replace(" rad", ""), CultureInfo.InvariantCulture) : 0;
                        bool cleanable = nodeData.TryGetValue("cleanable", out _);

                        if (!node.TryGetValue("decals", out var decalsObj)) continue;
                        var decals = (Dictionary<object, object>)decalsObj;

                        foreach (var decalKvp in decals)
                        {
                            var pos = decalKvp.Value.ToString()!.Split(',');
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
