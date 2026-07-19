// Services/EntityTypeRegistry.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Реестр всех типов сущностей (MapEntity и его наследников) в сборке.
/// Используется для сохранения/загрузки проекта так, чтобы новые типы,
/// добавленные в будущем, автоматически подхватывались без правок кода сохранения.
/// </summary>
public static class EntityTypeRegistry
{
    private static readonly Dictionary<string, Type> _types = Build();

    private static Dictionary<string, Type> Build()
    {
        var dict = new Dictionary<string, Type>();
        var baseType = typeof(MapEntity);

        foreach (var t in baseType.Assembly.GetTypes())
        {
            if (t == baseType || t.IsSubclassOf(baseType))
            {
                dict[t.Name] = t;
            }
        }

        return dict;
    }

    public static bool TryGetType(string typeName, out Type type)
    {
        return _types.TryGetValue(typeName, out type!);
    }
}