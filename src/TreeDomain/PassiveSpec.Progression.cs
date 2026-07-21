namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    /// <summary>
    /// Counts allocated non-ascendancy passives that consume passive points.
    /// Item-granted forbidden-jewel allocations are excluded because they are
    /// not stored in the mutable allocation set.
    /// </summary>
    public PassivePointUsage CountAllocatedPassivePoints()
    {
        var total = 0;
        var weaponSet1 = 0;
        var weaponSet2 = 0;
        foreach (var nodeId in _allocated)
        {
            if (!TryGetNode(nodeId, out var node)
                || node is null
                || node.Type is NodeType.ClassStart or NodeType.AscendancyStart
                || node.AscendancyName is not null)
            {
                continue;
            }

            total++;
            switch (AllocationSetOf(nodeId))
            {
                case PassiveAllocationSet.WeaponSet1:
                    weaponSet1++;
                    break;
                case PassiveAllocationSet.WeaponSet2:
                    weaponSet2++;
                    break;
            }
        }

        return new PassivePointUsage(total, weaponSet1, weaponSet2);
    }

    public int MinimumCharacterLevelForAllocations() =>
        CharacterProgression.MinimumLevel(Tree.GameId, CountAllocatedPassivePoints());
}
