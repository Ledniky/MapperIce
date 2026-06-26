using MapperIce.Models;
using System.Text.RegularExpressions;

namespace MapperIce.Services;

public class PrototypeIndexer
{
    private Dictionary<string, Prototype> _prototypes = new();
    private string _currentRepoPath = "";
    private string _currentRepoId = "";

    public event Action? OnIndexingComplete;
    public string CurrentRepoId => _currentRepoId;

    public void IndexRepository(Repository repo)
    {
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
    
    // Ищем все блоки с id:
    var blocks = Regex.Split(content, @"(?=^[ \t]*id:)", RegexOptions.Multiline);
    
    foreach (var block in blocks)
    {
        if (string.IsNullOrWhiteSpace(block)) continue;
        if (!block.Contains("id:")) continue;
        
        // Парсим ID
        var idMatch = Regex.Match(block, @"id:\s*(\S+)");
        if (!idMatch.Success) continue;
        
        var id = idMatch.Groups[1].Value;
        
        // ❌ ФИЛЬТРЫ
        if (string.IsNullOrEmpty(id)) continue;
        if (!Regex.IsMatch(id, @"[A-Za-z]")) continue;                    // Должна быть буква
        if (id == "true" || id == "false") continue;                     // Булевы
        if (id.All(c => char.IsDigit(c))) continue;                      // Только цифры
        if (id.StartsWith("!")) continue;                                // Начинается с "!"
        if (id.StartsWith("#")) continue;                                // Начинается с "#"
        if (id.StartsWith("\"")) continue;                               // Начинается с кавычки
        if (id.StartsWith("*")) continue;                                // Начинается с "*" ← НОВОЕ
        if (id.Contains("*")) continue;                                  // Содержит "*" ← НОВОЕ
        if (id.StartsWith("'")) continue;                                // Начинается с апострофа
        if (id.Contains("'")) continue;                                  // Содержит апостроф
        if (id.StartsWith(".")) continue;                                // Начинается с точки
        if (id.All(c => !char.IsLetterOrDigit(c))) continue;             // Только спецсимволы
        
        var proto = new Prototype();
        proto.Id = id;
        proto.FilePath = filePath;

        // Ищем sprite
        var spriteMatch = Regex.Match(block, @"sprite:\s*(\S+)");
        if (spriteMatch.Success)
            proto.SpritePath = spriteMatch.Groups[1].Value;

        // Ищем rsi
        var rsiMatch = Regex.Match(block, @"rsi:\s*(\S+)");
        if (rsiMatch.Success)
            proto.RsiPath = rsiMatch.Groups[1].Value;

        result.Add(proto);
    }

    return result;
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
        var proto = FindPrototype(id);
        if (proto == null) return null;

        string texturesPath = Path.Combine(_currentRepoPath, "Resources", "Textures");
        string relativePath = proto.SpritePath ?? proto.RsiPath ?? "";
        
        if (string.IsNullOrEmpty(relativePath)) return null;

        relativePath = relativePath.Replace("/", "\\").TrimStart('\\');
        
        if (relativePath.EndsWith(".rsi"))
        {
            string dirPath = Path.Combine(texturesPath, relativePath);
            if (Directory.Exists(dirPath))
            {
                var pngFiles = Directory.GetFiles(dirPath, "*.png", SearchOption.TopDirectoryOnly);
                if (pngFiles.Length > 0)
                    return pngFiles[0];
            }
            return null;
        }

        string fullPath = Path.Combine(texturesPath, relativePath + ".png");
        if (File.Exists(fullPath))
            return fullPath;
        
        return null;
    }
}