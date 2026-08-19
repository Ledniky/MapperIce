// Services/DecalPackManager.cs
using MapperIce.Models;
using System.Text.Json;

namespace MapperIce.Services;

public class DecalPackManager
{
    private Dictionary<string, DecalPack> _packs = new();
    public event Action? OnPacksChanged;

    private readonly string _storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce", "decal_packs.json"
    );

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public DecalPackManager()
    {
        Load();
    }

    public IReadOnlyList<DecalPack> Packs => _packs.Values.OrderBy(p => p.Name).ToList();

    public DecalPack? GetById(string id) => _packs.TryGetValue(id, out var p) ? p : null;

    public List<string> GetCategories() => _packs.Values.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

    /// <summary>Извлечённые (Extracted) паки сгруппированы под именем репозитория/сканирования — категория "Extracted" по умолчанию.</summary>
public (int added, int updated) MergeScanned(List<DecalPack> scanned)
    {
        int added = 0, updated = 0;

        var existingExtractedByName = _packs.Values
            .Where(p => p.Source == DecalPackSource.Extracted)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // МИГРАЦИЯ: паки, сохранённые ДО появления полей Category/Source в модели,
        // при загрузке старого JSON получили дефолтные значения (Source=Custom,
        // Category="Custom") независимо от того, откуда они реально появились —
        // из-за этого извлечённые паки скапливались в папке Custom вперемешку с
        // настоящими ручными. Если имя из свежего скана совпадает с уже существующим
        // "Custom"-паком, который никогда не редактировался (Positions идентичны
        // результату скана) — считаем его такой устаревшей записью и удаляем,
        // заменяя на новую, корректно помеченную как Extracted
        var staleCustomDuplicates = new List<string>();
        var scannedByName = scanned.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var existing in _packs.Values.Where(p => p.Source == DecalPackSource.Custom))
        {
            if (scannedByName.TryGetValue(existing.Name, out var freshlyScanned) &&
                PositionsEqual(existing.Positions, freshlyScanned.Positions))
            {
                staleCustomDuplicates.Add(existing.Id);
            }
        }
        foreach (var id in staleCustomDuplicates) _packs.Remove(id);

        foreach (var pack in scanned)
        {
            pack.Source = DecalPackSource.Extracted;
            if (string.IsNullOrWhiteSpace(pack.Category) || pack.Category == "Custom") pack.Category = "Extracted";

            if (existingExtractedByName.TryGetValue(pack.Name, out var existingExtracted))
            {
                existingExtracted.Positions = pack.Positions;
                updated++;
            }
            else
            {
                _packs[pack.Id] = pack;
                added++;
            }
        }

        if (added > 0 || updated > 0 || staleCustomDuplicates.Count > 0)
        {
            Save();
            OnPacksChanged?.Invoke();
        }
        return (added, updated);
    }

    private static bool PositionsEqual(Dictionary<DecalPosition, string> a, Dictionary<DecalPosition, string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var v) || v != kvp.Value) return false;
        }
        return true;
    }
    
        public void AddOrUpdate(DecalPack pack)
    {
        _packs[pack.Id] = pack;
        Save();
        OnPacksChanged?.Invoke();
    }

    /// <summary>
    /// Создаёт независимую копию пака (новый Id, категория "PerType") — используется
    /// окном "Наследование декалей", чтобы разные абстрактные типы комнат (например,
    /// Command и Engineering) могли использовать один и тот же узор декалей, но
    /// настраивать себе разный цвет, не влияя друг на друга.
    /// </summary>
    public DecalPack CloneForOwnUse(DecalPack source, string newName)
    {
        var clone = source.Clone();
        clone.Id = Guid.NewGuid().ToString();
        clone.Name = newName;
        clone.Category = "PerType";
        clone.Source = DecalPackSource.Custom;
        _packs[clone.Id] = clone;
        Save();
        OnPacksChanged?.Invoke();
        return clone;
    }

    public void Remove(string id)
    {
        if (_packs.Remove(id))
        {
            Save();
            OnPacksChanged?.Invoke();
        }
    }

    /// <summary>Переименовывает категорию (папку) целиком — у всех паков этой категории меняется Category.</summary>
    public void RenameCategory(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return;

        bool anyChanged = false;
        foreach (var pack in _packs.Values.Where(p => p.Category == oldName))
        {
            pack.Category = newName;
            anyChanged = true;
        }

        if (anyChanged)
        {
            Save();
            OnPacksChanged?.Invoke();
        }
    }

    // ==================== ЭКСПОРТ / ИМПОРТ (по образцу RoomTypeManager) ====================

    public void ExportPack(string id, string filePath)
    {
        var pack = GetById(id);
        if (pack == null) return;

        var data = new DecalPackExportData
        {
            Name = pack.Name,
            Category = pack.Category,
            Color = pack.Color,
            Positions = pack.Positions.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    public void ExportCategory(string category, string filePath)
    {
        var dataList = _packs.Values.Where(p => p.Category == category).Select(pack => new DecalPackExportData
        {
            Name = pack.Name,
            Category = pack.Category,
            Color = pack.Color,
            Positions = pack.Positions.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
        }).ToList();

        var json = JsonSerializer.Serialize(dataList, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>Импортирует один файл (одиночный пак или массив паков категории). Импортированные всегда попадают в Custom.</summary>
    public (int imported, int skipped) ImportFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var trimmed = json.TrimStart();

        var entries = new List<DecalPackExportData>();
        if (trimmed.StartsWith("["))
        {
            var list = JsonSerializer.Deserialize<List<DecalPackExportData>>(json, _jsonOptions);
            if (list != null) entries.AddRange(list);
        }
        else
        {
            var single = JsonSerializer.Deserialize<DecalPackExportData>(json, _jsonOptions);
            if (single != null) entries.Add(single);
        }

        int imported = 0, skipped = 0;
        var existingNames = _packs.Values.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var data in entries)
        {
            if (existingNames.Contains(data.Name)) { skipped++; continue; }

            var positions = new Dictionary<DecalPosition, string>();
            foreach (var kvp in data.Positions)
            {
                if (Enum.TryParse<DecalPosition>(kvp.Key, out var pos))
                    positions[pos] = kvp.Value;
            }

            var pack = new DecalPack
            {
                Name = data.Name,
                Category = string.IsNullOrWhiteSpace(data.Category) ? "Custom" : data.Category,
                Color = string.IsNullOrWhiteSpace(data.Color) ? "#FFFFFFFF" : data.Color,
                Positions = positions,
                Source = DecalPackSource.Custom
            };
            _packs[pack.Id] = pack;
            existingNames.Add(pack.Name);
            imported++;
        }

        if (imported > 0)
        {
            Save();
            OnPacksChanged?.Invoke();
        }
        return (imported, skipped);
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            var json = JsonSerializer.Serialize(_packs.Values.ToList(), _jsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storagePath)) return;
            var json = File.ReadAllText(_storagePath);
            var list = JsonSerializer.Deserialize<List<DecalPack>>(json, _jsonOptions);
            if (list == null) return;

            _packs.Clear();
            foreach (var pack in list)
            {
                if (string.IsNullOrWhiteSpace(pack.Category))
                    pack.Category = pack.Source == DecalPackSource.Extracted ? "Extracted" : "Custom";
                _packs[pack.Id] = pack;
            }
        }
        catch { }
    }
}