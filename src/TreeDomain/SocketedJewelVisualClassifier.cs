using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public enum SocketedJewelVisualKind
{
    Unknown,
    Crimson,
    Viridian,
    Cobalt,
    Prismatic,
    Abyss,
    Timeless,
    LargeCluster,
    MediumCluster,
    SmallCluster,
    UrsineCharm,
    CorvineCharm,
    LupineCharm,
}

public static class SocketedJewelVisualClassifier
{
    private static readonly HashSet<string> AbyssBases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ghastly Eye Jewel",
        "Searching Eye Jewel",
        "Murderous Eye Jewel",
        "Hypnotic Eye Jewel",
    };

    public static SocketedJewelVisualKind Classify(ImportedItem item) =>
        Classify(item.BaseType, item.Name, item.RawText);

    public static SocketedJewelVisualKind Classify(string baseType, string name, string rawText)
    {
        foreach (var candidate in CandidateBaseNames(baseType, name, rawText))
        {
            if (AbyssBases.Contains(candidate))
            {
                return SocketedJewelVisualKind.Abyss;
            }

            var kind = candidate switch
            {
                "Crimson Jewel" => SocketedJewelVisualKind.Crimson,
                "Viridian Jewel" => SocketedJewelVisualKind.Viridian,
                "Cobalt Jewel" => SocketedJewelVisualKind.Cobalt,
                "Prismatic Jewel" => SocketedJewelVisualKind.Prismatic,
                "Timeless Jewel" => SocketedJewelVisualKind.Timeless,
                "Large Cluster Jewel" => SocketedJewelVisualKind.LargeCluster,
                "Medium Cluster Jewel" => SocketedJewelVisualKind.MediumCluster,
                "Small Cluster Jewel" => SocketedJewelVisualKind.SmallCluster,
                "Ursine Charm" => SocketedJewelVisualKind.UrsineCharm,
                "Corvine Charm" => SocketedJewelVisualKind.CorvineCharm,
                "Lupine Charm" => SocketedJewelVisualKind.LupineCharm,
                _ => SocketedJewelVisualKind.Unknown,
            };
            if (kind != SocketedJewelVisualKind.Unknown)
            {
                return kind;
            }
        }

        return SocketedJewelVisualKind.Unknown;
    }

    public static string? OverlayKey(ImportedItem item, bool isExpansionSocket) =>
        OverlayKey(item.BaseType, item.Name, item.RawText, isExpansionSocket);

    public static string? OverlayKey(string baseType, string name, string rawText, bool isExpansionSocket) =>
        OverlayKey(Classify(baseType, name, rawText), isExpansionSocket);

    public static string? OverlayKey(SocketedJewelVisualKind kind, bool isExpansionSocket) =>
        kind switch
        {
            SocketedJewelVisualKind.Crimson => isExpansionSocket ? "JewelSocketActiveRedAlt" : "JewelSocketActiveRed",
            SocketedJewelVisualKind.Viridian => isExpansionSocket ? "JewelSocketActiveGreenAlt" : "JewelSocketActiveGreen",
            SocketedJewelVisualKind.Cobalt => isExpansionSocket ? "JewelSocketActiveBlueAlt" : "JewelSocketActiveBlue",
            SocketedJewelVisualKind.Prismatic => isExpansionSocket ? "JewelSocketActivePrismaticAlt" : "JewelSocketActivePrismatic",
            SocketedJewelVisualKind.Abyss => isExpansionSocket ? "JewelSocketActiveAbyssAlt" : "JewelSocketActiveAbyss",
            SocketedJewelVisualKind.Timeless => isExpansionSocket ? "JewelSocketActiveLegionAlt" : "JewelSocketActiveLegion",
            SocketedJewelVisualKind.LargeCluster => "JewelSocketActiveAltPurple",
            SocketedJewelVisualKind.MediumCluster => "JewelSocketActiveAltBlue",
            SocketedJewelVisualKind.SmallCluster => "JewelSocketActiveAltRed",
            SocketedJewelVisualKind.UrsineCharm => "CharmSocketActiveStr",
            SocketedJewelVisualKind.CorvineCharm => "CharmSocketActiveInt",
            SocketedJewelVisualKind.LupineCharm => "CharmSocketActiveDex",
            _ => null,
        };

    private static IEnumerable<string> CandidateBaseNames(string baseType, string name, string rawText)
    {
        if (!string.IsNullOrWhiteSpace(baseType))
        {
            yield return baseType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name.Trim();
        }

        foreach (var raw in rawText.Split('\n'))
        {
            var line = StripTags(raw.Trim());
            if (line.Length == 0 || line.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase) || line == "--------")
            {
                continue;
            }
            yield return line;
        }
    }

    private static string StripTags(string line)
    {
        while (line.StartsWith('{'))
        {
            var close = line.IndexOf('}', StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }
            line = line[(close + 1)..].TrimStart();
        }
        return line;
    }
}
