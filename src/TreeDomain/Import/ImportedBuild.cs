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
    public IReadOnlyList<ImportedItem> Items { get; init; } = [];
}
