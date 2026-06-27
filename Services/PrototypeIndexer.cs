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
        
        string currentBlock = "";
        string currentId = "";
        string currentType = "";
        bool inBlock = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            
            // ===== НОВЫЙ БЛОК: начинается с "- type:" =====
            if (trimmed.StartsWith("- type:"))
            {
                // Сохраняем предыдущий блок
                if (inBlock && !string.IsNullOrEmpty(currentId))
                {
                    var proto = ParseSingleBlock(currentBlock, currentId, currentType, filePath);
                    if (proto != null) result.Add(proto);
                }
                
                // Начинаем новый блок
                currentBlock = line + "\n";
                inBlock = true;
                currentId = "";
                
                var typeMatch = Regex.Match(line, @"- type:\s*(\S+)");
                currentType = typeMatch.Success ? typeMatch.Groups[1].Value : "";
                
                // Ищем id в этой же строке
                var idMatch = Regex.Match(line, @"id:\s*(\S+)");
                if (idMatch.Success) currentId = idMatch.Groups[1].Value;
            }
            else if (inBlock)
            {
                // ===== ДОБАВЛЯЕМ ВСЕ СТРОКИ В БЛОК =====
                currentBlock += line + "\n";
                
                // Ищем id (если ещё не нашли)
                if (string.IsNullOrEmpty(currentId))
                {
                    var idMatch = Regex.Match(line, @"^[ \t]*id:\s*(\S+)");
                    if (idMatch.Success) currentId = idMatch.Groups[1].Value;
                }
            }
        }
        
        // Сохраняем последний блок
        if (inBlock && !string.IsNullOrEmpty(currentId))
        {
            var proto = ParseSingleBlock(currentBlock, currentId, currentType, filePath);
            if (proto != null) result.Add(proto);
        }
        
        return result;
    }

    private Prototype? ParseSingleBlock(string block, string id, string type, string filePath)
    {
        // Оставляем только tile и entity
        if (type != "tile" && type != "entity") return null;
        if (string.IsNullOrEmpty(id)) return null;
        if (!char.IsUpper(id[0])) return null;
        
        var proto = new Prototype
        {
            Id = id,
            FilePath = filePath
        };
        
        // ===== ИЩЕМ sprite И rsi ВО ВСЁМ БЛОКЕ =====
        var spriteMatch = Regex.Match(block, @"sprite:\s*([^\s]+)");
        if (spriteMatch.Success)
        {
            string spritePath = spriteMatch.Groups[1].Value;
            spritePath = spritePath.Replace("/", "\\").TrimStart('\\');
            if (spritePath.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                spritePath = spritePath.Substring(9);
            proto.SpritePath = spritePath;
        }
        
        var rsiMatch = Regex.Match(block, @"rsi:\s*([^\s]+)");
        if (rsiMatch.Success)
        {
            string rsiPath = rsiMatch.Groups[1].Value;
            rsiPath = rsiPath.Replace("/", "\\").TrimStart('\\');
            if (rsiPath.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                rsiPath = rsiPath.Substring(9);
            proto.RsiPath = rsiPath;
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

    public string? GetFullTexturePath(string id)
    {
        if (string.IsNullOrEmpty(_rootPath)) return null;
        
        var proto = FindPrototype(id);
        if (proto == null) return null;

        string path = proto.SpritePath ?? proto.RsiPath ?? "";
        if (string.IsNullOrEmpty(path)) return null;

        // Нормализуем путь
        path = path.Replace("/", "\\").TrimStart('\\');
        if (path.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(9);

        string fullPath = Path.Combine(_rootPath, "Resources", "Textures", path);
        
        // Если это .rsi — ищем первый .png внутри папки
        if (path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
        {
            string dirPath = fullPath;
            if (Directory.Exists(dirPath))
            {
                var pngFiles = Directory.GetFiles(dirPath, "*.png", SearchOption.TopDirectoryOnly);
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
        
        // Если не нашли — пробуем найти по имени без учёта регистра
        string dir = Path.GetDirectoryName(fullPath)!;
        string fileName = Path.GetFileName(fullPath);
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                if (string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
        }
        
        return null;
    }
}