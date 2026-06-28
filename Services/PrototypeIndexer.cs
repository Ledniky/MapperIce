using MapperIce.Models;
using System.Text.RegularExpressions;

namespace MapperIce.Services;

public class PrototypeIndexer
{
    private Dictionary<string, Prototype> _prototypes = new();
    private string _currentRepoPath = "";
    private string _currentRepoId = "";
    private string _rootPath = "";

    public event Action? OnIndexingComplete;
    public string CurrentRepoId => _currentRepoId;

    public string GetRootPath() => _rootPath;

    public void IndexRepository(Repository repo)
    {
        _rootPath = repo.Path;
        _currentRepoId = repo.Id;
        _currentRepoPath = repo.Path;
        _prototypes.Clear();

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
            }
            catch { }
        }

        OnIndexingComplete?.Invoke();
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
        if (type != "tile" && type != "entity") return null;
        if (string.IsNullOrEmpty(id)) return null;
        if (!char.IsUpper(id[0])) return null;

        var proto = new Prototype { Id = id, FilePath = filePath };

        // Ищем sprite
        var s = Regex.Match(block, @"sprite:\s*([^\s]+)");
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

        // ===== ИЩЕМ state =====
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

    public List<string> SearchPrototypes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetPrototypeIds();

        return _prototypes.Keys
            .Where(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(1000)
            .ToList();
    }

private string? GetSpritePathRecursive(string id, int depth, out string? state)
{
    state = null;
    
    if (depth > 3) return null;
    
    var proto = FindPrototype(id);
    if (proto == null) return null;
    
    // Если есть спрайт — возвращаем его
    string path = proto.SpritePath ?? proto.RsiPath ?? "";
    if (!string.IsNullOrEmpty(path))
    {
        state = proto.State;
        return path;
    }
    
    // Если нет — ищем у родителя
    if (!string.IsNullOrEmpty(proto.Parent))
        return GetSpritePathRecursive(proto.Parent, depth + 1, out state);
    
    return null;
}

public string? GetFullTexturePath(string id)
{
    if (string.IsNullOrEmpty(_rootPath)) return null;
    
    // Ищем путь и state с учётом наследования
    string? path = GetSpritePathRecursive(id, 0, out var state);
    if (string.IsNullOrEmpty(path)) return null;

    // Нормализуем путь
    path = path.Replace("/", "\\").TrimStart('\\');
    if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
        path = path.Substring(9);

    string fullPath = Path.Combine(_rootPath, "Resources", "Textures", path);
    
    // Если это .rsi — ищем state
    if (path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
    {
        if (Directory.Exists(fullPath))
        {
            // 1. Если есть state — ищем его
            if (!string.IsNullOrEmpty(state))
            {
                string stateFile = Path.Combine(fullPath, state + ".png");
                if (File.Exists(stateFile))
                    return stateFile;
            }
            
            // 2. ПРИОРИТЕТ: closed.png (для дверей)
            string[] priorityFiles = { "closed.png", "full.png", "state0.png", "icon.png" };
            foreach (var fileName in priorityFiles)
            {
                string testPath = Path.Combine(fullPath, fileName);
                if (File.Exists(testPath))
                    return testPath;
            }
            
            // 3. Если ничего не нашли — берём первый .png
            var pngFiles = Directory.GetFiles(fullPath, "*.png", SearchOption.TopDirectoryOnly);
            if (pngFiles.Length > 0)
                return pngFiles[0];
        }
        return null;
    }

    // Обычный файл .png
    if (!fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        fullPath += ".png";

    if (File.Exists(fullPath))
        return fullPath;
    
    return null;
}




}