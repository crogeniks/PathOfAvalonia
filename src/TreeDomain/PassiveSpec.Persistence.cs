using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    /// <summary>
    /// Projects the current mutable passive state onto a build snapshot. Data
    /// owned by other build sections is retained from <paramref name="build"/>.
    /// </summary>
    public ImportedBuild CreateBuildSnapshot(ImportedBuild build)
    {
        var selectedClass = Classes.GetClass(_selectedClassIndex);
        var selectedAscendancy = _selectedAscendancyIndex >= 0
            && _selectedAscendancyIndex < selectedClass.Ascendancies.Count
                ? selectedClass.Ascendancies[_selectedAscendancyIndex]
                : selectedClass.Ascendancies[0];
        var baseNodeHashes = _allocated
            .Where(Tree.Nodes.ContainsKey)
            .Order()
            .ToArray();
        var clusterNodeHashes = _allocated
            .Where(_clusterNodes.ContainsKey)
            .Order()
            .ToArray();

        return build with
        {
            ClassId = selectedClass.ExternalIntegerId ?? selectedClass.ClassIndex,
            AscendClassId = _selectedAscendancyIndex,
            NodeHashes = baseNodeHashes,
            ClusterNodeHashes = clusterNodeHashes,
            MasterySelections = new Dictionary<int, int>(_masterySelections),
            TreeVersion = Tree.Version,
            ClusterHashFormatVersion = 2,
            ClassInternalId = selectedClass.Name,
            AscendancyInternalId = selectedAscendancy.InternalId,
            AttributeOverrides = new Dictionary<int, AttributeNodeOverride>(_attributeOverrides),
            AllocationSets = new Dictionary<int, PassiveAllocationSet>(_allocationSets),
        };
    }
}
