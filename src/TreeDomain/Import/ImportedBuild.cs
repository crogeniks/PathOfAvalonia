namespace PathOfAvalonia.TreeDomain.Import;

public sealed record ImportedBuild(
    int ClassId,
    int AscendClassId,
    int SecondaryAscendClassId,
    IReadOnlyList<int> NodeHashes,
    IReadOnlyList<int> ClusterNodeHashes,
    IReadOnlyDictionary<int, int> MasterySelections,
    string? TreeVersion,
    string Source)
{
    public int TotalAllocatedCount => NodeHashes.Count + ClusterNodeHashes.Count;
    public int ClusterHashFormatVersion { get; init; } = 2;
    public string? ClassInternalId { get; init; }
    public string? AscendancyInternalId { get; init; }
    public IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides { get; init; } =
        new Dictionary<int, AttributeNodeOverride>();
    public IReadOnlyList<ImportedItem> Items { get; init; } = [];
    public IReadOnlyDictionary<int, ImportedItem> ItemsById { get; init; } = new Dictionary<int, ImportedItem>();
    public IReadOnlyList<ImportedSocketedJewel> SocketedJewels { get; init; } = [];
    public IReadOnlyList<ImportedPassiveTreeVariant> PassiveTreeVariants { get; init; } = [];
    public IReadOnlyList<ImportedItemSetVariant> ItemSetVariants { get; init; } = [];
    public int ActivePassiveTreeVariantIndex { get; init; }
    public int ActiveItemSetVariantIndex { get; init; }

    public ImportedBuild WithPassiveTreeVariant(int index)
    {
        var variant = PassiveTreeVariants.FirstOrDefault(v => v.Index == index);
        if (variant is null)
        {
            if (PassiveTreeVariants.Count == 0 && index == 0)
            {
                return this;
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return this with
        {
            ClassId = variant.ClassId,
            AscendClassId = variant.AscendClassId,
            SecondaryAscendClassId = variant.SecondaryAscendClassId,
            NodeHashes = variant.NodeHashes,
            ClusterNodeHashes = variant.ClusterNodeHashes,
            MasterySelections = variant.MasterySelections,
            TreeVersion = variant.TreeVersion,
            ClusterHashFormatVersion = variant.ClusterHashFormatVersion,
            ClassInternalId = variant.ClassInternalId,
            AscendancyInternalId = variant.AscendancyInternalId,
            AttributeOverrides = variant.AttributeOverrides,
            SocketedJewels = variant.SocketedJewels,
            ActivePassiveTreeVariantIndex = index,
        };
    }

    public ImportedBuild WithItemSetVariant(int index)
    {
        var variant = ItemSetVariants.FirstOrDefault(v => v.Index == index);
        if (variant is null)
        {
            if (ItemSetVariants.Count == 0 && index == 0)
            {
                return this;
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return this with
        {
            Items = variant.Items,
            ActiveItemSetVariantIndex = index,
        };
    }

    public ImportedBuild WithVariants(int passiveIndex, int itemSetIndex) =>
        WithPassiveTreeVariant(passiveIndex).WithItemSetVariant(itemSetIndex);
}

public sealed record ImportedPassiveTreeVariant(
    int Index,
    string DisplayName,
    int ClassId,
    int AscendClassId,
    int SecondaryAscendClassId,
    IReadOnlyList<int> NodeHashes,
    IReadOnlyList<int> ClusterNodeHashes,
    IReadOnlyDictionary<int, int> MasterySelections,
    string? TreeVersion,
    int ClusterHashFormatVersion,
    string? ClassInternalId,
    string? AscendancyInternalId,
    IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides,
    IReadOnlyList<ImportedSocketedJewel> SocketedJewels);

public sealed record ImportedItemSetVariant(
    int Index,
    int Id,
    string DisplayName,
    IReadOnlyList<ImportedItem> Items);

public enum AttributeNodeOverride
{
    Strength = 1,
    Dexterity = 2,
    Intelligence = 3,
}
