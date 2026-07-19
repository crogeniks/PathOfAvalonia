using System.Text.RegularExpressions;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Jewels;

public static partial class TimelessJewelParser
{
    public static TimelessJewelSpec? Parse(ImportedItem item)
    {
        if (!item.BaseType.Trim().Equals("Timeless Jewel", StringComparison.OrdinalIgnoreCase)
            && !TryGetType(item.Name, out _))
        {
            return null;
        }

        foreach (var rawLine in item.RawText.Replace("\r\n", "\n").Split('\n'))
        {
            if (!ItemVariant.IsActive(rawLine, item.SelectedVariant))
            {
                continue;
            }

            var line = ItemText.StripTags(rawLine.Trim());
            var match = SeedLine().Match(line);
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, out var seed)
                || !TryGetConqueror(match.Groups[2].Value.Trim(), out var conqueror, out var conquerorId))
            {
                continue;
            }

            return new TimelessJewelSpec(TypeFor(conqueror), seed, conqueror, conquerorId);
        }

        return null;
    }

    public static bool TryGetType(string itemName, out TimelessJewelType type)
    {
        var name = itemName.Trim();
        foreach (var candidate in Enum.GetValues<TimelessJewelType>())
        {
            var canonicalName = TypeName(candidate);
            if (name.Equals(canonicalName, StringComparison.OrdinalIgnoreCase)
                || (name.StartsWith(canonicalName + " [", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(']')))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }

    private static string TypeName(TimelessJewelType type) => type switch
    {
        TimelessJewelType.GloriousVanity => "Glorious Vanity",
        TimelessJewelType.LethalPride => "Lethal Pride",
        TimelessJewelType.BrutalRestraint => "Brutal Restraint",
        TimelessJewelType.MilitantFaith => "Militant Faith",
        TimelessJewelType.ElegantHubris => "Elegant Hubris",
        TimelessJewelType.HeroicTragedy => "Heroic Tragedy",
        _ => string.Empty,
    };

    private static TimelessJewelType TypeFor(TimelessConqueror conqueror) => conqueror switch
    {
        TimelessConqueror.Vaal => TimelessJewelType.GloriousVanity,
        TimelessConqueror.Karui => TimelessJewelType.LethalPride,
        TimelessConqueror.Maraketh => TimelessJewelType.BrutalRestraint,
        TimelessConqueror.Templar => TimelessJewelType.MilitantFaith,
        TimelessConqueror.EternalEmpire => TimelessJewelType.ElegantHubris,
        TimelessConqueror.Kalguuran => TimelessJewelType.HeroicTragedy,
        _ => throw new ArgumentOutOfRangeException(nameof(conqueror)),
    };

    private static bool TryGetConqueror(string name, out TimelessConqueror conqueror, out string conquerorId)
    {
        (conqueror, conquerorId) = name.ToUpperInvariant() switch
        {
            "XIBAQUA" => (TimelessConqueror.Vaal, "1"),
            "ZERPHI" => (TimelessConqueror.Vaal, "2"),
            "DORYANI" => (TimelessConqueror.Vaal, "3"),
            "AHUANA" => (TimelessConqueror.Vaal, "2_v2"),
            "DESHRET" => (TimelessConqueror.Maraketh, "1"),
            "ASENATH" => (TimelessConqueror.Maraketh, "2"),
            "NASIMA" => (TimelessConqueror.Maraketh, "3"),
            "BALBALA" => (TimelessConqueror.Maraketh, "1_v2"),
            "CADIRO" => (TimelessConqueror.EternalEmpire, "1"),
            "VICTARIO" => (TimelessConqueror.EternalEmpire, "2"),
            "CHITUS" => (TimelessConqueror.EternalEmpire, "3"),
            "CASPIRO" => (TimelessConqueror.EternalEmpire, "3_v2"),
            "KAOM" => (TimelessConqueror.Karui, "1"),
            "RAKIATA" => (TimelessConqueror.Karui, "2"),
            "KILOAVA" => (TimelessConqueror.Karui, "3"),
            "AKOYA" => (TimelessConqueror.Karui, "3_v2"),
            "VENARIUS" => (TimelessConqueror.Templar, "1"),
            "DOMINUS" => (TimelessConqueror.Templar, "2"),
            "AVARIUS" => (TimelessConqueror.Templar, "3"),
            "MAXARIUS" => (TimelessConqueror.Templar, "1_v2"),
            "VORANA" => (TimelessConqueror.Kalguuran, "1"),
            "UHTRED" => (TimelessConqueror.Kalguuran, "2"),
            "MEDVED" => (TimelessConqueror.Kalguuran, "3"),
            _ => default,
        };
        return !string.IsNullOrEmpty(conquerorId);
    }

    [GeneratedRegex(@"^(?:Bathed in the blood of|Carved to glorify|Commanded leadership over|Commissioned|Denoted service of|Remembrancing)\s+(\d+)\b.*(?:name of|Templar|under|commemorate|akhara of|line of)\s+([^\r\n]+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SeedLine();
}
