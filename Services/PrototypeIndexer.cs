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
    var blocks = Regex.Split(content, @"(?=^[ \t]*- type:)", RegexOptions.Multiline);
    
    foreach (var block in blocks)
    {
        if (string.IsNullOrWhiteSpace(block)) continue;
        if (!block.Contains("id:")) continue;
        if (block.Contains("abstract: true")) continue;

        var typeMatch = Regex.Match(block, @"^[ \t]*- type:\s*(\S+)", RegexOptions.Multiline);
        if (!typeMatch.Success) continue;
        var blockType = typeMatch.Groups[1].Value;
        
        if (blockType != "tile" && blockType != "entity") continue;

        var idMatch = Regex.Match(block, @"^[ \t]*id:\s*(\S+)", RegexOptions.Multiline);
        if (!idMatch.Success) continue;
        
        var id = idMatch.Groups[1].Value;
        if (string.IsNullOrEmpty(id)) continue;
        if (!char.IsUpper(id[0])) continue;

        var proto = new Prototype
        {
            Id = id,
            FilePath = filePath
        };

        // Для tile — sprite на уровне блока
        if (blockType == "tile")
        {
            var spriteMatch = Regex.Match(block, @"sprite:\s*(\S+)");
            if (spriteMatch.Success)
            {
                // ===== НОРМАЛИЗУЕМ ПУТЬ СРАЗУ =====
                string spritePath = spriteMatch.Groups[1].Value;
                spritePath = spritePath.Replace("/", "\\");
                spritePath = spritePath.TrimStart('\\');
                if (spritePath.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                    spritePath = spritePath.Substring(9);
                proto.SpritePath = spritePath;
            }
        }
        else // entity
        {
            var compMatch = Regex.Match(block, @"components:\s*\n((?:.+\n)+?)(?=\n[^ ]|$)", RegexOptions.Multiline);
            if (compMatch.Success)
            {
                var compText = compMatch.Groups[1].Value;
                var spriteMatch = Regex.Match(compText, @"sprite:\s*(\S+)");
                if (spriteMatch.Success)
                {
                    string spritePath = spriteMatch.Groups[1].Value;
                    spritePath = spritePath.Replace("/", "\\");
                    spritePath = spritePath.TrimStart('\\');
                    if (spritePath.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                        spritePath = spritePath.Substring(9);
                    proto.SpritePath = spritePath;
                }
                
                var rsiMatch = Regex.Match(compText, @"rsi:\s*(\S+)");
                if (rsiMatch.Success)
                {
                    string rsiPath = rsiMatch.Groups[1].Value;
                    rsiPath = rsiPath.Replace("/", "\\");
                    rsiPath = rsiPath.TrimStart('\\');
                    if (rsiPath.StartsWith("Textures\\", StringComparison.OrdinalIgnoreCase))
                        rsiPath = rsiPath.Substring(9);
                    proto.RsiPath = rsiPath;
                }
            }
        }

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
    if (string.IsNullOrEmpty(_rootPath)) return null;
    
    var proto = FindPrototype(id);
    if (proto == null) return null;

    string path = proto.SpritePath ?? proto.RsiPath ?? "";
    if (string.IsNullOrEmpty(path)) return null;

    string fullPath = Path.Combine(_rootPath, "Resources", "Textures", path);
    if (!fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        fullPath += ".png";

    return File.Exists(fullPath) ? fullPath : null;
}



}