using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    // Forbidden Flesh and Forbidden Flame grant an ascendancy passive only as a
    // matching pair. Keep these allocations separate from ordinary tree choices:
    // removing or changing the pair must immediately remove the granted passive.
    private readonly HashSet<int> _forbiddenJewelAllocatedNodes = new();

    private void RebuildForbiddenJewelAllocations()
    {
        _forbiddenJewelAllocatedNodes.Clear();

        var variants = _socketedJewels
            .Where(pair => _allocated.Contains(pair.Key))
            .Select(pair => new ForbiddenJewelVariant(
                pair.Key,
                ForbiddenJewelKindOf(pair.Value),
                AllocatedAscendancyPassive(pair.Value)))
            .Where(variant => variant.Kind != ForbiddenJewelKind.None && variant.PassiveName is not null)
            .ToArray();

        // A character can use one Flesh/Flame pairing. Imports can contain more
        // than one (for example from alternate item variants), so choose one
        // deterministically instead of granting every matching passive.
        var activePair = variants
            .Where(variant => variant.Kind == ForbiddenJewelKind.Flesh)
            .Join(
                variants.Where(variant => variant.Kind == ForbiddenJewelKind.Flame),
                flesh => flesh.PassiveName!,
                flame => flame.PassiveName!,
                (flesh, flame) => new { Flesh = flesh, Flame = flame },
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(pair => Math.Min(pair.Flesh.SocketNodeId, pair.Flame.SocketNodeId))
            .ThenBy(pair => Math.Max(pair.Flesh.SocketNodeId, pair.Flame.SocketNodeId))
            .FirstOrDefault();

        if (activePair is null)
        {
            return;
        }

        var target = Tree.Nodes.Values.FirstOrDefault(node =>
            node.AscendancyName is not null
            && NormalizePassiveName(node.Name) == NormalizePassiveName(activePair.Flesh.PassiveName!));
        if (target is not null)
        {
            _forbiddenJewelAllocatedNodes.Add(target.Id);
        }
    }

    private static ForbiddenJewelKind ForbiddenJewelKindOf(ImportedItem item) =>
        item.Name.Equals("Forbidden Flesh", StringComparison.OrdinalIgnoreCase)
            ? ForbiddenJewelKind.Flesh
            : item.Name.Equals("Forbidden Flame", StringComparison.OrdinalIgnoreCase)
                ? ForbiddenJewelKind.Flame
                : ForbiddenJewelKind.None;

    private static string? AllocatedAscendancyPassive(ImportedItem item)
    {
        const string prefix = "Allocates ";
        const string matchingModifier = " if you have the matching modifier on Forbidden ";
        foreach (var rawLine in item.RawText.Split('\n'))
        {
            if (!ItemVariant.IsActive(rawLine, item.SelectedVariant))
            {
                continue;
            }
            var line = ItemText.StripTags(rawLine.Trim());
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var markerIndex = line.IndexOf(matchingModifier, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > prefix.Length)
            {
                var matchingJewel = line[(markerIndex + matchingModifier.Length)..].Trim();
                if (matchingJewel.Equals("Flesh", StringComparison.OrdinalIgnoreCase)
                    || matchingJewel.Equals("Flame", StringComparison.OrdinalIgnoreCase))
                {
                    return line[prefix.Length..markerIndex].Trim();
                }
            }
        }
        return null;
    }

    private static string NormalizePassiveName(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private sealed record ForbiddenJewelVariant(int SocketNodeId, ForbiddenJewelKind Kind, string? PassiveName);

    private enum ForbiddenJewelKind { None, Flesh, Flame }
}
