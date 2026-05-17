using System.Text.RegularExpressions;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Jewels;

public static partial class RadiusJewelParser
{
    public static RadiusJewelEffect? Parse(
        ImportedItem item,
        int socketNodeId,
        TreeModel tree,
        JewelRadiusTable table,
        IReadOnlyDictionary<string, int> keystoneNodeIdsByName)
    {
        var text = Normalize(item.RawText);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var radiusIndex = ParseRadiusIndex(lines, table, tree.GameId);
        var conqueror = ParseConqueror(text);
        var kind = DetermineKind(item, text, conqueror);
        var alternateCenter = ParseAlternateCenter(text, keystoneNodeIdsByName);

        if (alternateCenter is not null && kind == RadiusJewelKind.IntuitiveLeapLike)
        {
            kind = item.Name.Contains("From Nothing", StringComparison.OrdinalIgnoreCase)
                ? RadiusJewelKind.FromNothingLike
                : RadiusJewelKind.ImpossibleEscapeLike;
        }
        if (conqueror is not null)
        {
            kind = RadiusJewelKind.Timeless;
        }
        if (radiusIndex is null && kind == RadiusJewelKind.Timeless)
        {
            radiusIndex = table.NormalRadiusIndex("Large") ?? 3;
        }
        if (radiusIndex is null || kind == RadiusJewelKind.None)
        {
            return null;
        }

        return new RadiusJewelEffect(
            socketNodeId,
            item,
            kind,
            radiusIndex.Value,
            alternateCenter,
            conqueror,
            ParseTransforms(lines),
            AllowsUnconnectedAllocation: text.Contains("can be Allocated without being connected to your tree", StringComparison.OrdinalIgnoreCase));
    }

    private static int? ParseRadiusIndex(IReadOnlyList<string> lines, JewelRadiusTable table, GameId gameId)
    {
        foreach (var line in lines)
        {
            var match = RadiusLine().Match(line);
            if (match.Success && !match.Groups[1].Value.Equals("Variable", StringComparison.OrdinalIgnoreCase))
            {
                return table.NormalRadiusIndex(match.Groups[1].Value);
            }
        }

        foreach (var line in lines)
        {
            var match = RingLine().Match(line);
            if (match.Success)
            {
                return table.AnnulusRadiusIndex(match.Groups[1].Value);
            }
        }

        if (gameId == GameId.PathOfExile2 && lines.Any(l => l.Contains("Time-Lost", StringComparison.OrdinalIgnoreCase)))
        {
            return table.NormalRadiusIndex("Medium");
        }
        return null;
    }

    private static RadiusJewelKind DetermineKind(ImportedItem item, string text, TimelessConqueror? conqueror)
    {
        if (conqueror is not null || item.BaseType.Contains("Timeless Jewel", StringComparison.OrdinalIgnoreCase))
        {
            return RadiusJewelKind.Timeless;
        }
        if (text.Contains("Radius: Variable", StringComparison.OrdinalIgnoreCase))
        {
            return RadiusJewelKind.ThreadOfHopeLike;
        }
        if (text.Contains("can be Allocated without being connected to your tree", StringComparison.OrdinalIgnoreCase))
        {
            return RadiusJewelKind.IntuitiveLeapLike;
        }
        if (text.Contains("Radius:", StringComparison.OrdinalIgnoreCase))
        {
            return RadiusJewelKind.NormalRadius;
        }
        return RadiusJewelKind.None;
    }

    private static int? ParseAlternateCenter(string text, IReadOnlyDictionary<string, int> keystoneNodeIdsByName)
    {
        foreach (Match match in RadiusOfKeystoneLine().Matches(text))
        {
            var name = match.Groups[1].Value.Trim();
            if (keystoneNodeIdsByName.TryGetValue(name, out var id))
            {
                return id;
            }
        }
        foreach (var (name, id) in keystoneNodeIdsByName)
        {
            if (text.Contains($"Radius of {name}", StringComparison.OrdinalIgnoreCase)
                || text.Contains($"allocated {name}", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }
        return null;
    }

    private static TimelessConqueror? ParseConqueror(string text)
    {
        if (text.Contains("Eternal Empire", StringComparison.OrdinalIgnoreCase) || text.Contains("Chitus", StringComparison.OrdinalIgnoreCase) || text.Contains("Cadiro", StringComparison.OrdinalIgnoreCase) || text.Contains("Victario", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.EternalEmpire;
        }
        if (text.Contains("Karui", StringComparison.OrdinalIgnoreCase) || text.Contains("Kaom", StringComparison.OrdinalIgnoreCase) || text.Contains("Rakiata", StringComparison.OrdinalIgnoreCase) || text.Contains("Akoya", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.Karui;
        }
        if (text.Contains("Maraketh", StringComparison.OrdinalIgnoreCase) || text.Contains("Asenath", StringComparison.OrdinalIgnoreCase) || text.Contains("Nasima", StringComparison.OrdinalIgnoreCase) || text.Contains("Balbala", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.Maraketh;
        }
        if (text.Contains("Templar", StringComparison.OrdinalIgnoreCase) || text.Contains("Dominus", StringComparison.OrdinalIgnoreCase) || text.Contains("Avarius", StringComparison.OrdinalIgnoreCase) || text.Contains("Maxarius", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.Templar;
        }
        if (text.Contains("Vaal", StringComparison.OrdinalIgnoreCase) || text.Contains("Doryani", StringComparison.OrdinalIgnoreCase) || text.Contains("Xibaqua", StringComparison.OrdinalIgnoreCase) || text.Contains("Zerphi", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.Vaal;
        }
        if (text.Contains("Kalguur", StringComparison.OrdinalIgnoreCase))
        {
            return TimelessConqueror.Kalguuran;
        }
        return null;
    }

    private static IReadOnlyList<NodeStatTransform> ParseTransforms(IReadOnlyList<string> lines)
    {
        var transforms = new List<NodeStatTransform>();
        foreach (var line in lines)
        {
            var attr = AttributeTransformLine().Match(line);
            if (attr.Success)
            {
                transforms.Add(new NodeStatTransform(NodeStatTransformKind.ReplaceAttribute, From: attr.Groups[1].Value, To: attr.Groups[2].Value));
                continue;
            }

            var alsoGrant = AlsoGrantLine().Match(line);
            if (alsoGrant.Success)
            {
                transforms.Add(new NodeStatTransform(NodeStatTransformKind.AddLine, ParseNodeType(alsoGrant.Groups[1].Value), Line: alsoGrant.Groups[2].Value.Trim()));
                continue;
            }

            var passiveAlsoGrant = PassiveAlsoGrantLine().Match(line);
            if (passiveAlsoGrant.Success)
            {
                transforms.Add(new NodeStatTransform(NodeStatTransformKind.AddLine, Line: passiveAlsoGrant.Groups[1].Value.Trim()));
                continue;
            }

            var effect = IncreasedEffectLine().Match(line);
            if (effect.Success)
            {
                transforms.Add(new NodeStatTransform(NodeStatTransformKind.IncreasedEffect, ParseNodeType(effect.Groups[2].Value), Value: int.Parse(effect.Groups[1].Value)));
                continue;
            }

            if (line.Equals("Notable Passive Skills in Radius grant nothing", StringComparison.OrdinalIgnoreCase))
            {
                transforms.Add(new NodeStatTransform(NodeStatTransformKind.SuppressNodeStats, NodeType.Notable));
            }
        }
        return transforms;
    }

    private static NodeType? ParseNodeType(string value)
    {
        if (value.Contains("Notable", StringComparison.OrdinalIgnoreCase))
        {
            return NodeType.Notable;
        }
        if (value.Contains("Small", StringComparison.OrdinalIgnoreCase) || value.Contains("Non-Keystone", StringComparison.OrdinalIgnoreCase))
        {
            return NodeType.Normal;
        }
        return null;
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").Replace("{", string.Empty).Replace("}", string.Empty);

    [GeneratedRegex(@"^Radius:\s*(Small|Medium|Large|Very Large|Massive|Variable)$", RegexOptions.IgnoreCase)]
    private static partial Regex RadiusLine();

    [GeneratedRegex(@"(?:Only affects|Affects) Passives in (Very Small|Small|Medium-Small|Medium|Medium-Large|Large|Very Large|Massive) Ring", RegexOptions.IgnoreCase)]
    private static partial Regex RingLine();

    [GeneratedRegex(@"Passives in Radius of (.+?) can be Allocated without being connected to your tree", RegexOptions.IgnoreCase)]
    private static partial Regex RadiusOfKeystoneLine();

    [GeneratedRegex(@"^(Strength|Dexterity|Intelligence) from Passives in Radius is Transformed to (Strength|Dexterity|Intelligence)$", RegexOptions.IgnoreCase)]
    private static partial Regex AttributeTransformLine();

    [GeneratedRegex(@"^(Small|Notable|Non-Keystone|Passive) Passive Skills in Radius also grant:? (.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AlsoGrantLine();

    [GeneratedRegex(@"^Passive Skills in Radius also grant:? (.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PassiveAlsoGrantLine();

    [GeneratedRegex(@"^(\d+)% increased effect of (Small|Notable|Non-Keystone) Passive Skills in Radius$", RegexOptions.IgnoreCase)]
    private static partial Regex IncreasedEffectLine();
}
