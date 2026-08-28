// Services/YAMLLoader.cs
using MapperIce.Models;
using System.Globalization;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MapperIce.Services;

public class YAMLLoader
{
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
            var grid = new Grid { Uid = gridId, Name = $"Грид {gridId}" };
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

        // Ищем Grid entity (uid: 2) и парсим chunks
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
                if (!entity.TryGetValue("uid", out var uidObj)) continue;
                int uid = Convert.ToInt32(uidObj);

                if (uid != 2) continue; // Только Grid entity

                // Ищем MapGrid компонент
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                var components = (List<object>)compsObj;

                foreach (var compObj in components)
                {
                    var comp = (Dictionary<object, object>)compObj;
                    if (!comp.TryGetValue("type", out var typeObj)) continue;
                    if (typeObj.ToString() != "MapGrid") continue;

                    // Парсим chunks
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

                // Пропускаем Map Entity (uid: 1) и Grid (uid: 2)
                if (entity.TryGetValue("uid", out var uidObj))
                {
                    int uid = Convert.ToInt32(uidObj);
                    if (uid == 1 || uid == 2) continue;
                }

                // Парсим Transform
                if (!entity.TryGetValue("components", out var compsObj)) continue;
                var components = (List<object>)compsObj;

                float posX = 0, posY = 0;
                float rotation = 0;
                int parent = 2;

                foreach (var compObj in components)
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

                // Конвертируем координаты (игровые → редакторские)
                float gridX = posX;
                float gridY = -posY;

                // Определяем тип сущности по прототипу
                if (proto.Contains("Pipe"))
                {
                    string pipeType = proto.Contains("Alt2") ? "Distra" :
                                     proto.Contains("Alt1") ? "Waste" : "Normal";
                    grid.Entities.Add(new PipeEntity
                    {
                        X = gridX,
                        Y = gridY,
                        PipeType = pipeType,
                        IsEndpoint = false
                    });
                }
                else if (proto.Contains("Firelock"))
                {
                    grid.Entities.Add(new FirelockEntity
                    {
                        X = gridX,
                        Y = gridY,
                        Proto = proto,
                        IsGlass = proto.Contains("Glass")
                    });
                }
                else if (proto == "AirAlarm" || proto.Contains("AirAlarm"))
                {
                    grid.Entities.Add(new AirAlarmEntity
                    {
                        X = gridX,
                        Y = gridY,
                        Rotation = rotation
                    });
                }
                else if (proto == "FireAlarm" || proto.Contains("FireAlarm"))
                {
                    grid.Entities.Add(new FireAlarmEntity
                    {
                        X = gridX,
                        Y = gridY,
                        Rotation = rotation
                    });
                }
                else if (proto.Contains("GasVent"))
                {
                    string pipeType = "Normal";
                    foreach (var compObj in components)
                    {
                        var comp = (Dictionary<object, object>)compObj;
                        if (comp.TryGetValue("type", out var typeObj) && typeObj.ToString() == "AtmosPipeLayers")
                        {
                            if (comp.TryGetValue("pipeLayer", out var layerObj))
                            {
                                string layer = layerObj.ToString()!;
                                pipeType = layer == "Tertiary" ? "Distra" :
                                          layer == "Secondary" ? "Waste" : "Normal";
                            }
                        }
                    }

                    grid.Entities.Add(new PipeEntity
                    {
                        X = gridX,
                        Y = gridY,
                        PipeType = pipeType,
                        IsEndpoint = true
                    });
                }
                else
                {
                    grid.Entities.Add(new MapEntity
                    {
                        Proto = proto,
                        X = gridX,
                        Y = gridY,
                        Rotation = rotation,
                        ParentGridUid = parent
                    });
                }
            }
        }
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
                if (!entity.TryGetValue("uid", out var uidObj)) continue;
                int uid = Convert.ToInt32(uidObj);
                if (uid != 2) continue;

                if (!entity.TryGetValue("components", out var compsObj)) continue;
                var components = (List<object>)compsObj;

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