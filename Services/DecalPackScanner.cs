// Services/DecalPackScanner.cs
using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Автоматически собирает DecalPack из проиндексированного репозитория: группирует
/// декали по общему "стеблю" имени (без ключевого слова роли и без суффикса
/// направления), определяет позицию каждой по суффиксу NE/NW/SE/SW/N/S/E/W.
/// Например BrickCornerOverlayNE, BrickEndOverlayN, BrickLineOverlayE — все дают
/// стебель "BrickOverlay" и попадают в один пак.
/// </summary>
public static class DecalPackScanner
{
    private static readonly string[] DiagonalSuffixes = { "NE", "NW", "SE", "SW" };
    private static readonly string[] CardinalSuffixes = { "N", "S", "E", "W" };

    private enum Role { InnerCorner, OuterCorner, DeadEnd, Side }

    // Кэш последнего результата сканирования по repoId — повторный клик "🔄" на том же
    // репозитории не гоняет заново по всем прототипам, только явная смена репозитория
    // или принудительный forceRescan
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

            if (!TryMatchSuffix(id, out string baseName, out string suffix)) continue;
            if (!TryClassifyRole(baseName, out Role role, out string stem)) continue;

            var position = ResolvePosition(role, suffix);
            if (position == null) continue;

            if (!families.TryGetValue(stem, out var pack))
            {
                pack = new DecalPack { Name = stem };
                families[stem] = pack;
            }
            pack.Positions[position.Value] = id;
        }

        return families.Values
                    .Where(p => p.Positions.Count > 0)
                    .OrderBy(p => p.Name)
                    .ToList();
    }
    
    private static bool TryMatchSuffix(string id, out string baseName, out string suffix)
    {
        // Сначала диагональные (2 буквы) — иначе "...NE" ошибочно поймается как "...N" + "E" потерян
        foreach (var s in DiagonalSuffixes)
        {
            if (id.Length > s.Length && id.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                baseName = id.Substring(0, id.Length - s.Length);
                suffix = s;
                return true;
            }
        }
        foreach (var s in CardinalSuffixes)
        {
            if (id.Length > s.Length && id.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                baseName = id.Substring(0, id.Length - s.Length);
                suffix = s;
                return true;
            }
        }
        baseName = ""; suffix = "";
        return false;
    }

    private static bool TryClassifyRole(string baseName, out Role role, out string stem)
    {
        int idx;
        if ((idx = baseName.IndexOf("Inner", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            role = Role.InnerCorner;
            stem = baseName.Remove(idx, "Inner".Length);
            return true;
        }
        if ((idx = baseName.IndexOf("Corner", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            role = Role.OuterCorner;
            stem = baseName.Remove(idx, "Corner".Length);
            return true;
        }
        if ((idx = baseName.IndexOf("End", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            role = Role.DeadEnd;
            stem = baseName.Remove(idx, "End".Length);
            return true;
        }
        if ((idx = baseName.IndexOf("Line", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            role = Role.Side;
            stem = baseName.Remove(idx, "Line".Length);
            return true;
        }
        role = default; stem = "";
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