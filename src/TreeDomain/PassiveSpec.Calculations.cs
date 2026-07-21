using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public enum PassiveAllocationPreviewKind
{
    None,
    Allocate,
    Deallocate,
}

public sealed record PassiveAllocationPreview(
    int? TargetNodeId,
    PassiveAllocationPreviewKind Kind,
    IReadOnlySet<int> NodeIds,
    bool HasUnmodeledJewelEffectChange = false)
{
    public static PassiveAllocationPreview None { get; } = new(
        null,
        PassiveAllocationPreviewKind.None,
        new HashSet<int>());

    public bool IsEmpty => Kind == PassiveAllocationPreviewKind.None || NodeIds.Count == 0;
}

public sealed partial class PassiveSpec
{
    /// <summary>
    /// Returns a stable snapshot of the effective stat text contributed by the
    /// currently allocated passives. Radius/timeless transformations, selected
    /// masteries, PoE2 attribute choices, and weapon-set allocations are applied.
    /// </summary>
    public IReadOnlyList<string> GetAllocatedStatLines(
        PassiveAllocationSet activeWeaponSet = PassiveAllocationSet.WeaponSet1,
        PassiveAllocationPreview? preview = null)
    {
        var previewedAllocations = new HashSet<int>(AllocatedNodes);
        if (preview is { IsEmpty: false })
        {
            if (preview.Kind == PassiveAllocationPreviewKind.Allocate)
            {
                previewedAllocations.UnionWith(preview.NodeIds);
            }
            else if (preview.Kind == PassiveAllocationPreviewKind.Deallocate)
            {
                previewedAllocations.ExceptWith(preview.NodeIds);
            }
        }

        var lines = new List<string>();
        foreach (var nodeId in previewedAllocations.Order())
        {
            var allocationSet = AllocationSetOf(nodeId);
            if (allocationSet != PassiveAllocationSet.Normal && allocationSet != activeWeaponSet)
            {
                continue;
            }
            if (!TryGetNode(nodeId, out var node) || node is null)
            {
                continue;
            }

            IReadOnlyList<string> stats;
            if (node.Type == NodeType.Mastery
                && _masterySelections.TryGetValue(nodeId, out var selectedEffectId)
                && node.MasteryEffects?.FirstOrDefault(effect => effect.Id == selectedEffectId) is { } selectedEffect)
            {
                stats = selectedEffect.Stats;
            }
            else
            {
                stats = EffectiveNode(nodeId).EffectiveStats;
            }

            if (_attributeOverrides.TryGetValue(nodeId, out var attribute))
            {
                var attributeName = attribute.ToString();
                lines.AddRange(stats.Select(line =>
                    line.Replace("any Attribute", attributeName, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                lines.AddRange(stats);
            }
        }
        return lines.ToArray();
    }
}
