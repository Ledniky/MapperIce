using MapperIce.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MapperIce.Services;

public class PrototypeIndexer
{
    private Dictionary<string, Prototype> _prototypes = new();
    private string _currentRepoPath = "";
    private string _currentRepoId = "";
    private string _rootPath = "";
    private Dictionary<string, Palette> _palettes = new();
    public event Action? OnIndexingComplete;
    public string CurrentRepoId => _currentRepoId;

    public string GetRootPath() => _rootPath;

    public void IndexRepository(Repository repo)
    {
        _rootPath = repo.Path;
        _currentRepoId = repo.Id;
        _currentRepoPath = repo.Path;

        // Сначала пробуем быстро восстановить индекс из кэша на диске,
        // чтобы не пересканировать весь репозиторий заново при каждом запуске
        if (TryLoadCache(repo.Id))
        {
            OnIndexingComplete?.Invoke();
            return;
        }

        ReindexFromDisk(repo);
    }

    /// <summary>
    /// Полное пересканирование репозитория с диска (используется кнопкой "Обновить"
    /// и как fallback, если кэша ещё нет или он повреждён)
    /// </summary>
public void ReindexFromDisk(Repository repo)
{
    _rootPath = repo.Path;
    _currentRepoId = repo.Id;
    _currentRepoPath = repo.Path;
    _prototypes.Clear();
    _palettes.Clear();

    string prototypesPath = Path.Combine(repo.Path, "Resources", "Prototypes");
    if (!Directory.Exists(prototypesPath))
    {
        MessageBox.Show($"Папка Prototypes не найдена: {prototypesPath}");
        return;
    }

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

    SaveCache(repo.Id);
    OnIndexingComplete?.Invoke();
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
    
    private const int CacheFormatVersion = 5; 

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
        File.WriteAllText(path, json);
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

        // Ищем parent
        var parentMatch = Regex.Match(block, @"parent:\s*([^\s]+)");
        if (parentMatch.Success)
        {
            proto.Parent = parentMatch.Groups[1].Value;
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

        return proto;
    }

    public Prototype? FindPrototype(string id)
    {
        _prototypes.TryGetValue(id, out var proto);
        return proto;
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

        return _prototypes.Keys
            .Where(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(1000)
            .ToList();
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