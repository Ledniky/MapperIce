using System.Security.Cryptography;
using System.Text;

namespace MapperIce.Models;

public class Repository
{
    private string _path = "";

    // Оставляем публичный set — нужен для обратной совместимости с уже
    // существующими repositories.json (там есть поле "Id"), но фактическое
    // значение всегда пересчитывается из Path в его сеттере ниже, так что
    // это поле больше не источник истины само по себе.
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Path
    {
        get => _path;
        set
        {
            _path = value;
            if (!string.IsNullOrEmpty(value))
                Id = ComputeStableId(value);
        }
    }

    public bool IsIndexed { get; set; } = false;
    public DateTime LastIndexed { get; set; }
    public int PrototypeCount { get; set; }

    // Id репозитория теперь ВСЕГДА один и тот же для одного и того же пути на
    // диске, а не случайный Guid, генерируемый при каждом создании объекта.
    // Раньше, если repositories.json терялся или повреждался (например, из-за
    // неатомарной записи при аварийном завершении/перезагрузке), повторное
    // добавление той же папки создавало Repository с НОВЫМ случайным Id — и кэш
    // индексатора (index_cache/{Id}.json), сохранённый под старым Id, становился
    // недостижимым: TryLoadCache искал файл по новому Id и не находил его, хотя
    // он физически лежал на диске под другим именем. Теперь тот же путь = тот же
    // Id = тот же файл кэша находится всегда, независимо от судьбы repositories.json.
    private static string ComputeStableId(string path)
    {
        var normalized = path.TrimEnd('\\', '/').ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    public override string ToString() => Name;
}