using System.Text.RegularExpressions;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeDomain.Import;

public sealed record ImportedClusterJewel(
    ClusterJewelSize Size,
    int PassiveCount,
    int SocketCount,
    IReadOnlyList<string> NotableNames,
    string? KeystoneName = null);

public static class ImportedClusterJewelParser
{
    public static bool TryParse(ImportedItem item, out ImportedClusterJewel cluster)
    {
        cluster = null!;
        if (!TryParseSize(item, out var size))
        {
            return false;
        }

        var definition = ClusterJewelData.GetDefinition(size);
        var passiveCount = definition.MinNodes;
        var passiveCountWasExplicit = false;
        var socketCount = 0;
        var nothingnessCount = 0;
        var notables = new List<string>();

        foreach (var raw in item.RawText.Split('\n'))
        {
            var line = ItemText.StripTags(raw.Trim());
            if (line.Length == 0)
            {
                continue;
            }

            var metadataPassiveMatch = Regex.Match(line, @"^Cluster Jewel Node Count:\s*(?<count>\d+)$", RegexOptions.IgnoreCase);
            if (metadataPassiveMatch.Success && int.TryParse(metadataPassiveMatch.Groups["count"].Value, out var metadataPassiveCount))
            {
                passiveCount = metadataPassiveCount;
                passiveCountWasExplicit = true;
                continue;
            }

            var passiveMatch = Regex.Match(line, @"^Adds (?<count>\d+) Passive Skills$", RegexOptions.IgnoreCase);
            if (passiveMatch.Success && int.TryParse(passiveMatch.Groups["count"].Value, out var parsedPassiveCount))
            {
                passiveCount = parsedPassiveCount;
                passiveCountWasExplicit = true;
                continue;
            }

            var keystoneMatch = Regex.Match(line, @"^Adds (?<name>.+)$", RegexOptions.IgnoreCase);
            if (keystoneMatch.Success)
            {
                var name = keystoneMatch.Groups["name"].Value.Trim();
                if (ClusterKeystones.Contains(name))
                {
                    cluster = new ImportedClusterJewel(size, 1, 0, Array.Empty<string>(), name);
                    return true;
                }
            }

            var nothingnessMatch = Regex.Match(
                line,
                @"^Adds (?<count>\d+) Small Passive Skills? which grants? nothing$",
                RegexOptions.IgnoreCase);
            if (nothingnessMatch.Success && int.TryParse(nothingnessMatch.Groups["count"].Value, out var parsedNothingnessCount))
            {
                nothingnessCount = parsedNothingnessCount;
                continue;
            }

            var socketMatch = Regex.Match(
                line,
                @"^(?:(?<count>\d+) Added Passive Skills are Jewel Sockets|(?<single>1) Added Passive Skill is a Jewel Socket|Adds (?<adds>\d+) Jewel Socket Passive Skills?)$",
                RegexOptions.IgnoreCase);
            if (socketMatch.Success)
            {
                socketCount = ParseMatchedCount(socketMatch);
                continue;
            }

            var notableMatch = Regex.Match(line, @"^1 Added Passive Skill is (?<name>.+)$", RegexOptions.IgnoreCase);
            if (notableMatch.Success)
            {
                var name = notableMatch.Groups["name"].Value.Trim();
                if (!name.Equals("a Jewel Socket", StringComparison.OrdinalIgnoreCase))
                {
                    notables.Add(name);
                }
            }
        }

        socketCount = Math.Clamp(socketCount, 0, definition.SocketIndices.Count);
        if (!passiveCountWasExplicit && nothingnessCount > 0)
        {
            passiveCount = socketCount + notables.Count + nothingnessCount;
        }
        passiveCount = passiveCountWasExplicit
            ? Math.Clamp(passiveCount, definition.MinNodes, definition.MaxNodes)
            : Math.Clamp(passiveCount, 0, definition.MaxNodes);
        cluster = new ImportedClusterJewel(size, passiveCount, socketCount, notables);
        return true;
    }

    private static readonly HashSet<string> ClusterKeystones = new(StringComparer.OrdinalIgnoreCase)
    {
        "Disciple of Kitava",
        "Lone Messenger",
        "Nature's Patience",
        "Secrets of Suffering",
        "Kineticism",
        "Veteran's Awareness",
        "Hollow Palm Technique",
        "Pitfighter",
    };

    private static bool TryParseSize(ImportedItem item, out ClusterJewelSize size)
    {
        if (ContainsClusterBase(item, "Large Cluster Jewel"))
        {
            size = ClusterJewelSize.Large;
            return true;
        }
        if (ContainsClusterBase(item, "Medium Cluster Jewel"))
        {
            size = ClusterJewelSize.Medium;
            return true;
        }
        if (ContainsClusterBase(item, "Small Cluster Jewel"))
        {
            size = ClusterJewelSize.Small;
            return true;
        }

        size = default;
        return false;
    }

    private static bool ContainsClusterBase(ImportedItem item, string baseType) =>
        item.BaseType.Equals(baseType, StringComparison.OrdinalIgnoreCase)
        || item.RawText.Split('\n').Any(line => ItemText.StripTags(line.Trim()).Equals(baseType, StringComparison.OrdinalIgnoreCase));

    private static int ParseMatchedCount(Match match)
    {
        foreach (var groupName in new[] { "count", "single", "adds" })
        {
            if (int.TryParse(match.Groups[groupName].Value, out var value))
            {
                return value;
            }
        }
        return 0;
    }

}
