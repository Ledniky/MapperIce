using MapperIce.Models;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MapperIce.Services;

public class PrototypeIndexer
{
    private Dictionary<string, Prototype> _prototypes = new();
    private string _currentRepoPath = "";
    private string _currentRepoId = "";
    private string _rootPath = "";
    private Dictionary<string, Palette> _palettes = new();

    // Кэш результатов поиска прототипов: query -> список найденных ID
    private readonly Dictionary<string, List<string>> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxSearchCacheSize = 512;

    public event Action? OnIndexingComplete;
    public string CurrentRepoId => _currentRepoId;

    public string GetRootPath() => _rootPath;

    /// <summary>
    /// Очищает кэш результатов поиска. Вызывается при смене или переиндексации репозитория.
    /// </summary>
    public void ClearSearchCache()
    {
        lock (_searchCache)
        {
            _searchCache.Clear();
        }
    }

    public void IndexRepository(Repository repo)
    {
        _rootPath = repo.Path;
        _currentRepoId = repo.Id;
        _currentRepoPath = repo.Path;

        ClearSearchCache();

        // Сначала пробуем быстро восстановить индекс из кэша на диске,
        // чтобы не пересканировать весь репозиторий заново при каждом запуске
        if (TryLoadCache(repo.Id))
        {
            OnIndexingComplete?.Invoke();
            return;
        }

        // silent: true — это автоматическая попытка при выборе репозитория на старте,
        // а не явное действие пользователя, поэтому без блокирующего MessageBox
        ReindexFromDisk(repo, silent: true);
    }

    /// <summary>
    /// Полное пересканирование репозитория с диска (используется кнопкой "Обновить"
    /// и как fallback, если кэша ещё нет или он повреждён)
    /// </summary>
    public void ReindexFromDisk(Repository repo, bool silent = false)
    {
        string prototypesPath = Path.Combine(repo.Path, "Resources", "Prototypes");

        // Проверяем доступность папки ДО очистки текущих данных — иначе неудачная
        // переиндексация (диск не примонтирован после перезагрузки, путь временно
        // недоступен и т.п.) стирала уже загруженные прототипы, оставляя панель
        // пустой, хотя валидные данные (например, из дискового кэша) уже были в памяти.
        if (!Directory.Exists(prototypesPath))
        {
            System.Diagnostics.Debug.WriteLine($"[Index] Папка Prototypes не найдена: {prototypesPath}");

            // MessageBox блокирует UI-поток модальным окном. При автоматической
            // попытке на старте приложения это недопустимо (даёт эффект зависшего
            // "Поиск..." на панели) — блокирующий диалог оставляем только для
            // явного ручного нажатия кнопки "Обновить" (silent = false по умолчанию).
            if (!silent)
                MessageBox.Show($"Папка Prototypes не найдена: {prototypesPath}");
            return;
        }

        _rootPath = repo.Path;
        _currentRepoId = repo.Id;
        _currentRepoPath = repo.Path;
        _prototypes.Clear();
        _palettes.Clear();
        ClearSearchCache();

        var yamlFiles = Directory.GetFiles(prototypesPath, "*.yml", SearchOption.AllDirectories);
        int count = 0;

        foreach (var file in yamlFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var protos = ParsePrototypes(content, file);
                foreach (var proto in protos)
                {
                    if (!_prototypes.ContainsKey(proto.Id))
                    {
                        _prototypes[proto.Id] = proto;
                        count++;
                    }
                }

                var palettes = ParsePalettes(content);
                foreach (var palette in palettes)
                {
                    if (!string.IsNullOrEmpty(palette.Id))
                        _palettes[palette.Id] = palette;
                }
            }
            catch { }
        }

        ApplyLocalizedNames(repo.Path);
        CheckAllStructures();
        SaveCache(repo.Id);
        OnIndexingComplete?.Invoke();
    }


    /// <summary>
    /// Сканирует Resources/Locale/ru-RU репозитория (*.ftl, рекурсивно) и проставляет
    /// Prototype.LocalizedName для каждой сущности, у которой в локализации есть
    /// запись "ent-{Id} = ...". Значения могут ссылаться друг на друга через Fluent-
    /// синтаксис "{ ent-ДругойId }" (см. пример BarricadeBlock -> Barricade -> BaseBarricade
    /// в barricades.ftl) — такие ссылки резолвятся рекурсивно, аналогично тому, как
    /// FindPathRecursive идёт по цепочке parent:.
    /// </summary>
    private void ApplyLocalizedNames(string repoPath)
    {
        var localeDir = Path.Combine(repoPath, "Resources", "Locale", "ru-RU");
        if (!Directory.Exists(localeDir)) return;

        var rawNames = new Dictionary<string, string>();
        var ftlFiles = Directory.GetFiles(localeDir, "*.ftl", SearchOption.AllDirectories);
        foreach (var file in ftlFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                foreach (var kv in ParseFtlEntityNames(content))
                {
                    if (!rawNames.ContainsKey(kv.Key))
                        rawNames[kv.Key] = kv.Value;
                }
            }
            catch { }
        }

        if (rawNames.Count == 0) return;

        var resolved = new Dictionary<string, string>();
        foreach (var id in rawNames.Keys)
            ResolveFtlValue(id, rawNames, resolved, new HashSet<string>(), 0);

        foreach (var proto in _prototypes.Values)
        {
            if (resolved.TryGetValue(proto.Id, out var localized) && !string.IsNullOrWhiteSpace(localized))
                proto.LocalizedName = localized;
        }
    }

    /// <summary>
    /// Парсит один .ftl-файл и достаёт только "имя" сущности — верхнеуровневую
    /// строку "ent-{Id} = значение". Строки с отступом (например ".desc = ...")
    /// пропускаются: они относятся не к имени, а к атрибутам той же записи.
    /// </summary>
    private Dictionary<string, string> ParseFtlEntityNames(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = content.Replace("\r\n", "\n").Split('\n');

        foreach (var line in lines)
        {
            if (line.Length == 0 || char.IsWhiteSpace(line[0])) continue; // атрибут/продолжение — не имя

            var m = Regex.Match(line, @"^ent-(\S+)\s*=\s*(.*)$");
            if (!m.Success) continue;

            string id = m.Groups[1].Value;
            string value = m.Groups[2].Value.TrimEnd('\r');
            if (!result.ContainsKey(id))
                result[id] = value;
        }

        return result;
    }

    /// <summary>
    /// Рекурсивно резолвит Fluent-ссылки вида "{ ent-ДругойId }" внутри значения
    /// локализации, заменяя их на уже разрешённое имя ссылочной сущности.
    /// visiting защищает от циклических ссылок, depth — от случайной бесконечной
    /// рекурсии при повреждённых данных (тот же паттерн, что FindOffsetRecursive).
    /// </summary>
    private string ResolveFtlValue(string id, Dictionary<string, string> raw,
        Dictionary<string, string> resolved, HashSet<string> visiting, int depth)
    {
        if (resolved.TryGetValue(id, out var cached)) return cached;
        if (!raw.TryGetValue(id, out var value)) return id;
        if (depth > 10 || !visiting.Add(id)) return value;

        string result = Regex.Replace(value, @"\{\s*ent-(\S+?)\s*\}", m =>
            ResolveFtlValue(m.Groups[1].Value, raw, resolved, visiting, depth + 1));

        visiting.Remove(id);
        resolved[id] = result;
        return result;
    }


    private string GetCacheDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MapperIce", "index_cache");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetCachePath(string repoId) => Path.Combine(GetCacheDir(), $"{repoId}.json");

    // Версия формата кэша. Увеличивай на 1 каждый раз, когда меняешь состав полей
    // класса Prototype (добавляешь/удаляешь/переименовываешь свойство) — старые
    // кэши на диске автоматически перестанут подхватываться и пересоберутся с нуля.

    private const int CacheFormatVersion = 18;

    private class CacheEnvelope
    {
        public int Version { get; set; }
        public List<Prototype> Prototypes { get; set; } = new();
        public List<Palette> Palettes { get; set; } = new();
    }

    private void SaveCache(string repoId)
    {
        try
        {
            var envelope = new CacheEnvelope
            {
                Version = CacheFormatVersion,
                Prototypes = _prototypes.Values.ToList(),
                Palettes = _palettes.Values.ToList()
            };

            var json = JsonSerializer.Serialize(envelope);
            var path = GetCachePath(repoId);

            // Та же защита от обрезанного файла при аварийном завершении/перезагрузке,
            // что и в RepositoryManager.Save() — см. комментарий там
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);

            System.Diagnostics.Debug.WriteLine($"[Cache] Сохранён кэш v{CacheFormatVersion}: {path} ({_prototypes.Count} прототипов, {_palettes.Count} палитр)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] ОШИБКА сохранения кэша для repoId={repoId}: {ex}");
        }
    }

    private bool TryLoadCache(string repoId)
    {
        try
        {
            var cachePath = GetCachePath(repoId);
            System.Diagnostics.Debug.WriteLine($"[Cache] Ищу кэш: {cachePath}, exists={File.Exists(cachePath)}");

            if (!File.Exists(cachePath)) return false;

            var json = File.ReadAllText(cachePath);
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(json);

            if (envelope == null || envelope.Version != CacheFormatVersion)
            {
                System.Diagnostics.Debug.WriteLine($"[Cache] Кэш устарел или повреждён (версия {envelope?.Version.ToString() ?? "?"}, ожидалась {CacheFormatVersion}) — пересобираю с диска");
                return false;
            }

            if (envelope.Prototypes == null || envelope.Prototypes.Count == 0) return false;

            _prototypes.Clear();
            foreach (var proto in envelope.Prototypes)
            {
                if (!string.IsNullOrEmpty(proto.Id))
                    _prototypes[proto.Id] = proto;
            }

            _palettes.Clear();
            if (envelope.Palettes != null)
            {
                foreach (var palette in envelope.Palettes)
                {
                    if (!string.IsNullOrEmpty(palette.Id))
                        _palettes[palette.Id] = palette;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Cache] Загружен кэш v{envelope.Version}: {_prototypes.Count} прототипов, {_palettes.Count} палитр");
            ClearSearchCache();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] ОШИБКА загрузки кэша для repoId={repoId}: {ex}");
            return false;
        }
    }



    private List<Prototype> ParsePrototypes(string content, string filePath)
    {
        var result = new List<Prototype>();
        var lines = content.Split('\n');

        string block = "";
        string id = "";
        string type = "";
        bool inBlock = false;

        foreach (var line in lines)
        {
            int indent = line.Length - line.TrimStart().Length;
            var trimmed = line.TrimStart();

            // Проверяем, что это начало прототипа: "- type:" с отступом 0
            if (trimmed.StartsWith("- type:") && indent == 0)
            {
                // Сохраняем предыдущий
                if (inBlock && !string.IsNullOrEmpty(id))
                {
                    var proto = ParseBlock(block, id, type, filePath);
                    if (proto != null) result.Add(proto);
                }

                // Начинаем новый
                block = line + "\n";
                inBlock = true;
                id = "";

                var tMatch = Regex.Match(line, @"- type:\s*(\S+)");
                type = tMatch.Success ? tMatch.Groups[1].Value : "";

                var iMatch = Regex.Match(line, @"id:\s*(\S+)");
                if (iMatch.Success) id = iMatch.Groups[1].Value;
            }
            else if (inBlock)
            {
                // Если встретили "- type:" с отступом 0 — это новый прототип
                if (trimmed.StartsWith("- type:") && indent == 0)
                {
                    // Сохраняем текущий
                    if (!string.IsNullOrEmpty(id))
                    {
                        var proto = ParseBlock(block, id, type, filePath);
                        if (proto != null) result.Add(proto);
                    }

                    // Начинаем новый
                    block = line + "\n";
                    id = "";

                    var tMatch = Regex.Match(line, @"- type:\s*(\S+)");
                    type = tMatch.Success ? tMatch.Groups[1].Value : "";

                    var iMatch = Regex.Match(line, @"id:\s*(\S+)");
                    if (iMatch.Success) id = iMatch.Groups[1].Value;
                }
                else
                {
                    // Добавляем ВСЕ строки с отступом > 0 (компоненты и их содержимое)
                    block += line + "\n";

                    if (string.IsNullOrEmpty(id))
                    {
                        var iMatch = Regex.Match(line, @"id:\s*(\S+)");
                        if (iMatch.Success) id = iMatch.Groups[1].Value;
                    }
                }
            }
        }

        // Последний блок
        if (inBlock && !string.IsNullOrEmpty(id))
        {
            var proto = ParseBlock(block, id, type, filePath);
            if (proto != null) result.Add(proto);
        }

        return result;
    }

    private Prototype? ParseBlock(string block, string id, string type, string filePath)
    {
        if (type != "tile" && type != "entity" && type != "decal") return null;
        if (string.IsNullOrEmpty(id)) return null;
        // Убрано условие !char.IsUpper(id[0]) - теперь принимаем любые id

        var proto = new Prototype { Id = id, FilePath = filePath, Type = type };

        // Ищем parent — поддерживаем:
        // 1. Однострочный: "parent: X"
        // 2. Массив в строке: "parent: [Parent1, Parent2]"
        // 3. Множественный список: "parent:\n  - X\n  - Y"
        // Собираем ВСЕХ родителей для проверки на BaseStructure.

        // Сначала проверяем формат с квадратными скобками: parent: [A, B, C]
        var arrayParentMatch = Regex.Match(block, @"parent:\s*\[\s*([^\]]+)\]");
        if (arrayParentMatch.Success)
        {
            var parents = arrayParentMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parents)
            {
                if (!string.IsNullOrEmpty(p))
                {
                    proto.Parents.Add(p);
                }
            }
            if (proto.Parents.Count > 0)
                proto.Parent = proto.Parents[0];
        }
        else
        {
            // Однострочный формат: parent: X
            var parentMatch = Regex.Match(block, @"parent:[ \t]*([^\s\[\-][^\s\[\]]*)");
            if (parentMatch.Success)
            {
                proto.Parent = parentMatch.Groups[1].Value.Trim();
                proto.Parents.Add(proto.Parent);
            }
            else
            {
                // Списочный формат: parent:\n  - X\n  - Y — парсим все элементы под parent
                var parentSectionMatch = Regex.Match(block, @"parent:\s*\r?\n((?:\s+-\s+[^\s]+\r?\n?)*)");
                if (parentSectionMatch.Success)
                {
                    var parentList = Regex.Matches(parentSectionMatch.Groups[1].Value, @"-\s+([^\s]+)");
                    foreach (Match m in parentList)
                    {
                        string parent = m.Groups[1].Value;
                        if (!string.IsNullOrEmpty(parent))
                        {
                            proto.Parents.Add(parent);
                        }
                    }
                    if (proto.Parents.Count > 0)
                        proto.Parent = proto.Parents[0];
                }
            }
        }

        // Ищем sprite
        var nestedSprite = Regex.Match(block, @"sprite:\s*\r?\n\s*sprite:\s*([^\s]+)");
        var s = nestedSprite.Success
            ? nestedSprite
            : Regex.Match(block, @"sprite:\s*([^\s]+)");

        if (s.Success)
        {
            var path = s.Groups[1].Value.Replace("/", "\\").TrimStart('\\');
            if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(9);
            proto.SpritePath = path;
        }

        // Ищем rsi
        var r = Regex.Match(block, @"rsi:\s*([^\s]+)");
        if (r.Success)
        {
            var path = r.Groups[1].Value.Replace("/", "\\").TrimStart('\\');
            if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(9);
            proto.RsiPath = path;
        }

        // Ищем state
        var stateMatch = Regex.Match(block, @"state:\s*([^\s]+)");
        if (stateMatch.Success)
        {
            proto.State = stateMatch.Groups[1].Value;
        }

        // Ищем все компоненты
        var compMatches = Regex.Matches(block, @"-\s*type:\s*([^\s]+)");
        foreach (Match cm in compMatches)
        {
            string compType = cm.Groups[1].Value;
            if (compType == type) continue;
            proto.Components.Add(compType);
        }

        // Ищем offset у компонента Sprite ("offset: X,Y" — тот же Vector2-формат,
        // что pos/rot у Transform). Значение задано для южной (rotation=0)
        // ориентации спрайта; поворот под текущую facing-direction сущности
        // считается в Renderer.DrawTexturedRect через GetSpriteOffset.
        var offsetMatch = Regex.Match(block, @"offset:\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)");
        if (offsetMatch.Success)
        {
            proto.OffsetX = float.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            proto.OffsetY = float.Parse(offsetMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            proto.HasOffset = true;
        }

        // Ищем drawdepth — обычно задаётся на компоненте Sprite ("drawdepth: WallTops"
        // и т.п.), поэтому ищем по всему блоку без привязки к отступу, как и sprite/rsi/state.
        // Это имя ЗАТЕМ резолвится в числовой offset через DrawDepthManager.GetOffset(name)
        // в Renderer — без этого поля весь per-prototype drawdepth молча игнорировался,
        // и объекты сортировались только по фолбэк-слою контента (FloorTiles/Walls/Doors)
        // + Y, а generic-сущности/декали/огнешлюзы все попадали в один и тот же слой Objects.
        var drawDepthMatch = Regex.Match(block, @"drawdepth:\s*(\S+)");
        if (drawDepthMatch.Success)
        {
            proto.DrawDepth = drawDepthMatch.Groups[1].Value;
        }

        // Многослойность (Sprite.layers) — сначала вычленяем сам подблок компонента
        // Sprite по отступам (а не regex по всему block целиком, как sprite/state выше),
        // потому что порядок и границы элементов списка layers: критичны для отступов
        proto.Layers = ParseSpriteLayers(ExtractComponentBlock(block, "Sprite"));

        return proto;
    }

    /// <summary>
    /// Вычленяет подблок конкретного компонента (например "Sprite") из общего текста
    /// прототипа по фактическим отступам исходного YAML: ищет строку "- type: ИмяКомпонента",
    /// запоминает её отступ, и собирает все последующие строки с БОЛЬШИМ отступом —
    /// то есть до начала следующего компонента (или конца блока). Без этого парсинг
    /// layers: обычным regex по всему block рисковал бы зацепить чужой "state:"/"layers:"
    /// из другого компонента при похожей структуре.
    /// </summary>
    private string ExtractComponentBlock(string block, string componentType)
    {
        var lines = block.Replace("\r\n", "\n").Split('\n');
        int compIndent = -1;
        bool inComponent = false;
        var sb = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            if (inComponent)
            {
                if (indent <= compIndent)
                {
                    inComponent = false;
                }
                else
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
            }

            if (!inComponent && Regex.IsMatch(trimmed, @"^-\s*type:\s*" + Regex.Escape(componentType) + @"(\s|#|$)"))
            {
                compIndent = indent;
                inComponent = true;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Приводит путь текстуры (из "sprite:"/"rsi:" внутри слоя) к тому же виду, что и
    /// проверенная логика для top-level sprite/rsi в ParseBlock: заменяет "/" на "\",
    /// убирает ведущий "\" и опциональный префикс "Textures\".
    /// </summary>
    private string NormalizeTexturePath(string rawPath)
    {
        var path = rawPath.Replace("/", "\\").TrimStart('\\');
        if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(9);
        return path;
    }

    /// <summary>
    /// Парсит YAML-список "layers:" внутри уже вычлененного подблока компонента Sprite
    /// (см. ExtractComponentBlock). Каждый элемент списка ("- state: X" и последующие
    /// вложенные строки того же элемента) превращается в один SpriteLayer.
    /// </summary>
    private List<SpriteLayer> ParseSpriteLayers(string spriteComponentBlock)
    {
        var layers = new List<SpriteLayer>();
        if (string.IsNullOrWhiteSpace(spriteComponentBlock)) return layers;

        var lines = spriteComponentBlock.Replace("\r\n", "\n").Split('\n');

        int layersIndent = -1;
        int i = 0;
        for (; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("layers:"))
            {
                layersIndent = lines[i].Length - trimmed.Length;
                i++;
                break;
            }
        }
        if (layersIndent < 0) return layers; // нет layers: — обычный однослойный прототип

        SpriteLayer? current = null;

        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmedFull = line.TrimStart();
            int indent = line.Length - trimmedFull.Length;

            // ВАЖНО: элементы списка "- map: [...]" в реальных YAML прототипов SS14
            // стоят на ТОМ ЖЕ отступе, что и сама строка "layers:", а не глубже неё
            // (пример: "    layers:\n    - map: [...]\n      state: computer").
            // Раньше здесь стояло "<=" — из-за этого первая же строка списка (отступ
            // == layersIndent) ошибочно трактовалась как конец списка, и layers
            // оставался пустым для ВСЕХ прототипов с таким (обычным) стилем отступов.
            // "<" пропускает строки на том же уровне и обрывает список только на
            // строке с МЕНЬШИМ отступом (следующий ключ/компонент выше по иерархии).
            if (indent < layersIndent) break; // список layers: закончился

            var itemMatch = Regex.Match(trimmedFull, @"^-\s*(.*)$");
            string fieldLine;
            if (itemMatch.Success)
            {
                if (current != null) layers.Add(current);
                current = new SpriteLayer();
                fieldLine = itemMatch.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(fieldLine)) continue;
            }
            else
            {
                fieldLine = trimmedFull;
            }

            if (current == null) continue;

            var stateMatch = Regex.Match(fieldLine, @"^state:\s*(\S+)");
            if (stateMatch.Success) { current.State = stateMatch.Groups[1].Value; continue; }

            var spriteMatch = Regex.Match(fieldLine, @"^sprite:\s*(\S+)");
            if (spriteMatch.Success) { current.SpritePath = NormalizeTexturePath(spriteMatch.Groups[1].Value); continue; }

            var rsiMatch = Regex.Match(fieldLine, @"^rsi:\s*(\S+)");
            if (rsiMatch.Success) { current.RsiPath = NormalizeTexturePath(rsiMatch.Groups[1].Value); continue; }

            var colorMatch = Regex.Match(fieldLine, "^color:\\s*\"?(#[0-9A-Fa-f]{6,8})\"?");
            if (colorMatch.Success) { current.Color = colorMatch.Groups[1].Value; continue; }

            var shaderMatch = Regex.Match(fieldLine, @"^shader:\s*(\S+)");
            if (shaderMatch.Success) { current.Shader = shaderMatch.Groups[1].Value; continue; }

            var visibleMatch = Regex.Match(fieldLine, @"^visible:\s*(true|false)", RegexOptions.IgnoreCase);
            if (visibleMatch.Success) { current.Visible = visibleMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase); continue; }

            var offsetMatch = Regex.Match(fieldLine, @"^offset:\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)");
            if (offsetMatch.Success)
            {
                current.OffsetX = float.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                current.OffsetY = float.Parse(offsetMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                current.HasOffset = true;
                continue;
            }
        }
        if (current != null) layers.Add(current);

        return layers;
    }

    public Prototype? FindPrototype(string id)
    {
        _prototypes.TryGetValue(id, out var proto);
        return proto;
    }

    /// <summary>
    /// Проверяет, является ли прототип структурой: ищем BaseStructure в цепочке
    /// родителей, НО если среди родителей есть BaseStructureDynamic — это не структура.
    /// </summary>
    private bool CheckIsStructure(string id)
    {
        var visited = new HashSet<string>();
        var stack = new List<string> { id };

        while (stack.Count > 0)
        {
            var current = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);

            if (!visited.Add(current)) continue;

            var proto = FindPrototype(current);
            if (proto == null) continue;

            // Если среди прямых родителей есть BaseStructureDynamic — не структура
            foreach (var p in proto.Parents)
            {
                if (p == "BaseStructureDynamic")
                    return false;
            }

            // Если среди прямых родителей есть BaseStructure — это структура
            if (proto.Parents.Contains("BaseStructure"))
                return true;

            // Иначе идем дальше по цепочке родителей
            foreach (var p in proto.Parents)
                stack.Add(p);
        }

        return false;
    }

    /// <summary>
    /// Второй проход после загрузки всех прототипов — устанавливает IsStructure для каждого.
    /// </summary>
    private void CheckAllStructures()
    {
        foreach (var proto in _prototypes.Values)
        {
            proto.IsStructure = CheckIsStructure(proto.Id);
        }
    }

    /// <summary>
    /// Отступ спрайта (Sprite.offset), заданный для южной (rotation=0) ориентации.
    /// Ищет по цепочке родителей, как и FindStateRecursive — если у конкретного
    /// прототипа offset не переопределён, наследуется от родителя.
    /// </summary>
    public (float x, float y) GetSpriteOffset(string id)
    {
        return FindOffsetRecursive(id, 0);
    }

    /// <summary>
    /// Возвращает число направлений (directions) для иконного состояния прототипа,
    /// прочитанное из meta.json. Ищет state и path рекурсивно по цепочке родителей.
    /// Если meta.json нет или состояние не найдено — 1.
    /// </summary>
    public int GetStateDirections(string protoId)
    {
        string? state = FindStateRecursive(protoId, 0);
        if (string.IsNullOrEmpty(state))
            state = "closed";

        string? path = FindPathRecursive(protoId, 0);
        if (string.IsNullOrEmpty(path)) return 1;

        path = path.Replace("/", "\\").TrimStart('\\');
        if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(9);

        string metaPath = "";
        if (path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
        {
            metaPath = Path.Combine(_rootPath, "Resources", "Textures", path, "meta.json");
        }
        else
        {
            metaPath = Path.Combine(_rootPath, "Resources", "Textures", path + ".rsi", "meta.json");
        }

        try
        {
            if (!File.Exists(metaPath)) return 1;
            var json = File.ReadAllText(metaPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("states", out var statesArr)) return 1;

            foreach (var stateElem in statesArr.EnumerateArray())
            {
                if (!stateElem.TryGetProperty("name", out var nameEl)) continue;
                string name = nameEl.GetString() ?? "";
                if (name != state) continue;

                if (stateElem.TryGetProperty("directions", out var dirEl))
                    return dirEl.GetInt32();
                return 1;
            }
        }
        catch { }

        return 1;
    }

    private (float x, float y) FindOffsetRecursive(string id, int depth)
    {
        if (depth > 10) return (0f, 0f);

        var proto = FindPrototype(id);
        if (proto == null) return (0f, 0f);

        if (proto.HasOffset)
            return (proto.OffsetX, proto.OffsetY);

        if (!string.IsNullOrEmpty(proto.Parent))
            return FindOffsetRecursive(proto.Parent, depth + 1);

        return (0f, 0f);
    }


    public List<string> GetPrototypeIds()
    {
        return _prototypes.Keys.OrderBy(k => k).ToList();
    }

    public List<Palette> GetPalettes()
    {
        return _palettes.Values.OrderBy(p => p.Name).ToList();
    }

    public List<string> SearchPrototypes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetPrototypeIds();

        // Проверяем кэш (case-insensitive key)
        lock (_searchCache)
        {
            if (_searchCache.TryGetValue(query, out var cached))
                return cached;
        }

        // Матчим и по id (как раньше), и по русскому названию из локализации —
        // пользователь может не знать/не помнить английский id прототипа
        var results = _prototypes.Values
            .Where(p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(p.LocalizedName) &&
                         p.LocalizedName.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Id)
            .Take(1000)
            .ToList();

        // Сохраняем в кэш с ограничением размера (LRU через полную очистку при переполнении)
        lock (_searchCache)
        {
            if (_searchCache.Count >= MaxSearchCacheSize)
                _searchCache.Clear();
            _searchCache[query] = results;
        }

        return results;
    }

    
    /// <summary>
    /// Рекурсивно ищет drawdepth по цепочке родителей.
    /// Если у прототипа drawdepth не задан — ищет у ВСЕХ родителей
    /// (поддерживаем массив parent: [A, B]).
    /// </summary>
    private string? FindDrawDepthRecursive(string id, int depth)
    {
        if (depth > 10) return null;

        var proto = FindPrototype(id);
        if (proto == null) return null;

        if (!string.IsNullOrEmpty(proto.DrawDepth))
            return proto.DrawDepth;

        // Ищем drawdepth у ВСЕХ родителей (поддерживаем массив parent: [A, B])
        foreach (var parent in proto.Parents)
        {
            var result = FindDrawDepthRecursive(parent, depth + 1);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Получает drawdepth для прототипа, рекурсивно ища по цепочке родителей.
    /// </summary>
    public string? GetDrawDepth(string protoId)
    {
        return FindDrawDepthRecursive(protoId, 0);
    }

    public string? GetFullTexturePath(string id)
    {
        if (string.IsNullOrEmpty(_rootPath)) return null;

        var proto = FindPrototype(id);
        if (proto == null) return null;

        // 1. Ищем путь по цепочке родителей
        string? path = FindPathRecursive(id, 0);
        if (string.IsNullOrEmpty(path)) return null;

        // 2. Ищем state - СНАЧАЛА у самого прототипа, потом у родителей
        string? state = FindStateRecursive(id, 0);

        // 3. Если state не найден - используем "closed" как дефолтный для дверей
        if (string.IsNullOrEmpty(state))
        {
            state = "closed";
            System.Diagnostics.Debug.WriteLine($"State не найден для {id}, используем 'closed'");
        }

        // 4. Собираем полный путь
        path = path.Replace("/", "\\").TrimStart('\\');
        if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(9);

        string fullPath = Path.Combine(_rootPath, "Resources", "Textures", path);

        if (path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(fullPath))
            {
                // Пробуем найденный state
                string stateFile = Path.Combine(fullPath, state + ".png");
                if (File.Exists(stateFile))
                {
                    System.Diagnostics.Debug.WriteLine($"Найден файл: {stateFile}");
                    return stateFile;
                }

                // Если не найден - пробуем стандартные состояния
                string[] fallbackStates = { "closed", "open", "welded", "bolted", "full", "icon" };
                foreach (var fallback in fallbackStates)
                {
                    string testPath = Path.Combine(fullPath, fallback + ".png");
                    if (File.Exists(testPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Найден fallback файл: {testPath}");
                        return testPath;
                    }
                }

                // Берем любой PNG
                var pngFiles = Directory.GetFiles(fullPath, "*.png", SearchOption.TopDirectoryOnly);
                if (pngFiles.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Найден первый PNG: {pngFiles[0]}");
                    return pngFiles[0];
                }
            }
            return null;
        }

        if (!fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            fullPath += ".png";

        return File.Exists(fullPath) ? fullPath : null;
    }

    /// <summary>
    /// Аналог GetFullTexturePath, но для одного слоя многослойного спрайта: если у
    /// слоя нет собственного sprite/rsi/state (layerSpritePath/layerRsiPath/layerState
    /// == null) — путь и state наследуются от прототипа так же, как и для обычной
    /// (однослойной) текстуры, через FindPathRecursive/FindStateRecursive.
    /// </summary>
    public string? GetLayerTexturePath(string protoId, string? layerSpritePath, string? layerRsiPath, string? layerState)
    {
        if (string.IsNullOrEmpty(_rootPath)) return null;

        string? path = layerSpritePath ?? layerRsiPath;
        if (string.IsNullOrEmpty(path))
            path = FindPathRecursive(protoId, 0);
        if (string.IsNullOrEmpty(path)) return null;

        string? state = layerState;
        if (string.IsNullOrEmpty(state))
            state = FindStateRecursive(protoId, 0) ?? "icon";

        path = path.Replace("/", "\\").TrimStart('\\');
        if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(9);

        string fullPath = Path.Combine(_rootPath, "Resources", "Textures", path);

        if (path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(fullPath))
            {
                string stateFile = Path.Combine(fullPath, state + ".png");
                if (File.Exists(stateFile)) return stateFile;

                string[] fallbackStates = { "icon", "closed", "open", "full" };
                foreach (var fallback in fallbackStates)
                {
                    string testPath = Path.Combine(fullPath, fallback + ".png");
                    if (File.Exists(testPath)) return testPath;
                }

                var pngFiles = Directory.GetFiles(fullPath, "*.png", SearchOption.TopDirectoryOnly);
                if (pngFiles.Length > 0) return pngFiles[0];
            }
            return null;
        }

        if (!fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            fullPath += ".png";

        return File.Exists(fullPath) ? fullPath : null;
    }

    // Ищем ТОЛЬКО путь по цепочке родителей
    private string? FindPathRecursive(string id, int depth)
    {
        if (depth > 10) return null;

        var proto = FindPrototype(id);
        if (proto == null) return null;

        // Проверяем SpritePath и RsiPath
        string path = proto.SpritePath ?? proto.RsiPath ?? "";
        if (!string.IsNullOrEmpty(path))
        {
            System.Diagnostics.Debug.WriteLine($"Найден путь для {id}: {path} (глубина {depth})");
            return path;
        }

        // Если нет - идем к родителю
        if (!string.IsNullOrEmpty(proto.Parent))
        {
            return FindPathRecursive(proto.Parent, depth + 1);
        }

        return null;
    }

    // Ищем state СНАЧАЛА у самого прототипа, потом у родителей
    private string? FindStateRecursive(string id, int depth)
    {
        if (depth > 10) return null;

        var proto = FindPrototype(id);
        if (proto == null) return null;

        // СНАЧАЛА проверяем State у текущего прототипа
        if (!string.IsNullOrEmpty(proto.State))
        {
            System.Diagnostics.Debug.WriteLine($"Найден state у самого прототипа {id}: {proto.State} (глубина {depth})");
            return proto.State;
        }

        // Если у текущего нет - идем к родителю
        if (!string.IsNullOrEmpty(proto.Parent))
        {
            System.Diagnostics.Debug.WriteLine($"State не найден у {id}, ищем у родителя {proto.Parent}");
            return FindStateRecursive(proto.Parent, depth + 1);
        }

        return null;
    }

    private (string? path, string? state) FindSpriteRecursive(string id, int depth)
    {
        if (depth > 5) return (null, null);

        var proto = FindPrototype(id);
        if (proto == null) return (null, null);

        // Проверяем, есть ли спрайт у этого прототипа
        string path = proto.SpritePath ?? proto.RsiPath ?? "";
        if (!string.IsNullOrEmpty(path))
        {
            return (path, proto.State);
        }

        // Если нет - идем к родителю
        if (!string.IsNullOrEmpty(proto.Parent))
        {
            return FindSpriteRecursive(proto.Parent, depth + 1);
        }

        return (null, null);
    }


    // Парсит "- type: palette" блоки отдельным проходом — сами цвета лежат во
    // вложенной мапе "colors:", а не в плоских полях как у tile/entity/decal,
    // поэтому не переиспользует ParseBlock. Строки, начинающиеся с "#" внутри
    // блока (закомментированные цвета вроде "#light: ..."), пропускаются.
    private List<Palette> ParsePalettes(string content)
    {
        var result = new List<Palette>();
        var lines = content.Split('\n');

        bool inPalette = false;
        bool inColors = false;
        int colorsIndent = 0;

        string id = "";
        string name = "";
        Dictionary<string, string> colors = new();

        void FinishCurrent()
        {
            if (!string.IsNullOrEmpty(id))
            {
                result.Add(new Palette
                {
                    Id = id,
                    Name = string.IsNullOrEmpty(name) ? id : name,
                    Colors = colors
                });
            }
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            if (trimmed.StartsWith("- type:") && indent == 0)
            {
                if (inPalette) FinishCurrent();

                var tMatch = Regex.Match(line, @"- type:\s*(\S+)");
                string type = tMatch.Success ? tMatch.Groups[1].Value : "";

                inPalette = type == "palette";
                inColors = false;
                id = "";
                name = "";
                colors = new Dictionary<string, string>();
                continue;
            }

            if (!inPalette) continue;
            if (trimmed.StartsWith("#")) continue; // закомментированная строка

            if (!inColors)
            {
                var idMatch = Regex.Match(trimmed, @"^id:\s*(\S+)");
                if (idMatch.Success && string.IsNullOrEmpty(id)) { id = idMatch.Groups[1].Value; continue; }

                var nameMatch = Regex.Match(trimmed, @"^name:\s*(.+)$");
                if (nameMatch.Success && string.IsNullOrEmpty(name))
                {
                    name = nameMatch.Groups[1].Value.Trim().Trim('"');
                    continue;
                }

                if (trimmed.StartsWith("colors:"))
                {
                    inColors = true;
                    colorsIndent = indent;
                    continue;
                }
            }
            else
            {
                if (indent <= colorsIndent)
                {
                    inColors = false;
                }
                else
                {
                    var colorMatch = Regex.Match(trimmed, "^(\\w+):\\s*\"?(#[0-9A-Fa-f]{6,8})\"?");
                    if (colorMatch.Success)
                        colors[colorMatch.Groups[1].Value] = colorMatch.Groups[2].Value;
                }
            }
        }

        if (inPalette) FinishCurrent();

        return result;
    }


}