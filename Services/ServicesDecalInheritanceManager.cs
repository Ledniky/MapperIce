// Services/DecalInheritanceManager.cs
using MapperIce.Models;
using System.Text.Json;

namespace MapperIce.Services;

public class DecalTypeNode
{
    public Type Type = null!;
    public string DisplayName = "";
    public bool IsAbstractType;
    public List<DecalTypeNode> Children = new();
}

/// <summary>
/// Хранит "покрашенные" (явно заданные) DecalRuleSet по именам классов реальной
/// C#-иерархии RoomType (не по Pack/Category — по фактическому Type.BaseType).
/// Комната, не имеющая собственного правила, наследует ближайшее явное правило от
/// предка вверх по цепочке (RoomType — корень). "Покрасить родителя и всех дочек" —
/// это естественное следствие: задать правило на родителе, и все потомки без своего
/// явного правила сразу начинают его показывать через ResolveEffectiveRule.
/// </summary>
public class DecalInheritanceManager
{
    // Ключ — Type.Name (например "MedicalRoomType", "Warden")
    private Dictionary<string, DecalRuleSet> _rules = new();
    public event Action? OnChanged;

    private readonly string _storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce", "decal_type_rules.json"
    );
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public DecalInheritanceManager()
    {
        Load();
    }

    public bool HasExplicitRule(Type type) => _rules.ContainsKey(type.Name);

    /// <summary>Возвращает (и создаёт при отсутствии) явное правило для типа — ссылка "по месту", мутировать напрямую и звать Save().</summary>
    public DecalRuleSet GetOrCreateOwn(Type type, DecalRuleSet? seedFrom = null)
    {
        if (!_rules.TryGetValue(type.Name, out var rule))
        {
            rule = seedFrom?.Clone() ?? new DecalRuleSet();
            _rules[type.Name] = rule;
            Save();
            OnChanged?.Invoke();
        }
        return rule;
    }

    public void ClearRule(Type type)
    {
        if (_rules.Remove(type.Name))
        {
            Save();
            OnChanged?.Invoke();
        }
    }

    /// <summary>Идёт вверх по Type.BaseType, начиная с самого type, до первого явного правила. Возвращает null, если нигде в цепочке правило не задано.</summary>
    public DecalRuleSet? ResolveEffectiveRule(Type? type)
    {
        var t = type;
        while (t != null)
        {
            if (_rules.TryGetValue(t.Name, out var rule)) return rule;
            if (t == typeof(RoomType)) break;
            t = t.BaseType;
        }
        return null;
    }

    /// <summary>Рекурсивно снимает явные правила у ВСЕХ потомков узла (не у самого узла) — они начинают чисто наследовать от него.</summary>
    public void ClearDescendantOverrides(DecalTypeNode node)
    {
        bool anyChanged = false;
        void Walk(DecalTypeNode n)
        {
            foreach (var child in n.Children)
            {
                if (_rules.Remove(child.Type.Name)) anyChanged = true;
                Walk(child);
            }
        }
        Walk(node);

        if (anyChanged)
        {
            Save();
            OnChanged?.Invoke();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            var json = JsonSerializer.Serialize(_rules, _jsonOptions);
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
            var loaded = JsonSerializer.Deserialize<Dictionary<string, DecalRuleSet>>(json, _jsonOptions);
            if (loaded != null) _rules = loaded;
        }
        catch { }
    }

    // ==================== Построение дерева реальной C#-иерархии ====================

    public DecalTypeNode BuildTree()
    {
        var allTypes = typeof(RoomType).Assembly.GetTypes()
            .Where(t => t == typeof(RoomType) || t.IsSubclassOf(typeof(RoomType)))
            .Where(t => t != typeof(CustomRoomType)) // кастомные типы — данные, не классы; в дерево не входят
            .ToList();

        var nodes = new Dictionary<Type, DecalTypeNode>();
        foreach (var t in allTypes)
        {
            nodes[t] = new DecalTypeNode
            {
                Type = t,
                IsAbstractType = t.IsAbstract,
                DisplayName = t.IsAbstract ? PrettyAbstractName(t) : PrettyConcreteName(t)
            };
        }

        var root = nodes[typeof(RoomType)];
        foreach (var t in allTypes)
        {
            if (t == typeof(RoomType)) continue;
            var parentType = t.BaseType;
            if (parentType != null && nodes.TryGetValue(parentType, out var parentNode))
                parentNode.Children.Add(nodes[t]);
        }

        return root;
    }

    private static string PrettyAbstractName(Type t)
    {
        if (t == typeof(RoomType)) return "RoomType (базовый)";
        var n = t.Name;
        if (n.EndsWith("RoomType")) n = n.Substring(0, n.Length - "RoomType".Length);
        return n.Replace("Plus", "+");
    }

    private static string PrettyConcreteName(Type t)
    {
        try
        {
            if (Activator.CreateInstance(t) is RoomType instance)
                return instance.Name;
        }
        catch { }
        return t.Name;
    }
}