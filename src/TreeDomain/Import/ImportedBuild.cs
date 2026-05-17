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
}

public enum AttributeNodeOverride
{
    Strength = 1,
    Dexterity = 2,
    Intelligence = 3,
}
