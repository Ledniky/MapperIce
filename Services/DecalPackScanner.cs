// Services/DecalPackScanner.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Автоматически собирает DecalPack из проиндексированного репозитория: группирует
/// декали по общему "стеблю" имени. Направление (N/S/E/W/NE/NW/SE/SW) ищется НЕ на
/// конце всей строки id (как было раньше — ломалось на именах вида
/// "WoodTrimThinLineEWhite", где после направления ещё идёт хвост с цветом), а сразу
/// ПОСЛЕ ключевого слова роли (Line/Corner/End/Inner). Стебель — это всё остальное:
/// префикс до роли + всё, что шло после направления (включая цветовой хвост типа
/// "White"), склеенные вместе. Так "WoodTrimThinLineEWhite" и "WoodTrimThinLineNWhite"
/// дают одинаковый стебель "WoodTrimThinWhite" и корректно попадают в один пак.
/// </summary>
public static class DecalPackScanner
{
    private static readonly string[] DiagonalSuffixes = { "NE", "NW", "SE", "SW" };
    private static readonly string[] CardinalSuffixes = { "N", "S", "E", "W" };
    private static readonly (string keyword, Role role)[] RoleKeywords =
    {
        ("Inner", Role.InnerCorner),   // Inner проверяем раньше Corner — "InnerCorner" содержит "Corner" как подстроку
        ("Corner", Role.OuterCorner),
        ("End", Role.DeadEnd),
        ("Line", Role.Side),
    };

    private enum Role { InnerCorner, OuterCorner, DeadEnd, Side }

    private static string? _cachedRepoId = null;
    private static List<DecalPack> _cachedResult = new();

    public static List<DecalPack> ScanFromIndexer(PrototypeIndexer indexer, bool forceRescan = false)
    {
        if (!forceRescan && _cachedRepoId == indexer.CurrentRepoId && _cachedResult.Count > 0)
            return _cachedResult;

        var result = ScanUncached(indexer);
        _cachedRepoId = indexer.CurrentRepoId;
        _cachedResult = result;
        return result;
    }

    private static List<DecalPack> ScanUncached(PrototypeIndexer indexer)
    {
        var families = new Dictionary<string, DecalPack>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in indexer.GetPrototypeIds())
        {
            var proto = indexer.FindPrototype(id);
            if (proto == null || proto.Type != "decal") continue;

            if (!TryExtractRoleDirectionStem(id, out Role role, out string direction, out string stem)) continue;

            var position = ResolvePosition(role, direction);
            if (position == null) continue;

            if (!families.TryGetValue(stem, out var pack))
            {
                pack = new DecalPack { Name = stem, Category = "Extracted", Source = DecalPackSource.Extracted };
                families[stem] = pack;
            }
            pack.Positions[position.Value] = id;
        }

        return families.Values
            .Where(p => p.Positions.Count > 0)
            .OrderBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Ищет ключевое слово роли в id, а направление — СРАЗУ ПОСЛЕ него (не на конце
    /// строки). Стебель = префикс (до ключевого слова) + суффикс (всё после
    /// направления, например "White"), склеенные без ключевого слова и направления.
    /// </summary>
    private static bool TryExtractRoleDirectionStem(string id, out Role role, out string direction, out string stem)
    {
        role = default; direction = ""; stem = "";

        foreach (var (keyword, r) in RoleKeywords)
        {
            int idx = id.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            int afterKeyword = idx + keyword.Length;
            string rest = id.Substring(afterKeyword);

            // Сначала диагональные (2 буквы) — иначе "NE" ошибочно поймается как "N" + "E" потерян
            string? foundDir = DiagonalSuffixes.FirstOrDefault(s => rest.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                             ?? CardinalSuffixes.FirstOrDefault(s => rest.StartsWith(s, StringComparison.OrdinalIgnoreCase));

            if (foundDir == null) continue; // это ключевое слово встретилось, но направления сразу после него нет — не наш случай, пробуем следующее ключевое слово

            role = r;
            direction = foundDir;

            string prefix = id.Substring(0, idx);
            string tail = rest.Substring(foundDir.Length); // всё, что шло после направления (например "White")
            stem = prefix + tail;
            return true;
        }

        return false;
    }

    private static DecalPosition? ResolvePosition(Role role, string suffix)
    {
        suffix = suffix.ToUpperInvariant();
        return role switch
        {
            Role.OuterCorner => suffix switch
            {
                "NE" => DecalPosition.OuterCornerNE,
                "NW" => DecalPosition.OuterCornerNW,
                "SE" => DecalPosition.OuterCornerSE,
                "SW" => DecalPosition.OuterCornerSW,
                _ => null
            },
            Role.InnerCorner => suffix switch
            {
                "NE" => DecalPosition.InnerCornerNE,
                "NW" => DecalPosition.InnerCornerNW,
                "SE" => DecalPosition.InnerCornerSE,
                "SW" => DecalPosition.InnerCornerSW,
                _ => null
            },
            Role.DeadEnd => suffix switch
            {
                "N" => DecalPosition.DeadEndN,
                "S" => DecalPosition.DeadEndS,
                "E" => DecalPosition.DeadEndE,
                "W" => DecalPosition.DeadEndW,
                _ => null
            },
            Role.Side => suffix switch
            {
                "N" => DecalPosition.SideN,
                "S" => DecalPosition.SideS,
                "E" => DecalPosition.SideE,
                "W" => DecalPosition.SideW,
                _ => null
            },
            _ => null
        };
    }
}