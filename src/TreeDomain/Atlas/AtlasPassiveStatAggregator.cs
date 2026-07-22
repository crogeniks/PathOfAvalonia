using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PathOfAvalonia.TreeDomain.Atlas;

public sealed record AggregatedAtlasStat(
    string Text,
    int SourceCount,
    IReadOnlySet<int> SourceNodeIds);
public sealed record AggregatedAtlasStatGroup(
    string Type,
    IReadOnlyList<AggregatedAtlasStat> Stats);

/// <summary>
/// Combines repeated allocated Atlas modifiers by their textual shape. Each
/// numeric position is summed independently, so repeated lines such as
/// "Maps have 10% chance to contain Ritual Altars" become one 80% line.
/// </summary>
public static partial class AtlasPassiveStatAggregator
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> MechanicAliases =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abyss"] = ["Abyss"],
            ["Atlas Memories"] = ["Memory"],
            ["Bestiary"] = ["Bestiary", "Beast", "Einhar"],
            ["Betrayal"] = ["Betrayal", "Immortal Syndicate", "Syndicate", "Safehouse"],
            ["Beyond"] = ["Beyond"],
            ["Blight"] = ["Blight", "Cassia", "Ichor Pump", "Building and Upgrading Towers"],
            ["Breach"] = ["Breach", "Hiveblood", "Wombgift", "Flammable Burrow", "Hive Monster", "Grasping Coffer"],
            ["Conquerors"] = ["Conqueror", "Baran", "Veritania", "Al-Hezmin", "Drox"],
            ["Delirium"] = ["Delirium", "Delirious", "Simulacrum"],
            ["Delve"] = ["Delve", "Sulphite", "Azurite", "Niko"],
            ["Divination Cards"] = ["Divination Card", "Scrying Orb"],
            ["Essence"] = ["Essence", "Remnant of Corruption", "Imprisoned Monster"],
            ["Expedition"] = ["Expedition", "Logbook", "Dannig", "Gwennen", "Rog's", "Tujen", "Remnant", "Runic Monster", "Explosive", "Artifact", "Vendor Refresh"],
            ["Harvest"] = ["Harvest", "Sacred Grove", "Lifeforce"],
            ["Heist"] = ["Heist", "Smuggler's Cache", "Blueprint", "Contract", "Rogue's Marker"],
            ["Incursion"] = ["Incursion", "Alva", "Temple of Atzoatl", "Architect"],
            ["Labyrinth"] = ["Labyrinth", "Trial of Ascendancy"],
            ["Legion"] = ["Legion", "Timeless"],
            ["Maps"] = ["Map"],
            ["Mercenaries"] = ["Mercenar"],
            ["Ritual"] = ["Ritual", "Tribute"],
            ["Rogue Exiles"] = ["Rogue Exile"],
            ["Scarabs"] = ["Scarab"],
            ["Settlers of Kalguur"] = ["Settlers of Kalguur", "Kalguur", "Ore Deposit", "Lost Shipment"],
            ["Shrines"] = ["Shrine"],
            ["Strongboxes"] = ["Strongbox"],
            ["Synthesis"] = ["Synthesis", "Synthesised"],
            ["The Eater of Worlds"] = ["Eater of Worlds", "Eater Influence"],
            ["The Searing Exarch"] = ["Searing Exarch", "Exarch Influence"],
            ["The Shaper and Elder"] = ["Shaper", "Elder"],
            ["Torment"] = ["Torment", "Tormented Spirit", "Possess"],
            ["Ultimatum"] = ["Ultimatum"],
            ["Vaal Side Areas"] = ["Vaal Side Area"],
        };

    public static IReadOnlyList<AggregatedAtlasStatGroup> AggregateGroups(
        AtlasTreeModel tree,
        IEnumerable<int> allocatedNodeIds)
    {
        var categoryByGroup = tree.Nodes.Values
            .Where(node => node.Type == AtlasNodeType.ClusterIcon && !string.IsNullOrWhiteSpace(node.Name))
            .GroupBy(node => node.GroupId)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var mechanicTypes = categoryByGroup.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var entriesByType = allocatedNodeIds
            .Distinct()
            .Select(nodeId => tree.Nodes.TryGetValue(nodeId, out var node) ? node : null)
            .Where(node => node is not null
                && !node.IsGateway
                && node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
            .SelectMany(node => node!.Stats.Select(stat => new StatEntry(
                node.Id,
                stat,
                ModifierType(node, stat, categoryByGroup, mechanicTypes))))
            .GroupBy(entry => entry.Type, StringComparer.OrdinalIgnoreCase);

        return entriesByType
            .Select(group => new AggregatedAtlasStatGroup(
                group.Key,
                AggregateEntries(group)
                    .OrderBy(stat => stat.Text, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .Where(group => group.Stats.Count > 0)
            .OrderBy(group => TypeOrder(group.Type))
            .ThenBy(group => group.Type, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<AggregatedAtlasStat> Aggregate(
        AtlasTreeModel tree,
        IEnumerable<int> allocatedNodeIds)
    {
        var entries = allocatedNodeIds
            .Distinct()
            .Select(nodeId => tree.Nodes.TryGetValue(nodeId, out var node) ? node : null)
            .Where(node => node is not null
                && !node.IsGateway
                && node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
            .SelectMany(node => node!.Stats.Select(stat => new StatEntry(node.Id, stat, string.Empty)));
        return AggregateEntries(entries);
    }

    private static IReadOnlyList<AggregatedAtlasStat> AggregateEntries(IEnumerable<StatEntry> entries)
    {
        var accumulators = new Dictionary<string, StatAccumulator>(StringComparer.Ordinal);
        var ordered = new List<StatAccumulator>();

        foreach (var entry in entries)
        {
            var parsed = ParsedStat.TryParse(entry.Text);
            var key = parsed?.Pattern ?? $"text:{entry.Text}";
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new StatAccumulator(entry.Text, parsed);
                accumulators[key] = accumulator;
                ordered.Add(accumulator);
            }
            accumulator.Add(parsed, entry.NodeId);
        }

        return ordered
            .Select(accumulator => accumulator.ToResult())
            .ToArray();
    }

    private static string ModifierType(
        AtlasNode node,
        string stat,
        IReadOnlyDictionary<int, string> categoryByGroup,
        IReadOnlyList<string> mechanicTypes)
    {
        categoryByGroup.TryGetValue(node.GroupId, out var nodeCategory);
        var classificationText = $"{node.Name}\n{stat}";
        var referencedTypes = mechanicTypes
            .Where(type => MechanicMentioned(type, classificationText))
            .ToArray();

        var specificTypes = referencedTypes
            .Where(type => type is not ("Maps" or "Scarabs"))
            .ToArray();
        if (specificTypes.Length == 1)
        {
            return specificTypes[0];
        }
        if (specificTypes.Length > 1
            && nodeCategory is not null
            && specificTypes.Contains(nodeCategory, StringComparer.OrdinalIgnoreCase))
        {
            return nodeCategory;
        }
        if (specificTypes.Length > 1)
        {
            return nodeCategory ?? FallbackNodeType(node);
        }
        if (referencedTypes.Contains("Scarabs", StringComparer.OrdinalIgnoreCase))
        {
            return "Scarabs";
        }
        if (referencedTypes.Contains("Maps", StringComparer.OrdinalIgnoreCase))
        {
            return "Maps";
        }

        return nodeCategory ?? FallbackNodeType(node);
    }

    private static bool MechanicMentioned(string type, string stat)
    {
        var aliases = MechanicAliases.TryGetValue(type, out var configured) ? configured : [type];
        return aliases.Any(alias => stat.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }

    private static string FallbackNodeType(AtlasNode node) =>
        node.Type == AtlasNodeType.Keystone ? "Keystones"
        : node.Type == AtlasNodeType.Notable ? "Notables"
        : "General";

    private static int TypeOrder(string type) => type switch
    {
        "General" => 0,
        "Notables" => 1,
        "Keystones" => 2,
        _ => 3,
    };

    private sealed record StatEntry(int NodeId, string Text, string Type);

    private sealed class StatAccumulator(string originalText, ParsedStat? first)
    {
        private readonly decimal[] _totals = first?.Values.ToArray() ?? [];
        private readonly bool[] _explicitPlus = first?.ExplicitPlus.ToArray() ?? [];
        private readonly string _originalText = originalText;
        private readonly string? _pattern = first?.Pattern;
        private readonly HashSet<int> _sourceNodeIds = [];

        public int SourceCount { get; private set; }

        public void Add(ParsedStat? parsed, int sourceNodeId)
        {
            SourceCount++;
            _sourceNodeIds.Add(sourceNodeId);
            if (SourceCount == 1 || parsed is null)
            {
                return;
            }

            for (var index = 0; index < _totals.Length; index++)
            {
                _totals[index] += parsed.Values[index];
                _explicitPlus[index] |= parsed.ExplicitPlus[index];
            }
        }

        public AggregatedAtlasStat ToResult()
        {
            if (SourceCount == 1 || _pattern is null)
            {
                return new AggregatedAtlasStat(_originalText, SourceCount, _sourceNodeIds);
            }

            var text = _pattern;
            for (var index = 0; index < _totals.Length; index++)
            {
                var formatted = _totals[index].ToString("0.############################", CultureInfo.InvariantCulture);
                if (_explicitPlus[index] && _totals[index] > 0)
                {
                    formatted = $"+{formatted}";
                }
                text = text.Replace($"{{{index}}}", formatted, StringComparison.Ordinal);
            }
            return new AggregatedAtlasStat(text, SourceCount, _sourceNodeIds);
        }
    }

    private sealed record ParsedStat(
        string Pattern,
        IReadOnlyList<decimal> Values,
        IReadOnlyList<bool> ExplicitPlus)
    {
        public static ParsedStat? TryParse(string line)
        {
            var matches = NumberRegex().Matches(line);
            if (matches.Count == 0)
            {
                return null;
            }

            var hasPercentage = matches.Any(match =>
                match.Index + match.Length < line.Length && line[match.Index + match.Length] == '%');
            var selectedMatches = matches
                .Where(match => hasPercentage
                    ? match.Index + match.Length < line.Length && line[match.Index + match.Length] == '%'
                    : !IsTierQualifier(line, match))
                .ToArray();
            if (selectedMatches.Length == 0)
            {
                return null;
            }

            var pattern = new StringBuilder(line.Length + selectedMatches.Length * 2);
            var values = new List<decimal>(selectedMatches.Length);
            var explicitPlus = new List<bool>(selectedMatches.Length);
            var position = 0;
            for (var index = 0; index < selectedMatches.Length; index++)
            {
                var match = selectedMatches[index];
                if (!decimal.TryParse(
                        match.Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return null;
                }

                pattern.Append(line, position, match.Index - position);
                pattern.Append('{').Append(index).Append('}');
                position = match.Index + match.Length;
                values.Add(value);
                explicitPlus.Add(match.Value.StartsWith('+'));
            }
            pattern.Append(line, position, line.Length - position);
            return new ParsedStat(pattern.ToString(), values, explicitPlus);
        }

        private static bool IsTierQualifier(string line, Match match)
        {
            var tierRangeEnd = line.IndexOf(" Maps", StringComparison.OrdinalIgnoreCase);
            if (line.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase)
                && tierRangeEnd > 0
                && match.Index < tierRangeEnd)
            {
                return true;
            }

            return line[(match.Index + match.Length)..]
                .TrimStart()
                .StartsWith("tier", StringComparison.OrdinalIgnoreCase);
        }
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
