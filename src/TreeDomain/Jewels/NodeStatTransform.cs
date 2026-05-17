using System.Text.RegularExpressions;

namespace PathOfAvalonia.TreeDomain.Jewels;

public enum NodeStatTransformKind
{
    ReplaceAttribute,
    AddLine,
    SuppressNodeStats,
    IncreasedEffect,
}

public sealed partial record NodeStatTransform(
    NodeStatTransformKind Kind,
    NodeType? AppliesToType = null,
    string? From = null,
    string? To = null,
    string? Line = null,
    int Value = 0)
{
    public bool AppliesTo(Node node) =>
        AppliesToType is null
        || AppliesToType == node.Type
        || (AppliesToType == NodeType.Normal && node.Type == NodeType.Mastery);

    public IReadOnlyList<string> Apply(Node node, IReadOnlyList<string> stats)
    {
        if (!AppliesTo(node))
        {
            return stats;
        }

        return Kind switch
        {
            NodeStatTransformKind.SuppressNodeStats => [],
            NodeStatTransformKind.AddLine when !string.IsNullOrWhiteSpace(Line) => stats.Concat([Line!]).ToArray(),
            NodeStatTransformKind.ReplaceAttribute when From is not null && To is not null => ReplaceAttribute(stats, From, To),
            NodeStatTransformKind.IncreasedEffect when Value != 0 => ApplyIncreasedEffect(stats, Value),
            _ => stats,
        };
    }

    private static IReadOnlyList<string> ReplaceAttribute(IReadOnlyList<string> stats, string from, string to)
    {
        var result = new List<string>(stats.Count);
        foreach (var stat in stats)
        {
            result.Add(stat.Replace(from, to, StringComparison.OrdinalIgnoreCase));
        }
        return result;
    }

    private static IReadOnlyList<string> ApplyIncreasedEffect(IReadOnlyList<string> stats, int value)
    {
        var multiplier = 1 + value / 100.0;
        var result = new List<string>(stats.Count);
        foreach (var stat in stats)
        {
            result.Add(LeadingInteger().Replace(stat, match =>
            {
                var original = int.Parse(match.Value);
                return Math.Round(original * multiplier).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }, 1));
        }
        return result;
    }

    [GeneratedRegex(@"(?<![\d.])-?\d+")]
    private static partial Regex LeadingInteger();
}
